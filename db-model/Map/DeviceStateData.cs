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

        /// <summary>Parameters this version does not know, kept verbatim.</summary>
        [JsonIgnore]
        public Dictionary<string, JsonNode?>? Extra { get; set; }

        [JsonIgnore]
        public bool? IsIgnitionOn => Ignition == null ? null : Ignition.Value > 0.5;

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
