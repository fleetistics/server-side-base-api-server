using api_server.Controllers.MapData.Dto;
using db_model.Gps;
using db_model.Map;
using exs.Database.Commons.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        /// <summary>A heading less accurate than this (degrees) is ignored.</summary>
        private const float MaxHeadingAccuracy = 45;
        /// <summary>A motion activity the phone is less confident about than this (percent) is ignored.</summary>
        private const int MinActivityConfidence = 30;
        private const float KmhPerMs = 3.6f;

        private const float MovingAboveKmh = 3f;
        private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

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
            public List<MobileLocation?> TrackPoints { get; set; } = default!;
        }

        public MobileGpsDeviceMapTrackService(IServiceScopeFactory scopeFactory, ILogger<MobileGpsDeviceMapTrackService> logger)
        {
            mScopeFactory = scopeFactory;
            mLogger = logger;
        }

        /// <summary>
        /// Queues a parsed location report for the background worker, which turns it into track points and updates the
        /// device's map state (see <see cref="processUpdateTrackPoints"/>).
        /// </summary>
        public async Task ParseAndUpdateDeviceLocationsAsync(int userId, int? companyId, int mobileGpsDeviceId, List<MobileLocation?> locations, CancellationToken cancellationToken)
        {
            if (locations.Count == 0) return;

            await mChannel.Writer.WriteAsync(new Command
            {
                Type = CommandType.UpdateTrackPoints,
                UserId = userId,
                MobileGpsDeviceId = mobileGpsDeviceId,
                CompanyId = companyId,
                ReceivedAt = DateTime.UtcNow,
                TrackPoints = locations
            }, cancellationToken);
            mLogger.LogInformation("Parsed and enqueued {Count} location points for user {UserId}", locations.Count, userId);
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

        /// <summary>
        /// Goes through the report oldest first; each location is decoded once, stored as a <see cref="MobileGpsDeviceTrackPoint"/>
        /// and applied to the device's current state right there, on top of what <see cref="GpsDeviceMapState"/> /
        /// <see cref="GpsDeviceStateData"/> already hold. A value the phone did not report or that is not acceptable (bad accuracy,
        /// unknown speed or heading, low confidence ...) is ignored: it is not counted as reported and the stored value is kept.
        /// An item that is not newer than the stored value (a late batch) does not replace it.
        /// </summary>
        private async Task processUpdateTrackPoints(Command command, CancellationToken cancellationToken)
        {
            if (command.TrackPoints.Count == 0) return;

            // Oldest first (OrderBy is stable), so a later acceptable value simply follows an earlier one. Items without a time
            // sort first and are skipped below.
            var locations = command.TrackPoints.OrderBy(l => l?.Timestamp ?? l.RecordedAt ?? DateTime.MinValue).ToList();

            using var scope = mScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
            var deviceId = command.MobileGpsDeviceId;

            var mapState = await repository.GetByIdAsync<GpsDeviceMapState>(deviceId, cancellationToken);
            var stateData = await repository.GetByIdAsync<GpsDeviceStateData>(deviceId, cancellationToken);
            
            short mapStateStatusId = GpsDeviceMapStatus.Visible;
            if (stateData == null)
            {
                //ToDo - load actual PrivateMode
                // A device that has not been on the map yet: only an active one gets its rows.
                var device = await repository.GetByIdAsync<GpsDevice>(deviceId, cancellationToken);
                if (device == null || device.StatusId != GpsDeviceStatus.Active)
                {
                    mLogger.LogWarning("Ignoring {Count} track points: gps device {DeviceId} {Reason}", locations.Count, deviceId, device == null ? "does not exist" : "is not active");
                    return;
                }
                stateData = new GpsDeviceStateData { GpsDeviceId = deviceId };
                repository.Create(stateData);
            }
            var parameters = DeviceStateData.Parse(stateData.Data);

            bool wasSomethingChanged = false;
            foreach (var location in locations)
            {
                if (location == null) continue;
                try
                {
                    var deviceDate = location.RecordedAt ?? location.Timestamp ?? DateTime.MinValue;
                    var locationDate = location.Timestamp;
                    Point locationPoint = mGeometryFactory.CreatePoint();
                    float? speedKmh = null;
                    short? dir = null;
                    double? altitude = null;
                    double? odometer = null;
                    var locationAccuracy = GPSLocationAccuracy.Bad;
                    if (location.Coords != null)
                    {
                        locationAccuracy = mapLocationAccuracy(location.Coords.Accuracy);
                        if (locationAccuracy != GPSLocationAccuracy.Bad && location.Coords.Latitude != null && location.Coords.Longitude != null && locationDate != null)
                        {
                            locationPoint = mGeometryFactory.CreatePoint(new Coordinate(location.Coords.Latitude.Value, location.Coords.Longitude.Value));

                            //device does not send speed, dir, altitude without GPS

                            // Speed needs a positive accuracy; -1 means "unknown".
                            speedKmh = location.Coords.SpeedAccuracy > 0 && location.Coords.Speed >= 0 ? location.Coords.Speed * KmhPerMs : null;
                            odometer = location.OdometerError < 50 && location.Odometer > 0 ? location.Odometer : null;
                            // Heading needs a positive accuracy of at most MaxHeadingAccuracy degrees; -1 means "unknown".
                            dir = location.Coords.HeadingAccuracy > 0 && location.Coords.HeadingAccuracy <= MaxHeadingAccuracy && location.Coords.Heading >= 0 ? (short)Math.Round(location.Coords.Heading.Value) : null;
                            altitude = location.Coords.Altitude != null && mapLocationAccuracy(location.Coords.AltitudeAccuracy) != GPSLocationAccuracy.Bad ? Math.Round((double)location.Coords.Altitude.Value, 2) : null;

                        }
                        else locationAccuracy = GPSLocationAccuracy.Bad;
                    }
                    short? phoneActivity = location.Activity is { Confidence: >= MinActivityConfidence } a ? mapActivityType(a.Type) : null;
                    float? battery = location.Battery is { Level: >= 0 } b ? b.Level : null;
                    bool? charging = location.Battery?.IsCharging ?? location.Battery?.IsChargingCamel; // null when the phone sent neither

                    // ---- the track point: columns and Data hold the acceptable values, RawData the item as received ----
                    var activityStatusId = ActivityStatusFor(speedKmh, phoneActivity);
                    var point = new MobileGpsDeviceTrackPoint
                    {
                        GpsDeviceId = deviceId,
                        CompanyId = command.CompanyId,
                        DeviceDate = deviceDate,
                        Location = locationPoint,
                        LocationAccuracy = locationAccuracy,
                        Speed = speedKmh,
                        Dir = dir,
                        ActivityStatusId = activityStatusId,
                        RawData = JsonSerializer.Serialize(location, mRawDataJsonOptions),
                        Data = new DeviceStateSnapshot
                        {
                            DeviceActivity = phoneActivity,
                            Battery = battery,
                            Charging = charging,
                            Altitude = altitude,
                            Odometer = odometer,
                        }.ToJson(),
                    };
                    repository.Create(point);
                    wasSomethingChanged = true;

                    // ---- the dated parameters: each moves forward on its own time, the changed time only when the value differs ----
                    stateData.LastCommunicationDeviceDate = deviceDate;
                    if (locationAccuracy != GPSLocationAccuracy.Bad) {
                        stateData.LastLocationDeviceDate = locationDate;
                        if (altitude != null) parameters.Altitude = StateValue<double>.Merge(parameters.Altitude, altitude.Value, locationDate!.Value);
                        if (speedKmh != null) parameters.Speed = StateValue<float>.Merge(parameters.Speed, speedKmh.Value, locationDate!.Value);
                        if (dir != null) parameters.Direction = StateValue<short>.Merge(parameters.Direction, dir.Value, locationDate!.Value);
                        if (mapState == null || mapState.LocationAccuracyId != GPSLocationAccuracy.Bad || !mapState.Location.Equals(locationPoint)) stateData.FirstLocationDeviceDate = locationDate;
                    }
                    if (odometer != null) parameters.Odometer = StateValue<double>.Merge(parameters.Odometer, odometer.Value, deviceDate);
                    if (battery != null) parameters.Battery = StateValue<float>.Merge(parameters.Battery, battery.Value, deviceDate);
                    if (charging != null) parameters.Charging = StateValue<bool>.Merge(parameters.Charging, charging.Value, deviceDate);
                    if (phoneActivity != null) parameters.DeviceActivity = StateValue<short>.Merge(parameters.DeviceActivity, phoneActivity.Value, deviceDate);
                    parameters.ActivityStatus = StateValue<short>.Merge(parameters.ActivityStatus, activityStatusId, deviceDate);

                    // ---- the map state ----
                    // WarningFlags.LowBattery is left alone: the phones' battery scale is not pinned down yet, the level itself goes to the data row.
                    if (mapState == null)
                    {
                        // A phone never seen on the map gets its row, every field, with its first acceptable position (Location is not nullable).
                        if (locationAccuracy != GPSLocationAccuracy.Bad)
                        {
                            mapState = new GpsDeviceMapState
                            {
                                GpsDeviceId = deviceId,
                                CompanyId = command.CompanyId,
                                Location = locationPoint,
                                LocationAccuracyId = locationAccuracy,
                                Speed = speedKmh,
                                Dir = dir,
                                ActivityStatusId = activityStatusId,
                                StatusId = mapStateStatusId
                            };
                            repository.Create(mapState);
                        }
                    }
                    else
                    {
                        mapState.CompanyId = command.CompanyId;
                        mapState.ActivityStatusId = activityStatusId;
                        if (locationAccuracy != GPSLocationAccuracy.Bad)
                        {
                            mapState.Location = locationPoint;
                            mapState.LocationAccuracyId = locationAccuracy;
                            mapState.Speed = speedKmh;
                            mapState.Dir = dir;
                            mapState.WarningFlags &= ~GpsDeviceWarning.StalePosition;
                        }
                    }
                }
                catch (Exception ex)
                {
                    mLogger.LogError(ex, "Error processing location item of device {DeviceId}", deviceId);
                }
            }
            if (wasSomethingChanged)
            {

                stateData.Data = parameters.ToJson();
                await repository.SaveAsync(cancellationToken);
            }
        }
        /// <summary>
        /// Moving above the speed threshold; otherwise Idle while the phone is in a vehicle (engine-on stop),
        /// else Parked. A phone has no ignition, so motion activity is the nearest signal. Offline is not set here.
        /// </summary>
        private static byte ActivityStatusFor(float? speedKmh, short? phoneActivity)
        {
            if (phoneActivity == null)
            {
                if (speedKmh != null)
                {
                    if (speedKmh > MovingAboveKmh) return GpsDeviceActivityStatus.Driving;
                    else return GpsDeviceActivityStatus.Stopped;
                }
                else return GpsDeviceActivityStatus.Unknown;
            }
            else
            {
                switch (phoneActivity)
                {
                    case MobileDeviceMotionActivity.InVehicle:
                        if (speedKmh != null && speedKmh > MovingAboveKmh) return GpsDeviceActivityStatus.Driving;
                        else return GpsDeviceActivityStatus.Idle;
                    case MobileDeviceMotionActivity.OnBicycle:
                        return GpsDeviceActivityStatus.OnBicycle;
                    case MobileDeviceMotionActivity.Running:
                        return GpsDeviceActivityStatus.Running;
                    case MobileDeviceMotionActivity.Walking:
                        return GpsDeviceActivityStatus.Walking;
                    default:
                        return GpsDeviceActivityStatus.Stopped;
                }
            }
        }
        private static readonly JsonSerializerOptions mRawDataJsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        private readonly Channel<Command> mChannel = Channel.CreateUnbounded<Command>();
        private readonly GeometryFactory mGeometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        private readonly IServiceScopeFactory mScopeFactory;
        private readonly ILogger mLogger;
    }
}
