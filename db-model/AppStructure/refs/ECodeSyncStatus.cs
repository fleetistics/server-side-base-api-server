using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.AppStructure
{
    public static class ECodeSyncStatus
    {
        public const short Created = 1;
        public const short Found = 2;
        public const short NotFound = 3;
        public const short Internal = 4;
    }
}
