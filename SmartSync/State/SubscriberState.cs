using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SmartSync
{
    public class SubscriberState : AgentState
    {
        public string SubscribedToPublisher { get; set; }
        public string LastSyncedSnaphostIdToken { get; set; }
        public Uri PublicUpdateNotifyUri { get; set; }
        public Uri PrivateUpdateNotifyUri { get; set; }
    } 
}
