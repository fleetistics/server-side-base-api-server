using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.AppStructure
{
    public class ApplicationPage
    {
        public int Id { get; set; }
        public string Path { get; set; } = "";
        public short ClientApplicationId { get; set; }
        public string CodeClassName { get; set; } = "";
        public short CodeSyncState { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }

        public string? KbItemKey { get; set; }
        
        public bool? IsNotForMenu { get; set; }
        public DateTime LatestUpdate { get; set; }
    }
}
