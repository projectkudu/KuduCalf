using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
namespace SmartSync
{
    static class DefaultLogger
    {
        static public ILogEvent LogEvent { get; set; }
        // TODO: reconsider the pattern of using a static constructor
        static DefaultLogger()
        {
            var ts = new TraceSource("SmartSync", 
                SourceLevels.Critical | 
                SourceLevels.Error | 
                SourceLevels.Warning | 
                SourceLevels.Verbose);
            LogEvent = EventLoggerFactory.CreateEventLogger<ILogEvent>(ts);
        }
    }
}
