using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Companies
{
    public class User2Group
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public int GroupId { get; set; }

        public DateTime LatestUpdate { get; set; }
    }
}
