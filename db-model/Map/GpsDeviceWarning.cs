namespace db_model.Map
{
    /// <summary>Problems a map icon should show, as bit flags in <see cref="GpsDeviceMapState.WarningFlags"/>.</summary>
    [Flags]
    public enum GpsDeviceWarning : short
    {
        None = 0,
        LowBattery = 1,
        EngineFault = 2,
        /// <summary>Not used: a position that is too old is the activity status <see cref="GpsDeviceActivityStatus.StalePosition"/>, set by the feeders' sweep.</summary>
        StalePosition = 4,
    }
}
