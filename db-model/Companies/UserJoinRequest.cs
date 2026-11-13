using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Companies
{
    public class UserJoinRequest
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int UserJoinInvitationId { get; set; }
        public int UserId { get; set; }

        public short StatusId { get; set; }
        public DateTime CreationDate { get; set; }
        public int HandledByCompanyUserId { get; set; }

        public DateTime LatestUpdate { get; set; }
    }
}
