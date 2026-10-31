using api_server.Controllers.MapData.Dto;
using db_model.Gps;
using db_model.Map;
using db_model.Team;
using db_model.UserManagement;
using exs.Commons.Utils;
using exs.Database.Commons.Interfaces;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Text.Json;
using System.Threading.Channels;

namespace api_server.Controllers.MapData.Services
{

	/// <summary>
	/// Persists mobile GPS track points off the request path: a singleton background worker
	/// with an in-process channel as its work queue, mirroring MediaPreviewService. Unlike the
	/// legacy pssp MapTrackService this ports from, every queued item already carries its own
	/// MobileGpsDeviceId/UserId from a single authenticated request, so there is no batch-wide
	/// grouping/ordering across users to do here — each command is processed independently.
	/// </summary>
	public sealed class MobileGpsDeviceMapTrackService : BackgroundService
	{
		private enum CommandType
		{
			UpdateTrackPoints
		}

		private class Command
		{
			public CommandType Type { get; set; }
            public int UserId { get; set; }
			public int MobileGpsDeviceId { get; set; }
			public int? CompanyId { get; set; }
			public DateTime ReceivedAt { get; set; }
            public List<MobileLocation> TrackPoints { get; set; } = default!;
		}

		public MobileGpsDeviceMapTrackService(IServiceScopeFactory scopeFactory, ILogger<MobileGpsDeviceMapTrackService> logger)
		{
			mScopeFactory = scopeFactory;
			mLogger = logger;
		}

		/// <summary>
		/// Turns a parsed location report into track points item by item: an unusable item is logged and skipped,
		/// the rest of the batch is still processed.
		/// </summary>
		public async Task ParseAndUpdateDeviceLocationsAsync(int userId, int? companyId, int mobileGpsDeviceId, List<MobileLocation?> locations, CancellationToken cancellationToken)
		{
			if (locations.Count > 0)
			{
                await mChannel.Writer.WriteAsync(new Command { Type = CommandType.UpdateTrackPoints, UserId=userId, MobileGpsDeviceId=mobileGpsDeviceId, CompanyId=companyId, TrackPoints = locations }, cancellationToken);
                mLogger.LogInformation("Parsed and enqueued {Count} location points for user {UserId}", locations.Count, userId);
	
            }
   //         var points = new List<MobileGpsDeviceTrackPoint>(locations.Count);
			//var now = DateTime.UtcNow;
			//foreach (var location in locations)
			//{
			//	try
			//	{
			//		if (location == null)
			//		{
			//			mLogger.LogWarning("Null location item in report");
			//			continue;
			//		}

			//		var point = toTrackPoint(location, mobileGpsDeviceId, now);
			//		if (point == null)
			//		{
			//			//mLogger.LogWarning("Location item {Uuid} has no coordinates or timestamp", location.Uuid);
			//			continue;
			//		}

			//		// ---- `location` is parsed and valid here: coords, battery, activity, provider, geofence, extras, uuid, ... ----
			//		// TODO: map state update goes here (not implemented yet).

			//		points.Add(point);
			//	}
			//	catch (Exception ex)
			//	{
			//		//mLogger.LogError(ex, "Error processing location item {Uuid}", location?.Uuid);
			//	}
			//}
			//if (points.Count > 0)
			//{
			//	await mChannel.Writer.WriteAsync(new Command { Type = CommandType.UpdateTrackPoints, TrackPoints = points, UserId = userId, TeamId = teamId, MobileGpsDeviceId = mobileGpsDeviceId }, cancellationToken);
			//	mLogger.LogInformation("Parsed and enqueued {Count} location points for user {UserId}", points.Count, userId);
			//}
		}

		// Null when the item lacks what a track point cannot do without: a position and a device time.
		private MobileGpsDeviceTrackPoint? toTrackPoint(MobileLocation location, int mobileGpsDeviceId, DateTime now)
		{
			var coords = location.Coords;
			if (coords?.Latitude == null || coords.Longitude == null || location.Timestamp == null) return null;

			var point = new MobileGpsDeviceTrackPoint
			{
				MobileGpsDeviceId = mobileGpsDeviceId,
				ReceivedDate = now,
				DeviceDate = location.Timestamp.Value,
				Location = mGeometryFactory.CreatePoint(new Coordinate(coords.Longitude.Value, coords.Latitude.Value)),
				LocationAccuracy = mapLocationAccuracy(coords.Accuracy),
			};

			// Speed and heading are only trusted when the device also reports a positive accuracy for them.
			if (coords.SpeedAccuracy > 0 && coords.Speed >= 0) point.Speed = coords.Speed;
			if (coords.HeadingAccuracy > 0 && coords.Heading >= 0) point.Dir = (short)Math.Round(coords.Heading.Value);

			var battery = location.Battery;
			if (battery != null)
			{
				if (battery.Level >= 0) point.BatteryLevel = battery.Level;
				point.BatteryIsCharging = battery.IsCharging ?? battery.IsChargingCamel;
			}

			if (location.Activity is { Confidence: > 0 } activity) point.MotionActivity = mapActivityType(activity.Type);

			return point;
		}

