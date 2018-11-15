using LiteDB;
using MQTTnet;
using MQTTnet.Server;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MQTT.Chat
{
    internal class RetainedMessageHandler : IMqttServerStorage
    {
        private string v;

        public RetainedMessageHandler(string v)
        {
            this.v = v ?? "RetainedMessages.db";
        }

        public static IMqttServerStorage Instance { get; internal set; }

        public Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync()
        {
            using (var db = new LiteDatabase(v))
            {
                // Get customer collection
                var col = db.GetCollection<MqttApplicationMessage>();
                // Use LINQ to query documents (with no index)
                var results = Task.Factory.StartNew(() =>
                {
                    var r = (IList<MqttApplicationMessage>)new List<MqttApplicationMessage>(col.FindAll());
                    db.DropCollection(col.Name);
                    return r;
                });
                return results;
            }
        }

        public Task SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages)
        {
            using (var db = new LiteDatabase(v))
            {
                // Get customer collection
                var col = db.GetCollection<MqttApplicationMessage>();
                var task = Task.Factory.StartNew(() =>
                  {
                      col.EnsureIndex(x => x.Topic, true);
                      col.InsertBulk(messages);
                  });
                return task;
            }
        }
    }
}