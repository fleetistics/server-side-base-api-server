using db_model.Gps;
using db_model.Map;
using db_model.Team;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Threading.Channels;

namespace api_server.Controllers.Teams.Services
{
	/// <summary>Config section: MockTeamMembers.</summary>
	public sealed class MockTeamMemberMoveOptions
	{
		/// <summary>Master switch; when false teams are never treated as mock teams.</summary>
		public bool Enabled { get; set; } = true;

		/// <summary>A team whose notes start with this text (case-insensitive) gets mock members.</summary>
		public string NotesPrefix { get; set; } = "mock members";

		/// <summary>User.UserSourceId of the users that are moved into the team.</summary>
		public short MockUserSourceId { get; set; } = 13;

		/// <summary>How often to look for the creator's map state until it exists.</summary>
		public int CreatorPollSeconds { get; set; } = 10;

		/// <summary>Pause between switching one mock user to the team and the next.</summary>
		public int SwitchDelaySeconds { get; set; } = 30;

		/// <summary>How often the positions of the switched users are advanced and saved.</summary>
		public int MoveIntervalSeconds { get; set; } = 5;

		/// <summary>A user's initial location is random within this radius of the creator.</summary>
		public double StartRadiusKm { get; set; } = 1;

		/// <summary>The centre of a user's circle is random within this radius of the creator.</summary>
		public double CircleCenterRadiusKm { get; set; } = 1;

		public double CircleRadiusKm { get; set; } = 2;

		public double MinSpeedKmh { get; set; } = 5;
		public double MaxSpeedKmh { get; set; } = 20;
	}

	public interface IMockTeamMemberMove
	{
		/// <summary>True when mock members are enabled and <paramref name="notes"/> starts with the configured prefix.</summary>
		bool IsMockTeamNotes(string? notes);

		/// <summary>Queues the background job for an already started team; returns immediately.</summary>
		void Start(int teamId, int creatorUserId);
	}

	/// <summary>
	/// Demo/test helper: once the creator of a "mock members" team reports a location, moves the
	/// mock users (UserSource = MockUserSourceId) into the team one by one and keeps their
	/// MobileGpsDeviceMapState moving on circles around the creator. Singleton background worker
	/// (work queue = channel, scope per DB access), same shape as MobileGpsDeviceMapTrackService.
	/// Jobs live in memory only: they do not survive an app restart, and each one ends when its
	/// team is no longer Active.
	/// </summary>
	public sealed class MockTeamMemberMove : BackgroundService, IMockTeamMemberMove
	{
		private const double KmPerDegreeLat = 111.32;

		private sealed record Job(int TeamId, int CreatorUserId);

		// Position is kept in meters east (X) / north (Y) of the member's own circle centre.
		private sealed class Member
		{
			public int UserId { get; init; }
			public int DeviceId { get; init; }
			public double CenterEastKm { get; init; }
			public double CenterNorthKm { get; init; }
			public double X { get; set; }
			public double Y { get; set; }
			public double SpeedMs { get; init; }
			public bool Clockwise { get; init; }
			public bool Approaching { get; set; } = true;
			public double Angle { get; set; }
			public short? Heading { get; set; }
		}

		public MockTeamMemberMove(IServiceScopeFactory scopeFactory, IOptions<MockTeamMemberMoveOptions> options, ILogger<MockTeamMemberMove> logger)
		{
			mScopeFactory = scopeFactory;
			mOptions = options.Value;
			mLogger = logger;
		}

		public bool IsMockTeamNotes(string? notes) =>
			mOptions.Enabled && notes != null && notes.StartsWith(mOptions.NotesPrefix, StringComparison.OrdinalIgnoreCase);

