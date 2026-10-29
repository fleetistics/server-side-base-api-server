namespace db_model.Map
{
    /// <summary>Whether the map shows a device: <see cref="GpsDeviceMapState.StatusId"/>. More values may follow.</summary>
    public class GpsDeviceMapStatus
    {
        public const byte Invisible = 0;
        public const byte Visible = 1;

        public byte Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
