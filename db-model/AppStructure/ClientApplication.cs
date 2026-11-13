using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.AppStructure
{
    public class ClientApplication
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; } = default!;

        public DateTime LatestUpdate { get; set; }
    }
}
