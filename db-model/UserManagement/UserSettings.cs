using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.UserManagement
{
    public class UserSettings
    {
        public int Id { get; set; }

        public int? UserId { get; set; }
        public string Name { get; set; }
        public string? Value { get; set; }

        public short? ClientApplicationId { get; set; }
        public short? ClientDevicePlatformId { get; set; }

        public DateTime LatestUpdate { get; set; }
    }
}
