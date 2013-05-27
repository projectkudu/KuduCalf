using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Xml;
namespace SmartSync
{
    [DataContract]
    public class SmartSyncState
    {
        static private Type[] KnownTypes = { 
           typeof(SubscriberState), 
           typeof(PublisherState) };
        static DataContractSerializer seralizer = 
            new DataContractSerializer(typeof(SmartSyncState), KnownTypes);
        static XmlWriterSettings settings = 
            new XmlWriterSettings() { Indent = true, CloseOutput = false };

        public SmartSyncState()
        {
            Agents = new AgentStateMap();
        }
        [DataMember]
        public Uri Log { get; set; }
        
        [DataMember]
        public AgentStateMap Agents { get; private set; }

        public static SmartSyncState ReadFromStream(Stream strm)
        {
            return (SmartSyncState)seralizer.ReadObject(strm);
        }

        public static void WriteToStream(Stream strm,SmartSyncState state)
        {
            using (var xwr = XmlWriter.Create(strm, settings))
            {
                seralizer.WriteObject(xwr, state);
            }
        }
    }
}
