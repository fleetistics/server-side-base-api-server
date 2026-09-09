namespace db_model.Map
{

    public class MobileDeviceMotionActivity
    {
        public const short Still = 1;
        public const short Walking = 2;
        public const short OnFoot = 3;
        public const short Running = 4;
        public const short OnBicycle = 5;
        public const short InVehicle = 6;

        public short Id { get; set; }
        public string Name { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
