namespace db_model.Map
{
    /// <summary>Problems a map icon should show, as bit flags in <see cref="GpsDeviceMapState.WarningFlags"/>.</summary>
    [Flags]
    public enum GpsDeviceWarning : short
    {
        None = 0,
        LowBattery = 1,
        EngineFault = 2,
        /// <summary>The newest position is too old to trust; derived from its age, not from how accurate it was.</summary>
        StalePosition = 4,
    }
}
