using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Companies
{
    public class PermissionRole
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public int? CompanyId { get; set; }
        public bool? IsDefault { get; set; }
        public string? Description { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
