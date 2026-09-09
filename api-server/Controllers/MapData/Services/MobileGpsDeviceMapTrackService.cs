using db_model.Map;
using db_model.UserManagement;
using exs.Commons.Utils;
using exs.Database.Commons.Interfaces;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Text.Json.Nodes;
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
			public List<MobileGpsDeviceTrackPoint> TrackPoints { get; set; } = default!;
		}

		public MobileGpsDeviceMapTrackService(IServiceScopeFactory scopeFactory, ILogger<MobileGpsDeviceMapTrackService> logger)
		{
			mScopeFactory = scopeFactory;
			mLogger = logger;
		}

		public async Task ParseAndUpdateDeviceLocationsAsync(int userId, int mobileGpsDeviceId, JsonArray jLocations, CancellationToken cancellationToken)
		{
			var points = new List<MobileGpsDeviceTrackPoint>(jLocations.Count);
			var now = DateTime.UtcNow;
			foreach (var jItem in jLocations)
			{
				try
				{
					if (jItem is JsonArray jArray)
					{
						// [0]=lat, [1]=lon, [2]=accuracy(m), [3]=speed, [4]=heading, [5]=activity, [6]=timestamp, [7]=altitude, [8]=battery level, [9]=battery charging
						if (jArray.Count < 7) continue;
						var latitude = jArray[0]?.GetValue<double?>();
						var longitude = jArray[1]?.GetValue<double?>();
						if (latitude == null || longitude == null) continue;
						var jDt = jArray[6]?.AsValue();
						if (jDt == null) continue;
						DateTime deviceDate;
						if (jDt.TryGetValue<DateTime>(out var dt)) deviceDate = dt;
						else if (jDt.TryGetValue<Int64>(out var l)) deviceDate = DateTimeHelper.UnixTimeToUtcDateTime(l);
						else if (jDt.TryGetValue<Int32>(out var i)) deviceDate = DateTimeHelper.UnixTimeToUtcDateTime(i);
						else continue;

						var accuracy = jArray[2]?.GetValue<float?>();

						var point = new MobileGpsDeviceTrackPoint
						{
                            MobileGpsDeviceId = mobileGpsDeviceId,
                            ReceivedDate = now,
							DeviceDate = deviceDate,
							Location = mGeometryFactory.CreatePoint(new Coordinate(longitude.Value, latitude.Value)),
							Speed = jArray[3]?.GetValue<float?>(),
							LocationAccuracy = mapLocationAccuracy(accuracy),
							BatteryLevel = jArray.Count > 8 ? jArray[8]?.GetValue<float?>() : null,
							BatteryIsCharging = jArray.Count > 9 ? jArray[9]?.GetValue<bool?>() : null,
							MotionActivity = mapActivityType(jArray[5]?.GetValue<string>())
						};
						if (point.Speed < 0) point.Speed = null;

						var jDir = jArray[4]?.AsValue();
						if (jDir != null)
						{
							if (jDir.TryGetValue<int>(out var iDir) && iDir >= 0) point.Dir = (short)iDir;
							else if (jDir.TryGetValue<float>(out var fDir) && fDir >= 0) point.Dir = (short)Math.Round(fDir);
						}

						points.Add(point);
					}
					else if (jItem is JsonObject jLoc)
					{
						var jCoords = jLoc["coords"]?.AsObject();
						if (jCoords == null) continue;

						var latitude = jCoords["latitude"]?.GetValue<double?>();
						var longitude = jCoords["longitude"]?.GetValue<double?>();
						if (latitude == null || longitude == null) continue;

						var jDt = jLoc["timestamp"]?.AsValue();
						if (jDt == null) continue;
						DateTime deviceDate;
						if (jDt.TryGetValue<DateTime>(out var dt)) deviceDate = dt;
						else if (jDt.TryGetValue<Int64>(out var l)) deviceDate = DateTimeHelper.UnixTimeToUtcDateTime(l);
						else if (jDt.TryGetValue<Int32>(out var i)) deviceDate = DateTimeHelper.UnixTimeToUtcDateTime(i);
						else continue;

						var accuracy = jCoords["accuracy"]?.GetValue<float?>();

						var point = new MobileGpsDeviceTrackPoint
						{
                            MobileGpsDeviceId = mobileGpsDeviceId,
                            ReceivedDate = now,
							DeviceDate = deviceDate,
							Location = mGeometryFactory.CreatePoint(new Coordinate(longitude.Value, latitude.Value)),
							LocationAccuracy = mapLocationAccuracy(accuracy),
						};

						var jSpeedAcc = jCoords["speed_accuracy"]?.AsValue();
						if (jSpeedAcc != null)
						{
							float speed_accuracy = 0f;
							if (jSpeedAcc.TryGetValue<float>(out var f)) speed_accuracy = f;
							else if (jSpeedAcc.TryGetValue<int>(out var i)) speed_accuracy = i;
							if (speed_accuracy > 0)
							{
								var jSpeed = jCoords["speed"]?.AsValue();
								if (jSpeed != null)
								{
									float speed = 0f;
									if (jSpeed.TryGetValue<float>(out var fs)) speed = fs;
									else if (jSpeed.TryGetValue<int>(out var i)) speed = i;
									if (speed >= 0) point.Speed = speed;
								}
							}
						}

						var jDirAcc = jCoords["heading_accuracy"]?.AsValue();
						if (jDirAcc != null)
						{
							float dirAcc = 0f;
							if (jDirAcc.TryGetValue<float>(out var f)) dirAcc = f;
							else if (jDirAcc.TryGetValue<int>(out var i)) dirAcc = i;
							if (dirAcc > 0)
							{
								var jDir = jCoords["heading"]?.AsValue();
								if (jDir != null)
								{
									float dir = 0f;
									if (jDir.TryGetValue<float>(out var fd)) dir = fd;
									else if (jDir.TryGetValue<int>(out var i)) dir = i;
									if (dir >= 0) point.Dir = (short)Math.Round(dir);
								}
							}
						}

						var jBat = jLoc["battery"]?.AsObject();
						if (jBat != null)
						{
							var batLevel = jBat["level"]?.GetValue<float?>();
							if (batLevel >= 0) point.BatteryLevel = batLevel;
							var batCharging = jBat["isCharging"]?.GetValue<bool?>();
							if (batCharging != null) point.BatteryIsCharging = batCharging;
						}

						var jAct = jLoc["activity"]?.AsObject();
						if (jAct != null)
						{
							var actConf = jAct["confidence"]?.GetValue<byte?>();
							if (actConf > 0)
							{
								var actType = jAct["type"]?.GetValue<string>();
								point.MotionActivity = mapActivityType(actType);
							}
						}

						points.Add(point);
					}
				}
				catch (Exception ex)
				{
					mLogger.LogError(ex, "Error parsing location item: {Item}", jItem);
				}
			}
			if (points.Count > 0)
			{
				await mChannel.Writer.WriteAsync(new Command { Type = CommandType.UpdateTrackPoints, TrackPoints = points, UserId = userId, MobileGpsDeviceId = mobileGpsDeviceId }, cancellationToken);
				mLogger.LogInformation("Parsed and enqueued {Count} location points for user {UserId}", points.Count, userId);
			}
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
			mChannel.Writer.Complete();
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

			using var scope = mScopeFactory.CreateScope();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

			var mapState = await repository.GetByIdAsync<MobileGpsDeviceMapState>(command.MobileGpsDeviceId, cancellationToken);
			if (mapState == null )
			{
                mapState = new MobileGpsDeviceMapState { 
					MobileGpsDeviceId = command.MobileGpsDeviceId,
                    UserId = command.UserId
                };
                repository.Create(mapState);

            }
            MobileGpsDeviceTrackPoint latestMobileGpsDeviceTrackPoint = command.TrackPoints[0];

            foreach (var point in command.TrackPoints)
			{
				if (latestMobileGpsDeviceTrackPoint.DeviceDate <= point.DeviceDate)
				{
					latestMobileGpsDeviceTrackPoint = point;
				}
				repository.Create(point);
			}


			mapState.Location = latestMobileGpsDeviceTrackPoint.Location;
			mapState.MotionActivity = latestMobileGpsDeviceTrackPoint.MotionActivity;
			mapState.Speed = latestMobileGpsDeviceTrackPoint.Speed;
			mapState.Dir = latestMobileGpsDeviceTrackPoint.Dir;
			mapState.BatteryLevel = latestMobileGpsDeviceTrackPoint.BatteryLevel;
			mapState.LatestOnMapUpdate = latestMobileGpsDeviceTrackPoint.ReceivedDate;


            await repository.SaveAsync(cancellationToken);
		}

		private readonly Channel<Command> mChannel = Channel.CreateUnbounded<Command>();
		private readonly GeometryFactory mGeometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
		private readonly IServiceScopeFactory mScopeFactory;
		private readonly ILogger mLogger;
	}
}
