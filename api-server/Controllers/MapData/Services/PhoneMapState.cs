using db_model.Map;
using exs.Database.Commons.Interfaces;
using NetTopologySuite.Geometries;

namespace api_server.Controllers.MapData.Services
{
	/// <summary>The newest fix of a phone, as the map state needs it. Speed is in m/s, as the phone reports it.</summary>
	public sealed record PhoneFix(
		Point Location, float? SpeedMs, short? Dir, short? MotionActivity,
		float? BatteryLevel, bool? BatteryIsCharging, DateTime PositionAt, DateTime ReceivedAt,
		short LocationAccuracyId = GPSLocationAccuracy.Fine);

	/// <summary>
	/// Writes a phone's current state into <see cref="GpsDeviceMapState"/> / <see cref="GpsDeviceStateData"/>, the same
	/// tables the feeders fill for trackers, so one reader serves both. Does not save: the caller owns the unit of work.
	/// </summary>
	public static class PhoneMapState
	{
		/// <summary>The map table keeps km/h (what the feeders store); phones report m/s.</summary>
		public const float KmhPerMs = 3.6f;
		/// <summary>Same threshold as the feeders' status rule: above it the vehicle is Moving.</summary>
		public const float MovingAboveKmh = 3f;
		/// <summary>A phone clock further ahead than this is not trusted (see <see cref="TrustedPositionAt"/>).</summary>
		public static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

		public const string BatteryKey = StateKeys.Battery;
		public const string ChargingKey = StateKeys.Charging;
		public const string ActivityKey = StateKeys.Activity;

		/// <summary>
		/// Moving above the speed threshold; otherwise Idle while the phone is in a vehicle (engine-on stop),
		/// else Parked. A phone has no ignition, so motion activity is the nearest signal. Offline is not set here.
		/// </summary>
		public static byte StatusFor(float? speedKmh, short? motionActivity)
		{
			if (speedKmh is > MovingAboveKmh) return GpsDeviceActivityStatus.Moving;
			if (motionActivity == MobileDeviceMotionActivity.InVehicle) return GpsDeviceActivityStatus.Idle;
			return GpsDeviceActivityStatus.Parked;
		}

		/// <summary>
		/// The position time to use: the phone's own, unless it is ahead of the server by more than the tolerance.
		/// Without this one fix with a far-future time would make every later (real) fix look older and freeze the map.
		/// </summary>
		public static DateTime TrustedPositionAt(DateTime deviceDate, DateTime receivedAt) =>
			deviceDate > receivedAt + ClockSkewTolerance ? receivedAt : deviceDate;

		/// <summary>
		/// Applies the fix unless the stored position is as new or newer (a late batch must not move the phone back).
		/// A fix also ends StalePosition: the status is recomputed from the fix. Returns whether anything was written.
		/// </summary>
		public static async Task<bool> ApplyAsync(IRepository repository, int deviceId, int? companyId, PhoneFix fix, CancellationToken cancellationToken)
		{
			var state = await repository.GetByIdAsync<GpsDeviceMapState>(deviceId, cancellationToken);
			if (state != null && fix.PositionAt <= state.LastPositionAt) return false;

			var speedKmh = fix.SpeedMs * KmhPerMs;
			var status = StatusFor(speedKmh, fix.MotionActivity);
			if (state == null)
			{
				state = new GpsDeviceMapState { GpsDeviceId = deviceId };
				repository.Create(state);
			}

			state.CompanyId = companyId;
			state.Location = fix.Location;
			state.Speed = speedKmh;
			state.Dir = fix.Dir;
			state.ActivityStatusId = status;
			state.LocationAccuracyId = fix.LocationAccuracyId;
			state.LastPositionAt = fix.PositionAt;
			// WarningFlags.LowBattery is left alone: the phones' battery scale is not pinned down yet, the level itself goes to the data row.
			state.LatestOnMapUpdate = fix.ReceivedAt;

			// The dated parameters. The activity status follows the column just written (forced); the rest only move forward in time.
			var data = await repository.GetByIdAsync<GpsDeviceStateData>(deviceId, cancellationToken);
			if (data == null)
			{
				data = new GpsDeviceStateData { GpsDeviceId = deviceId };
				repository.Create(data);
			}
			var parameters = DeviceStateData.Parse(data.Data);
			var at = fix.PositionAt;
			parameters.ActivityStatus = StateValue<short>.Set(parameters.ActivityStatus, status, at);
			parameters.Communication = StateValue<bool>.Merge(parameters.Communication, true, at);
			if (fix.BatteryLevel != null) parameters.Battery = StateValue<float>.Merge(parameters.Battery, fix.BatteryLevel.Value, at);
			if (fix.BatteryIsCharging != null) parameters.Charging = StateValue<bool>.Merge(parameters.Charging, fix.BatteryIsCharging.Value, at);
			if (fix.MotionActivity != null) parameters.Activity = StateValue<short>.Merge(parameters.Activity, fix.MotionActivity.Value, at);
			data.Data = parameters.ToJson();
			data.LatestOnMapUpdate = fix.ReceivedAt;
			return true;
		}

		/// <summary>The phone values kept in the state data: motion activity and battery level (null when absent).</summary>
		public static (short? MotionActivity, float? BatteryLevel) ReadValues(string? stateData)
		{
			var parameters = DeviceStateData.Parse(stateData);
			return (parameters.Activity?.Value, parameters.Battery?.Value);
		}
	}
}
