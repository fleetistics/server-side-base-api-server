using db_model.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.UserManagement
{
    public class User
    {
        public int Id { get; set; }
        
        public string UserName { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public string FullName { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
        public short StatusId { get; set; }
        public short UserSourceId { get; set; }


        public string? Password { get; set; } = default!;
        public bool? IsPrivateMode { get; set; }

        
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int? AvatarImageId { get; set; }
        public int? GovIDImageId { get; set; }

        public UploadedMedia? AvatarImage { get; set; }
        public UploadedMedia? GovIDImage { get; set; }
        public UserLocationPrivacy? LocationPrivacy { get; set; }
    }
}
