using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace exs.Commons
{
    public class TNotAuthorizedException : Exception
    {
        public TNotAuthorizedException() : base()
        {
        }
        public TNotAuthorizedException(string message) : base(message)
        {
        }
        public TNotAuthorizedException(string message, string traceIdentifier) : base(message)
        {
            TraceIdentifier = traceIdentifier;
        }
        public string? TraceIdentifier { get; set; }
    }
}
