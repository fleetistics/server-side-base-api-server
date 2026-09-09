using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.UserManagement
{
    public class UserLocationPrivacy
    {
        public short PrivacyMode { get; set; }
        public int UserId { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
