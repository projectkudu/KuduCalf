using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartSync
{
    [AttributeUsage(AttributeTargets.Method)]
    public class LogEventAttribute : Attribute
    {
        public LogEventAttribute(System.Diagnostics.TraceEventType evtType, string fmt)
        {
            Message = fmt;
            EventType = evtType;
        }

        public LogEventAttribute(System.Diagnostics.TraceEventType evtType)
            : this(evtType, null)
        {
        }

        public LogEventAttribute(string fmt)
            : this(TraceEventType.Verbose, fmt)
        {

        }

        public LogEventAttribute()
            : this(TraceEventType.Verbose, null)
        {

        }

        public string Message { get; set; }
        public System.Diagnostics.TraceEventType EventType { get; set; }
    }
}
