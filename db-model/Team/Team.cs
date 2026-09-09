using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Team
{
    public class Team
    {
        public int Id { get; set; }
        public int CreatorUserId { get; set; }
        public string TeamName { get; set; } = default!;
        public short StatusId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ClosedDate { get; set; }
        public string JoinKey { get; set; }
        public string? JoinQrCode { get; set; }

        public List<TeamUploadedMedia> UploadedMedias { get; set; } = default!;
    }
}
