using System.Text.Json.Serialization;

namespace db_model.Map
{
    /// <summary>
    /// One parameter of <see cref="DeviceStateData"/> as stored in <see cref="GpsDeviceStateData.Data"/>:
    /// <c>{"v": value, "t": reported time, "c": changed time}</c>. Every parameter carries its own dates, so a
    /// batch with values of very different ages never lets an old value replace a newer one.
    /// </summary>
    public sealed class StateValue<T>
    {
        [JsonPropertyName("v")]
        public T Value { get; set; } = default!;

        /// <summary>When the device reported this value (source time, UTC). Orders writes: only a newer one replaces it.</summary>
        [JsonPropertyName("t")]
        public DateTime At { get; set; }

        /// <summary>
        /// When the value last became different from the one before ("parked since 14:00"). Null in entries written before
        /// this field existed; <see cref="Since"/> then falls back to <see cref="At"/>.
        /// </summary>
        [JsonPropertyName("c")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? ChangedAt { get; set; }

        [JsonIgnore]
        public DateTime Since => ChangedAt ?? At;

        public StateValue() { }

        public StateValue(T value, DateTime at, DateTime? changedAt = null)
        {
            Value = value;
            At = at;
            ChangedAt = changedAt;
        }
    }
}
