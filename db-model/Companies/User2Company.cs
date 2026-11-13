using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Companies
{
    public class User2Company
    {
        
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public int PermissionRoleId { get; set; }

        public short StatusId { get; set; }
        public short JoinTypeId { get; set; }
        public DateTime JoinedDate { get; set; }
        

        public DateTime LatestUpdate { get; set; }
    }
}
