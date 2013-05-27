using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SmartSync
{
    public abstract class AgentState
    {
        protected AgentState()
        {
            ExpiryTime = DateTimeOffset.MaxValue;
        }
        public string Id { get; set; }
        public DateTimeOffset ExpiryTime { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset LastModified { get; set; }
    }
}
