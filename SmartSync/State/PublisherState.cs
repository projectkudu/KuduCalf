using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SmartSync
{
    public class PublisherState : AgentState
    {
        public Uri RepositoryUri { get; set; }
        public string LatestSnapshotIdToken { get; set; }
        public string StableSnapshotIdToken { get; set; }
    }
}
