namespace db_model.Map
{
    /// <summary>Problems a map icon should show, as bit flags in <see cref="GpsDeviceMapState.WarningFlags"/>.</summary>
    [Flags]
    public enum GpsDeviceWarning : short
    {
        None = 0,
        LowBattery = 1,
        EngineFault = 2,
        /// <summary>
        /// The device sent no position for the configured time. Set together with the activity status
        /// <see cref="GpsDeviceActivityStatus.StalePosition"/> by the feeders' sweep, and cleared by the next position.
        /// </summary>
        StalePosition = 4,
    }
}
