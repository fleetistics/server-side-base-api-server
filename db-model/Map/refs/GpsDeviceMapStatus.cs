namespace db_model.Map
{
    /// <summary>Whether the map shows a device: <see cref="GpsDeviceMapState.StatusId"/>. More values may follow.</summary>
    public class GpsDeviceMapStatus
    {
        public const short Invisible = 0;
        public const short Visible = 1;

        public short Id { get; set; }
        public string Name { get; set; } = default!;
        public DateTime LatestUpdate { get; set; }
    }
}