		public void Start(int teamId, int creatorUserId)
		{
			if (!mOptions.Enabled) return;
			mChannel.Writer.TryWrite(new Job(teamId, creatorUserId));
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			mChannel.Writer.TryComplete();
			await base.StopAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				await foreach (var job in mChannel.Reader.ReadAllAsync(stoppingToken))
				{
					// One long-running task per team so jobs never block each other.
					_ = Task.Run(() => runJobAsync(job, stoppingToken), stoppingToken);
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private async Task runJobAsync(Job job, CancellationToken cancellationToken)
		{
			try
			{
				var creatorLocation = await waitForCreatorLocationAsync(job, cancellationToken);
				if (creatorLocation == null) return;
				mLogger.LogInformation("Mock members for team {TeamId}: creator location found, starting", job.TeamId);

				List<int> userIds;
				using (var scope = mScopeFactory.CreateScope())
				{
					var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
					userIds = await repository.GetQueryable<User>(u => u.UserSourceId == mOptions.MockUserSourceId)
						.OrderBy(u => u.Id).Select(u => u.Id).ToListAsync(cancellationToken);
				}
				var pending = new Queue<int>(userIds);
				var members = new List<Member>();
				var nextSwitch = DateTime.UtcNow;
				var lastMove = DateTime.UtcNow;

				while (!cancellationToken.IsCancellationRequested)
				{
					var now = DateTime.UtcNow;
					if (pending.Count > 0 && now >= nextSwitch)
					{
						var member = await switchUserAsync(job, pending.Dequeue(), creatorLocation, cancellationToken);
						if (member != null) members.Add(member);
						nextSwitch = DateTime.UtcNow.AddSeconds(mOptions.SwitchDelaySeconds);
					}

					var elapsed = (now - lastMove).TotalSeconds;
					lastMove = now;
					if (!await moveMembersAsync(job, members, creatorLocation, elapsed, cancellationToken))
					{
						mLogger.LogInformation("Mock members for team {TeamId}: team is no longer active, stopping", job.TeamId);
						return;
					}

					await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, mOptions.MoveIntervalSeconds)), cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				mLogger.LogError(ex, "Mock members job for team {TeamId} failed", job.TeamId);
			}
		}

		/// <summary>Polls until the creator has a map state; null when the team stops being Active meanwhile.</summary>
		private async Task<Point?> waitForCreatorLocationAsync(Job job, CancellationToken cancellationToken)
		{
			while (true)
			{
				using (var scope = mScopeFactory.CreateScope())
				{
					var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
					if (!await isTeamActiveAsync(repository, job.TeamId, cancellationToken)) return null;

					var location = await repository.GetQueryable<MobileGpsDeviceMapState>(s => s.UserId == job.CreatorUserId)
						.AsNoTracking()
						.OrderByDescending(s => s.LatestOnMapUpdate)
						.Select(s => s.Location)
						.FirstOrDefaultAsync(cancellationToken);
					if (location != null) return location;
				}
				await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, mOptions.CreatorPollSeconds)), cancellationToken);
			}
		}

		private static Task<bool> isTeamActiveAsync(IRepository repository, int teamId, CancellationToken cancellationToken) =>
			repository.GetQueryable<db_model.Team.Team>(t => t.Id == teamId && t.StatusId == TeamStatus.Active).AnyAsync(cancellationToken);

		/// <summary>
		/// Makes the team the user's only one (other memberships are deleted) and creates the user's
		/// first fake map state at a random spot near the creator.
		/// </summary>
		private async Task<Member?> switchUserAsync(Job job, int userId, Point creatorLocation, CancellationToken cancellationToken)
		{
			try
			{
				using var scope = mScopeFactory.CreateScope();
				var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

				// MobileGpsDeviceMapState is keyed by device, so every mock user needs one.
				var device = await repository.GetQueryable<MobileGpsDevice>(d => d.UserId == userId).OrderBy(d => d.Id).FirstOrDefaultAsync(cancellationToken);
				if (device == null)
				{
					device = new MobileGpsDevice
					{
						UserId = userId,
						DeviceUID = "mock-" + userId,
						ProviderId = GpsDeviceProvider.Unknown,
						StatusId = GpsDeviceStatus.Active
					};
					repository.Create(device);
					await repository.SaveAsync(cancellationToken);
				}

				var otherMemberships = await repository.GetQueryable<TeamMemberUser>(e => e.UserId == userId && e.TeamId != job.TeamId).ToListAsync(cancellationToken);
				foreach (var other in otherMemberships) repository.Delete(other);
				var membership = await repository.GetQueryable<TeamMemberUser>(e => e.UserId == userId && e.TeamId == job.TeamId).FirstOrDefaultAsync(cancellationToken);
				if (membership == null)
				{
					repository.Create(new TeamMemberUser { TeamId = job.TeamId, UserId = userId, StatusId = TeamMemberUserStatus.Active, RoleId = TeamUserRole.Member });
				}
				else
				{
					membership.StatusId = TeamMemberUserStatus.Active;
				}

				var (startEastKm, startNorthKm) = randomPointInCircle(mOptions.StartRadiusKm);
				var (centerEastKm, centerNorthKm) = randomPointInCircle(mOptions.CircleCenterRadiusKm);
				var minSpeed = Math.Min(mOptions.MinSpeedKmh, mOptions.MaxSpeedKmh);
				var maxSpeed = Math.Max(mOptions.MinSpeedKmh, mOptions.MaxSpeedKmh);
				var member = new Member
				{
					UserId = userId,
					DeviceId = device.Id,
					CenterEastKm = centerEastKm,
					CenterNorthKm = centerNorthKm,
					X = (startEastKm - centerEastKm) * 1000,
					Y = (startNorthKm - centerNorthKm) * 1000,
					SpeedMs = (minSpeed + Random.Shared.NextDouble() * (maxSpeed - minSpeed)) / 3.6,
					Clockwise = Random.Shared.Next(2) == 0,
					Angle = Random.Shared.NextDouble() * 2 * Math.PI
				};

				var mapState = await repository.GetByIdAsync<MobileGpsDeviceMapState>(device.Id, cancellationToken);
				if (mapState == null)
				{
					mapState = new MobileGpsDeviceMapState { MobileGpsDeviceId = device.Id };
					repository.Create(mapState);
				}
				applyToMapState(mapState, member, job, creatorLocation);

				await repository.SaveAsync(cancellationToken);
				mLogger.LogInformation("Mock members for team {TeamId}: user {UserId} switched to the team", job.TeamId, userId);
				return member;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				mLogger.LogError(ex, "Mock members for team {TeamId}: failed to switch user {UserId}", job.TeamId, userId);
				return null;
			}
		}

		/// <summary>Advances every switched member and saves their map states; false when the team is no longer Active.</summary>
		private async Task<bool> moveMembersAsync(Job job, List<Member> members, Point creatorLocation, double elapsedSeconds, CancellationToken cancellationToken)
		{
			using var scope = mScopeFactory.CreateScope();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
			if (!await isTeamActiveAsync(repository, job.TeamId, cancellationToken)) return false;
			if (members.Count == 0) return true;

			var deviceIds = members.Select(m => m.DeviceId).ToList();
			var states = (await repository.GetQueryable<MobileGpsDeviceMapState>(s => deviceIds.Contains(s.MobileGpsDeviceId)).ToListAsync(cancellationToken))
				.ToDictionary(s => s.MobileGpsDeviceId);
			foreach (var member in members)
			{
				advance(member, member.SpeedMs * elapsedSeconds);
				if (!states.TryGetValue(member.DeviceId, out var state))
				{
					state = new MobileGpsDeviceMapState { MobileGpsDeviceId = member.DeviceId };
					repository.Create(state);
				}
				applyToMapState(state, member, job, creatorLocation);
			}
			await repository.SaveAsync(cancellationToken);
			return true;
		}

		// First head straight to the circle (the initial spot is random, not on it), then go round it.
		private void advance(Member m, double stepMeters)
		{
			var radius = mOptions.CircleRadiusKm * 1000;
			var oldX = m.X;
			var oldY = m.Y;
			if (m.Approaching)
			{
				var r = Math.Sqrt(m.X * m.X + m.Y * m.Y);
				if (r < 1e-6)
				{
					m.X = 1e-6 * Math.Cos(m.Angle);
					m.Y = 1e-6 * Math.Sin(m.Angle);
					r = 1e-6;
				}
				var gap = radius - r;
				if (Math.Abs(gap) <= stepMeters)
				{
					m.Angle = Math.Atan2(m.Y, m.X);
					m.X = radius * Math.Cos(m.Angle);
					m.Y = radius * Math.Sin(m.Angle);
					m.Approaching = false;
				}
				else
				{
					var move = Math.Sign(gap) * stepMeters;
					m.X += m.X / r * move;
					m.Y += m.Y / r * move;
				}
			}
			else
			{
				m.Angle += (m.Clockwise ? -1 : 1) * stepMeters / radius;
				m.X = radius * Math.Cos(m.Angle);
				m.Y = radius * Math.Sin(m.Angle);
			}

			var dx = m.X - oldX;
			var dy = m.Y - oldY;
			if (Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9)
			{
				var bearing = Math.Atan2(dx, dy) * 180 / Math.PI; // 0 = north, clockwise
				m.Heading = (short)(Math.Round((bearing + 360) % 360) % 360);
			}
		}

		private void applyToMapState(MobileGpsDeviceMapState state, Member m, Job job, Point creatorLocation)
		{
			var northKm = m.CenterNorthKm + m.Y / 1000;
			var eastKm = m.CenterEastKm + m.X / 1000;
			var latitude = creatorLocation.Y + northKm / KmPerDegreeLat;
			var longitude = creatorLocation.X + eastKm / (KmPerDegreeLat * Math.Cos(creatorLocation.Y * Math.PI / 180));

			state.UserId = m.UserId;
			state.TeamId = job.TeamId;
			state.Location = mGeometryFactory.CreatePoint(new Coordinate(longitude, latitude));
			state.Speed = (float)m.SpeedMs;
			state.Dir = m.Heading;
			state.MotionActivity = m.SpeedMs * 3.6 < 12 ? MobileDeviceMotionActivity.Running : MobileDeviceMotionActivity.OnBicycle;
			state.LatestOnMapUpdate = DateTime.UtcNow;
		}

		// Uniform over the disc (sqrt keeps points from bunching at the centre); result is (east, north) in km.
		private static (double EastKm, double NorthKm) randomPointInCircle(double radiusKm)
		{
			var distance = radiusKm * Math.Sqrt(Random.Shared.NextDouble());
			var angle = Random.Shared.NextDouble() * 2 * Math.PI;
			return (distance * Math.Cos(angle), distance * Math.Sin(angle));
		}

		private readonly Channel<Job> mChannel = Channel.CreateUnbounded<Job>();
		private readonly GeometryFactory mGeometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
		private readonly IServiceScopeFactory mScopeFactory;
		private readonly MockTeamMemberMoveOptions mOptions;
		private readonly ILogger mLogger;
	}
}
