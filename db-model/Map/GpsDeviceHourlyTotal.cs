namespace db_model.Map
{
    /// <summary>
    /// The cumulative readings of a device as they stood at the end of one hour (UTC): the last reading seen in that hour, each with
    /// its own time. Fuel used, distance and idle share between two hour-aligned times are differences of two such rows, so the
    /// readings are the history and the consumption is computed, never stored. See <c>FuelEconomyService</c>.
    /// </summary>
    public class GpsDeviceHourlyTotal
    {
        public int GpsDeviceId { get; set; }

        /// <summary>The start of the hour, UTC.</summary>
        public DateTime HourUtc { get; set; }

        /// <summary>Odometer in metres, as the vehicle reports it.</summary>
        public double? Odometer { get; set; }
        public DateTime? OdometerAt { get; set; }

        /// <summary>Engine operational time in seconds.</summary>
        public double? EngineHours { get; set; }
        public DateTime? EngineHoursAt { get; set; }

        /// <summary>Fuel used since the device was installed, litres.</summary>
        public double? FuelTotal { get; set; }
        public DateTime? FuelTotalAt { get; set; }

        /// <summary>Fuel used while idling since the device was installed, litres.</summary>
        public double? FuelIdleTotal { get; set; }
        public DateTime? FuelIdleTotalAt { get; set; }
    }
}
