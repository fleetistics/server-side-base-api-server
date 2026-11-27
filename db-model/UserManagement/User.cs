using db_model.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.UserManagement
{
    public class User
    {
        public int Id { get; set; }
        public int? CompanyId { get; set; }

        public string UserName { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string? LastName { get; set; }

        public DateTime LatestUpdate { get; set; }
        public short StatusId { get; set; }
        public short UserSourceId { get; set; }
        /// <summary>What the row is: a <see cref="UserKind"/> (1 User, 2 Contact, 3 Test).</summary>
        public short KindId { get; set; } = UserKind.User;


        public string? Password { get; set; } = default!;
        public bool? IsMfaEnabled { get; set; }
        public bool? IsEmailVerified { get; set; }
        public bool? IsPhoneVerified { get; set; }


        public short? IconKeyOnMap { get; set; }

        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int? AvatarImageId { get; set; }
        public int? GovIDImageId { get; set; }

        public UploadedMedia? AvatarImage { get; set; }
        public UploadedMedia? GovIDImage { get; set; }
        public UserLocationPrivacy? LocationPrivacy { get; set; }
    }
}
