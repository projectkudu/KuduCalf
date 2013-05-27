using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging;
using System.Reflection;
using System.Globalization;
using System.Diagnostics;
namespace SmartSync
{
    public class EventLoggerFactory : RealProxy
    {
        TraceSource source;
        protected EventLoggerFactory(Type t,TraceSource ts) : base(t)
        {
            source = ts;
        }
        public static T CreateEventLogger<T>(TraceSource ts)
        {
            var proxy = new EventLoggerFactory(typeof(T), ts);
            return (T)proxy.GetTransparentProxy();
        }
        public override IMessage Invoke(IMessage msg)
        {
            IMethodCallMessage call = msg as IMethodCallMessage;
            if (call != null)
            {
                LogEventImpl(call.MethodBase, call.InArgs);
                return new MethodResponse(null, call);
            }
            throw new NotSupportedException("Can't handled message type");
        }

        // TODO use .NET tracing infrastructre/ETW
        private  void LogEventImpl(MethodBase eventMth, params object[] args)
        {
            
            var attrbs = eventMth.GetCustomAttributes(typeof(LogEventAttribute), true);
            var buf = new StringBuilder();
            if (attrbs.Length == 1)
            {
                var info = attrbs[0] as LogEventAttribute;
                if (info != null)
                {
                    buf.AppendFormat(CultureInfo.InvariantCulture, "[{0}], ", eventMth.Name);
                    if (string.IsNullOrEmpty(info.Message))
                    {
                        FormatArgumentDefault(buf, eventMth, args);
                    }
                    else
                    {
                        FormatArgumentMessage(buf, info.Message, args);
                    }
                    source.TraceEvent(info.EventType, 0, buf.ToString());
                }
            }
        }
        private static void FormatArgumentDefault(StringBuilder buf, MethodBase mth, params object[] args)
        {
            buf.Append("{");
            int idx = 0;
            foreach (var pinfo in mth.GetParameters())
            {
                if (idx != args.Length)
                {
                    buf.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}, ", pinfo.Name, args[idx]);
                }
                else
                {
                    buf.AppendFormat(CultureInfo.InvariantCulture, "{0}: {1}", pinfo.Name, args[idx]);
                }
                idx++;
            }
            buf.Append("}");
            buf.ToString();
        }
        private static void FormatArgumentMessage(StringBuilder buf, string fmt, params object[] args)
        {
            buf.AppendFormat(CultureInfo.InvariantCulture, fmt, args);
        }
    }
}
