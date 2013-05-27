using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;
namespace SmartSync
{
    public abstract class SmartSyncStateOperations
    {
        abstract public bool Initialize(bool force = false);
        abstract public bool WaitForStateChange(TimeSpan timeout);
        abstract protected SmartSyncState ReadState();
        abstract protected void ReadModifyWriteState(Action<SmartSyncState> act);

        public PublisherState GetPublisher(string id)
        {
            return (PublisherState)GetAgent(id);
        }

        public SubscriberState GetSubscriber(string id)
        {
            return (SubscriberState)GetAgent(id);
        }

        public IEnumerable<SubscriberState> GetSubcribers()
        {
            return GetSubcriberByPrefix("");
        }

        public IEnumerable<PublisherState> GetPublishers()
        {
            return GetPublishersByPrefix("");
        }

        public IEnumerable<SubscriberState> GetSubcriberByPrefix(string id)
        {
           return  GetAgentsByPrefix(id).OfType<SubscriberState>();
        }

        public IEnumerable<PublisherState> GetPublishersByPrefix(string id)
        {
            return GetAgentsByPrefix(id).OfType<PublisherState>();
        }

        public bool CreatePublisher(PublisherState initial, bool replaceExisting = false)
        {
            return CreateAgent(initial, replaceExisting);
        }

        public bool CreateSubscriber(SubscriberState initial, bool replaceExisting = false)
        {
            return CreateAgent(initial, replaceExisting);
        }

        public void UpdatePublisher(string id, Action<PublisherState> act)
        {
            UpdateAgent(id, agent => act((PublisherState)agent));
        }

        public void UpdateSubscriber(string id, Action<SubscriberState> act)
        {
            UpdateAgent(id, agent => act((SubscriberState)agent));
        }

        public void Delete(string id)
        {
            DeleteAgent(id);
        }

        public void GarbageCollectExpiredAgents()
        {
            ReadModifyWriteState(state =>
             {
                 try
                 {
                     var staleThreshold = DateTimeOffset.UtcNow;
                     var staleAgents = (from kv in state.Agents
                                        where kv.Value.ExpiryTime < staleThreshold
                                        select kv.Key);
                     foreach (string id in staleAgents.ToArray())
                     {
                         state.Agents.Remove(id);
                     }
                 }
                 catch (Exception ex)
                 {
                     DefaultLogger.LogEvent.UnexpectedExceptionAgentGarabeCollect(ex);
                 }
             });
        }

        private void UpdateAgent(string id, Action<AgentState> act)
        {
            ReadModifyWriteState(state =>
                {
                    var agent = state.Agents[id];
                    act(agent);
                    agent.LastModified = DateTimeOffset.UtcNow;
                });
        }
        
        private AgentState GetAgent(string id)
        {
            var state = ReadState();
            if (!state.Agents.ContainsKey(id))
            {
                return null;
            }
            return state.Agents[id];
        }

        private bool CreateAgent(AgentState init, bool replaceExisiting)
        {
            bool created = false;
            ReadModifyWriteState(state =>
                {
                    init.Created = DateTimeOffset.UtcNow;
                    init.LastModified = init.Created;
                    if (!state.Agents.ContainsKey(init.Id))
                    {
                        state.Agents.Add(init.Id, init);
                        created = true;
                    }
                    else if(replaceExisiting)
                    {
                        state.Agents[init.Id] = init;
                        created = true;
                    }
                });
            return created ;
        }

        private IEnumerable<AgentState> GetAgentsByPrefix(string id)
        {
            var state = ReadState();
            return from kv in state.Agents
                   where kv.Key.StartsWith(id)
                   select kv.Value;
        }

        private void DeleteAgent(string id)
        {
            ReadModifyWriteState(state =>
            {
                state.Agents.Remove(id);
            });
        }
    }
}
