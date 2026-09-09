using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace exs.Commons
{
    public class TUserMessageException : Exception
    {
        public TUserMessageException(string message) : base(message)
        {
        }
        public TUserMessageException(string message, Exception ex) : base(message, ex)
        {

        }
        public TUserMessageException(string message, string traceIdentifier) : base(message)
        {
            TraceIdentifier = traceIdentifier;
        }
        public TUserMessageException(string message, Exception ex, string traceIdentifier) : base(message, ex)
        {
            TraceIdentifier = traceIdentifier;
        }

        public string? TraceIdentifier { get; set; }
    }
}