		// Mirrors the accuracy-radius (meters) buckets a device's location fix is graded into.
		private static byte mapLocationAccuracy(float? accuracyMeters)
		{
			if (accuracyMeters == null) return GPSLocationAccuracy.Bad;
			if (accuracyMeters < 5) return GPSLocationAccuracy.Fine;
			if (accuracyMeters < 20) return GPSLocationAccuracy.Fair;
			if (accuracyMeters < 50) return GPSLocationAccuracy.Poor;
			return GPSLocationAccuracy.Bad;
		}

		private static short? mapActivityType(string? activityType)
		{
			return activityType switch
			{
				"still" => MobileDeviceMotionActivity.Still,
				"walking" => MobileDeviceMotionActivity.Walking,
				"on_foot" => MobileDeviceMotionActivity.OnFoot,
				"running" => MobileDeviceMotionActivity.Running,
				"on_bicycle" => MobileDeviceMotionActivity.OnBicycle,
				"in_vehicle" => MobileDeviceMotionActivity.InVehicle,
				_ => null
			};
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			// TryComplete, not Complete: the host can call StopAsync on a hosted service more than
			// once during shutdown (observed via WebApplicationFactory's teardown) - Complete()
			// throws ChannelClosedException on a channel that's already completed, TryComplete()
			// just returns false.
			mChannel.Writer.TryComplete();
			await base.StopAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				await foreach (var command in mChannel.Reader.ReadAllAsync(stoppingToken))
				{
					try
					{
						switch (command.Type)
						{
							case CommandType.UpdateTrackPoints:
								await processUpdateTrackPoints(command, stoppingToken);
								break;
						}
					}
					catch (Exception ex)
					{
						mLogger.LogError(ex, "Error processing MapTrackService command {Type}", command.Type);
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private async Task processUpdateTrackPoints(Command command, CancellationToken cancellationToken)
		{
			if (command.TrackPoints.Count == 0) return;

			foreach (var trackPoint in command.TrackPoints)
			{
				mLogger.LogInformation("Track point for user {UserId}, device {DeviceId}:\n{TrackPointJson}",
					command.UserId, command.MobileGpsDeviceId, JsonSerializer.Serialize(trackPoint, mIndentedJsonOptions));
			}

			//using var scope = mScopeFactory.CreateScope();
			//var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

			//// The phone is a gps_device of provider Mobile; its current state lives in gps_device_map_state.
			//var device = await repository.GetByIdAsync<GpsDevice>(command.MobileGpsDeviceId, cancellationToken);
			//if (device == null || device.ProviderId != GpsDeviceProvider.Mobile)
			//{
			//	mLogger.LogWarning("Dropping {Count} track points: device {DeviceId} is not a mobile gps_device", command.TrackPoints.Count, command.MobileGpsDeviceId);
			//	return;
			//}
   //         MobileGpsDeviceTrackPoint latestMobileGpsDeviceTrackPoint = command.TrackPoints[0];

   //         foreach (var point in command.TrackPoints)
			//{
			//	if (latestMobileGpsDeviceTrackPoint.DeviceDate <= point.DeviceDate)
			//	{
			//		latestMobileGpsDeviceTrackPoint = point;
			//	}
			//	point.TeamId = command.TeamId;
			//	repository.Create(point);
			//}


			//var latest = latestMobileGpsDeviceTrackPoint;
			//await PhoneMapState.ApplyAsync(repository, device.Id, device.CompanyId, new PhoneFix(
			//	latest.Location, latest.Speed, latest.Dir, latest.MotionActivity, latest.BatteryLevel, latest.BatteryIsCharging,
			//	PhoneMapState.TrustedPositionAt(latest.DeviceDate, latest.ReceivedDate), latest.ReceivedDate), cancellationToken);

   //         await repository.SaveAsync(cancellationToken);
		}

		private static readonly JsonSerializerOptions mIndentedJsonOptions = new() { WriteIndented = true };
		private readonly Channel<Command> mChannel = Channel.CreateUnbounded<Command>();
		private readonly GeometryFactory mGeometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
		private readonly IServiceScopeFactory mScopeFactory;
		private readonly ILogger mLogger;
	}
}
