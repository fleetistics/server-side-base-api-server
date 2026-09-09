using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace exs.Commons
{
    public class TTraceIdentifierException : Exception
    {
        public TTraceIdentifierException(string traceIdentifier) : base()
        {
            TraceIdentifier = traceIdentifier;
        }
        public TTraceIdentifierException(string message, string traceIdentifier) : base(message)
        {
            TraceIdentifier = traceIdentifier;
        }

        public string TraceIdentifier { get; set; }
    }
}
