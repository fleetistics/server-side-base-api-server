using System.Text.Json;
using System.Text.Json.Serialization;

namespace db_model.Map
{
    /// <summary>
    /// <see cref="DeviceStateData"/> without the dates: the same keys (<see cref="StateKeys"/>), but each parameter is just its
    /// value, not <c>{"v","t","c"}</c>. It is what a history row (<see cref="MobileGpsDeviceTrackPoint.Data"/>) stores, where the
    /// row's own times already say when the values were reported. Every property is null when the device did not report it,
    /// and null properties are not written.
    /// </summary>
    public sealed class DeviceStateSnapshot
    {
        [JsonPropertyName(StateKeys.Ignition)]
        public double? Ignition { get; set; }

        [JsonPropertyName(StateKeys.BatteryVoltage)]
        public double? BatteryVoltage { get; set; }

        /// <summary>Phone battery level, as the phone reports it.</summary>
        [JsonPropertyName(StateKeys.Battery)]
        public float? Battery { get; set; }

        /// <summary>Phone is charging.</summary>
        [JsonPropertyName(StateKeys.Charging)]
        public bool? Charging { get; set; }

        /// <summary>Phone motion activity (<see cref="MobileDeviceMotionActivity"/>).</summary>
        [JsonPropertyName(StateKeys.DeviceActivity)]
        public short? DeviceActivity { get; set; }

        /// <summary><see cref="GpsDeviceMapState.ActivityStatusId"/> at the time of the row.</summary>
        [JsonPropertyName(StateKeys.ActivityStatus)]
        public short? ActivityStatus { get; set; }

        [JsonPropertyName(StateKeys.PrivateMode)]
        public bool? PrivateMode { get; set; }

        [JsonPropertyName(StateKeys.EngineFault)]
        public bool? EngineFault { get; set; }

        [JsonPropertyName(StateKeys.BatteryLow)]
        public bool? BatteryLow { get; set; }

        [JsonPropertyName(StateKeys.BatteryFault)]
        public bool? BatteryFault { get; set; }

        [JsonPropertyName(StateKeys.Odometer)]
        public double? Odometer { get; set; }

        [JsonPropertyName(StateKeys.EngineHours)]
        public double? EngineHours { get; set; }

        [JsonPropertyName(StateKeys.OdometerAdjustment)]
        public double? OdometerAdjustment { get; set; }

        [JsonPropertyName(StateKeys.EngineHoursAdjustment)]
        public double? EngineHoursAdjustment { get; set; }

        [JsonPropertyName(StateKeys.FuelLevel)]
        public double? FuelLevel { get; set; }

        [JsonPropertyName(StateKeys.FuelTotal)]
        public double? FuelTotal { get; set; }

        [JsonPropertyName(StateKeys.FuelIdleTotal)]
        public double? FuelIdleTotal { get; set; }

        [JsonPropertyName(StateKeys.SeatBelt)]
        public double? SeatBelt { get; set; }

        /// <summary>Altitude of the position, in metres above sea level.</summary>
        [JsonPropertyName(StateKeys.Altitude)]
        public double? Altitude { get; set; }

        private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        /// <summary>The snapshot of a full state: the current value of every parameter, dates dropped.</summary>
        public static DeviceStateSnapshot From(DeviceStateData data) => new()
        {
            Ignition = data.Ignition?.Value,
            BatteryVoltage = data.BatteryVoltage?.Value,
            Battery = data.Battery?.Value,
            Charging = data.Charging?.Value,
            Activity = data.Activity?.Value,
            ActivityStatus = data.ActivityStatus?.Value,
            PrivateMode = data.PrivateMode?.Value,
            EngineFault = data.EngineFault?.Value,
            BatteryLow = data.BatteryLow?.Value,
            BatteryFault = data.BatteryFault?.Value,
            Odometer = data.Odometer?.Value,
            EngineHours = data.EngineHours?.Value,
            OdometerAdjustment = data.OdometerAdjustment?.Value,
            EngineHoursAdjustment = data.EngineHoursAdjustment?.Value,
            FuelLevel = data.FuelLevel?.Value,
            FuelTotal = data.FuelTotal?.Value,
            FuelIdleTotal = data.FuelIdleTotal?.Value,
            SeatBelt = data.SeatBelt?.Value,
            Altitude = data.Altitude?.Value,
        };

        /// <summary>Reads the stored JSON. Blank, malformed or not an object gives an empty snapshot; nothing throws.</summary>
        public static DeviceStateSnapshot Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new DeviceStateSnapshot();
            try { return JsonSerializer.Deserialize<DeviceStateSnapshot>(json, Options) ?? new DeviceStateSnapshot(); }
            catch (JsonException) { return new DeviceStateSnapshot(); }
        }

        /// <summary>The JSON to store: the parameters that have a value.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, Options);
    }
}
