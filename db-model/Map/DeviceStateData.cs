using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace db_model.Map
{
    /// <summary>The names of the parameters inside <see cref="GpsDeviceStateData.Data"/>.</summary>
    public static class StateKeys
    {
        public const string Ignition = "ignition";
        public const string BatteryVoltage = "batteryVoltage";
        public const string Battery = "battery";
        public const string Charging = "charging";
        public const string Activity = "activity";
        public const string ActivityStatus = "activityStatus";
        public const string Communication = "communication";
        public const string PrivateMode = "privateMode";
        public const string EngineFault = "engineFault";
        public const string BatteryLow = "batteryLow";
        public const string BatteryFault = "batteryFault";
        public const string Odometer = "odometer";
        public const string EngineHours = "engineHours";
        public const string OdometerAdjustment = "odometerAdjustment";
        public const string EngineHoursAdjustment = "engineHoursAdjustment";
        public const string FuelLevel = "fuelLevel";
        public const string FuelTotal = "fuelTotal";
        public const string FuelIdleTotal = "fuelIdleTotal";
        public const string SeatBelt = "seatBelt";
    }

    /// <summary>
    /// Everything that can be in <see cref="GpsDeviceStateData.Data"/>, typed. Every property is null when the device never
    /// reported it. Feeders fill only the parameters they have and write them as a partial object; the API (and from it the
    /// TypeScript client) reads the whole object. A key this version does not know, or an entry of the wrong shape, is not
    /// an error: it is kept in <see cref="Extra"/> and written back unchanged.
    /// </summary>
    public sealed class DeviceStateData
    {
        /// <summary>Ignition as the tracker reports it: above 0.5 is on (see <see cref="IsIgnitionOn"/>).</summary>
        [JsonPropertyName(StateKeys.Ignition)]
        public StateValue<double>? Ignition { get; set; }

        /// <summary>Vehicle battery voltage, in volts.</summary>
        [JsonPropertyName(StateKeys.BatteryVoltage)]
        public StateValue<double>? BatteryVoltage { get; set; }

        /// <summary>Phone battery level, as the phone reports it.</summary>
        [JsonPropertyName(StateKeys.Battery)]
        public StateValue<float>? Battery { get; set; }

        /// <summary>Phone is charging.</summary>
        [JsonPropertyName(StateKeys.Charging)]
        public StateValue<bool>? Charging { get; set; }

        /// <summary>Phone motion activity (<see cref="MobileDeviceMotionActivity"/>); not the map's activity status.</summary>
        [JsonPropertyName(StateKeys.Activity)]
        public StateValue<short>? Activity { get; set; }

        /// <summary>
        /// <see cref="GpsDeviceMapState.ActivityStatusId"/> with its dates: <see cref="StateValue{T}.ChangedAt"/> is "parked
        /// since" (replaces the removed StatusChangedAt column).
        /// </summary>
        [JsonPropertyName(StateKeys.ActivityStatus)]
        public StateValue<short>? ActivityStatus { get; set; }

        /// <summary>
        /// Whether the device is communicating: <see cref="StateValue{T}.At"/> is the newest record of any kind (replaces the
        /// removed LastCommunicationAt column), <see cref="StateValue{T}.ChangedAt"/> since when it has been (not) communicating.
        /// </summary>
        [JsonPropertyName(StateKeys.Communication)]
        public StateValue<bool>? Communication { get; set; }

        /// <summary>
        /// The device is in the provider's private (personal) mode: <see cref="StateValue{T}.ChangedAt"/> is since when. While it is
        /// true the map state is invisible (<see cref="GpsDeviceMapState.StatusId"/>).
        /// </summary>
        [JsonPropertyName(StateKeys.PrivateMode)]
        public StateValue<bool>? PrivateMode { get; set; }

        /// <summary>
        /// An engine warning light is on: <see cref="StateValue{T}.At"/> is the newest active fault record and
        /// <see cref="StateValue{T}.ChangedAt"/> since when. The map state's EngineFault warning follows it.
        /// </summary>
        [JsonPropertyName(StateKeys.EngineFault)]
        public StateValue<bool>? EngineFault { get; set; }

        /// <summary>The voltage reading says the battery is low (below the configured threshold). The LowBattery warning is this OR <see cref="BatteryFault"/>.</summary>
        [JsonPropertyName(StateKeys.BatteryLow)]
        public StateValue<bool>? BatteryLow { get; set; }

        /// <summary>The provider's own "battery has low voltage" fault is active (set by an Active record, ended by an Inactive or Cleared one).</summary>
        [JsonPropertyName(StateKeys.BatteryFault)]
        public StateValue<bool>? BatteryFault { get; set; }

        /// <summary>Odometer as the vehicle reports it (Geotab: <c>DiagnosticOdometerId</c>, metres on the checked account); the unit is the provider's, not converted here. See <see cref="OdometerAdjustment"/>.</summary>
        [JsonPropertyName(StateKeys.Odometer)]
        public StateValue<double>? Odometer { get; set; }

        /// <summary>Engine hours as the vehicle reports them (Geotab: <c>DiagnosticEngineHoursId</c>, seconds); the unit is the provider's, not converted here. Many vehicles report none. See <see cref="EngineHoursAdjustment"/>.</summary>
        [JsonPropertyName(StateKeys.EngineHours)]
        public StateValue<double>? EngineHours { get; set; }

        /// <summary>
        /// A user's correction of the odometer (Geotab: <c>DiagnosticOdometerAdjustmentId</c>, metres). It exists only where someone adjusted the odometer: on the checked
        /// account there were none. How it combines with <see cref="Odometer"/> is the provider's rule and is not applied here.
        /// </summary>
        [JsonPropertyName(StateKeys.OdometerAdjustment)]
        public StateValue<double>? OdometerAdjustment { get; set; }

        /// <summary>A user's correction of the engine hours (Geotab: <c>DiagnosticEngineHoursAdjustmentId</c>, seconds); not applied to <see cref="EngineHours"/> here.</summary>
        [JsonPropertyName(StateKeys.EngineHoursAdjustment)]
        public StateValue<double>? EngineHoursAdjustment { get; set; }

        /// <summary>Fuel level in percent of the tank (Geotab: <c>DiagnosticFuelLevelId</c>).</summary>
        [JsonPropertyName(StateKeys.FuelLevel)]
        public StateValue<double>? FuelLevel { get; set; }

        /// <summary>Cumulative fuel used by the device in its lifetime (Geotab: <c>DiagnosticDeviceTotalFuelId</c>). Fuel used between two times is the difference of two readings.</summary>
        [JsonPropertyName(StateKeys.FuelTotal)]
        public StateValue<double>? FuelTotal { get; set; }

        /// <summary>Cumulative fuel used while idling (Geotab: <c>DiagnosticDeviceTotalIdleFuelId</c>).</summary>
        [JsonPropertyName(StateKeys.FuelIdleTotal)]
        public StateValue<double>? FuelIdleTotal { get; set; }

        /// <summary>Driver seat belt as the provider reports it: above 0 is unbuckled, 0 buckled, below 0 unavailable (Geotab: <c>DiagnosticDriverSeatbeltId</c>). See <see cref="IsSeatBeltUnbuckled"/>.</summary>
        [JsonPropertyName(StateKeys.SeatBelt)]
        public StateValue<double>? SeatBelt { get; set; }

        /// <summary>Parameters this version does not know, kept verbatim.</summary>
        [JsonIgnore]
        public Dictionary<string, JsonNode?>? Extra { get; set; }

        [JsonIgnore]
        public bool? IsIgnitionOn => Ignition == null ? null : Ignition.Value > 0.5;

        /// <summary>True when the driver's seat belt is unbuckled, false when buckled, null when it is unknown (never reported, or the provider says unavailable).</summary>
        [JsonIgnore]
        public bool? IsSeatBeltUnbuckled => SeatBelt == null || SeatBelt.Value < 0 ? null : SeatBelt.Value > 0;

        private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        /// <summary>Reads the stored JSON. Blank, malformed or not an object gives an empty object; nothing throws.</summary>
        public static DeviceStateData Parse(string? json)
        {
            var data = new DeviceStateData();
            if (string.IsNullOrWhiteSpace(json)) return data;

            JsonObject? root;
            try { root = JsonNode.Parse(json) as JsonObject; }
            catch (JsonException) { return data; }
            if (root == null) return data;

            foreach (var (key, node) in root)
            {
                if (!read(data, key, node)) (data.Extra ??= new())[key] = node?.DeepClone();
            }
            return data;
        }

        /// <summary>The JSON to store or send: known parameters that have a value, plus <see cref="Extra"/>.</summary>
        public string ToJson()
        {
            var root = new JsonObject();
            if (Extra != null)
            {
                foreach (var (key, node) in Extra) root[key] = node?.DeepClone();
            }
            var known = JsonSerializer.SerializeToNode(this, Options) as JsonObject;
            if (known != null)
            {
                foreach (var (key, node) in known.ToList())
                {
                    known.Remove(key);
                    root[key] = node;
                }
            }
            return root.ToJsonString();
        }

        private static bool read(DeviceStateData data, string key, JsonNode? node)
        {
            switch (key)
            {
                case StateKeys.Ignition: return tryRead<double>(node, v => data.Ignition = v);
                case StateKeys.BatteryVoltage: return tryRead<double>(node, v => data.BatteryVoltage = v);
                case StateKeys.Battery: return tryRead<float>(node, v => data.Battery = v);
                case StateKeys.Charging: return tryRead<bool>(node, v => data.Charging = v);
                case StateKeys.Activity: return tryRead<short>(node, v => data.Activity = v);
                case StateKeys.ActivityStatus: return tryRead<short>(node, v => data.ActivityStatus = v);
                case StateKeys.Communication: return tryRead<bool>(node, v => data.Communication = v);
                case StateKeys.PrivateMode: return tryRead<bool>(node, v => data.PrivateMode = v);
                case StateKeys.EngineFault: return tryRead<bool>(node, v => data.EngineFault = v);
                case StateKeys.BatteryLow: return tryRead<bool>(node, v => data.BatteryLow = v);
                case StateKeys.BatteryFault: return tryRead<bool>(node, v => data.BatteryFault = v);
                case StateKeys.Odometer: return tryRead<double>(node, v => data.Odometer = v);
                case StateKeys.EngineHours: return tryRead<double>(node, v => data.EngineHours = v);
                case StateKeys.OdometerAdjustment: return tryRead<double>(node, v => data.OdometerAdjustment = v);
                case StateKeys.EngineHoursAdjustment: return tryRead<double>(node, v => data.EngineHoursAdjustment = v);
                case StateKeys.FuelLevel: return tryRead<double>(node, v => data.FuelLevel = v);
                case StateKeys.FuelTotal: return tryRead<double>(node, v => data.FuelTotal = v);
                case StateKeys.FuelIdleTotal: return tryRead<double>(node, v => data.FuelIdleTotal = v);
                case StateKeys.SeatBelt: return tryRead<double>(node, v => data.SeatBelt = v);
                default: return false;
            }
        }

        private static bool tryRead<T>(JsonNode? node, Action<StateValue<T>> assign)
        {
            if (node is not JsonObject) return false;
            try
            {
                var value = node.Deserialize<StateValue<T>>();
                if (value == null) return false;
                assign(value);
                return true;
            }
            catch (JsonException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (FormatException) { return false; }
        }
    }
}
