using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SmartSync
{
    [CollectionDataContract(
       Name = "AgentStates", ItemName = "Agent", KeyName = "Path", ValueName = "State")]
    [Serializable]
    public class AgentStateMap : Dictionary<string, AgentState>
    {
        public AgentStateMap()
        {
        
        }
        
        protected AgentStateMap(
           SerializationInfo info, 
           StreamingContext context) : base(info, context)
        {

        }

    }
}
