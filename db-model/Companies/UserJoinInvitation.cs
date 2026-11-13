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
        public string JoinCode { get; set; }
        public string JoinQrCodeImageName { get; set; }


        public int? PermissionRoleId { get; set; }
        public int? GroupId { get; set; }

        public bool? IsPersonal { get; set; }
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
