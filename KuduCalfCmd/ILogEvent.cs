using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartSync;
using System.Net;
namespace KuduCalfCmd
{
    interface ILogEvent
    {
        [LogEvent(TraceEventType.Error, "Unexpected error when doing site ping: {0}.")]
        void UnexpectedErrorDuringSitePing(AggregateException ex);

        [LogEvent(TraceEventType.Verbose, "Sending Sync Request to {0} using custom host header {1}.")]
        void SendingSyncRequestTo(Uri uri, string hostheader);

        [LogEvent(TraceEventType.Error, "Sync of subscriber {0} at uri {1} return with status code {2} expected 200.")]
        void SubscriberSyncWrongStatus(string subId, Uri uri, int httpStatusCode);

        [LogEvent(TraceEventType.Warning, "Ignoring toplevel git repository {0} in source directory.")]
        void IgnoringTopLevelGitRepositoryInSourceDirectory(string path);
    }

}
