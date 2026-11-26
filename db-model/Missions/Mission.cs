using db_model.Companies;
using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Missions
{
    public class Mission
    {
        public int Id { get; set; }
        public int CreatorUserId { get; set; }
        public int? CompanyId { get; set; }
        public string Name { get; set; } = default!;
        public short StatusId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string JoinKey { get; set; }
        public string? JoinQrCode { get; set; }

        public DateTime LatestUpdate { get; set; }
        public Company Company { get; set; }

    }
}
