using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Companies
{
    public class UserJoinInvitation
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int CreatedByUserId { get; set; }
        /// <summary>The code in plain text (XXXX-XXXX). Null for a User invitation (Type 2): it uses the company's code.</summary>
        public string? JoinCode { get; set; }
        public string JoinQrCodeImageName { get; set; } = "";


        public int? PermissionRoleId { get; set; }
        public int? GroupId { get; set; }

        /// <summary>A UserJoinInvitationType id: 1 Company (the anonymous row), 2 User, 3 Personal.</summary>
        public short TypeId { get; set; }
        public bool? IsAutoAppRegistrationAllowed { get; set; }
        public bool? DoesNotRequireAdminApproval { get; set; }


        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Message { get; set; }


        public short StatusId { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime ExpirationDate { get; set; }


        public DateTime LatestUpdate { get; set; }
    }
}
