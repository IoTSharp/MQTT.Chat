using MQTTnet;
using MQTTnet.Server;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MQTT.Chat.Handlers
{
    public class BrokerStatus : IJob
    {
        public BrokerStatus()
        {
            Topics = new Dictionary<string, int>();
            Changeset = System.Text.Encoding.Default.EncodingName;
            var mename = typeof(MqttEventsHandler).Assembly.GetName();
            var mqttnet = typeof(MqttClientSubscribedTopicEventArgs).Assembly.GetName();
            Version = $"{mename.Name}V{mename.Version.ToString()},{mqttnet.Name}.{mqttnet.Version.ToString()}";
        }

        public DateTime Uptime { get; set; }
        /// <summary>
        /// $SYS/broker/clients/total
        /// </summary>
        public long Clients { get; set; }
        public long Received { get; set; }

        /// <summary>
        /// "$SYS/broker/subscriptions/count"
        /// </summary>
        public long Subscribed { get; set; }
        public string Changeset { get; set; }

        public Dictionary<string, int> Topics { get; set; }

        public Task Execute(IJobExecutionContext context)
        {
            if (SYSTopics.Contains("$SYS/broker/version"))
            {
                mqttServer.PublishAsync("$SYS/broker/version", MQTTBroker.Status.Version, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
            if (SYSTopics.Contains("$SYS/broker/uptime"))
            {
                mqttServer.PublishAsync("$SYS/broker/uptime", MQTTBroker.Status.Uptime.ToString(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
            if (SYSTopics.Contains("$SYS/broker/changeset"))
            {
                mqttServer.PublishAsync("$SYS/broker/changeset", MQTTBroker.Status.Changeset, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
            if (SYSTopics.Contains("$SYS/broker/clients/total"))
            {
                mqttServer.PublishAsync("$SYS/broker/clients/total", MQTTBroker.Status.Clients.ToString(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
            if (SYSTopics.Contains("$SYS/broker/subscriptions/count"))
            {
                mqttServer.PublishAsync("$SYS/broker/subscriptions/count", MQTTBroker.Status.Topics.Count.ToString(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
            if (SYSTopics.Contains("$SYS/broker/timestamp"))
            {
                long timeStamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1).ToUniversalTime()).TotalSeconds;
                mqttServer.PublishAsync("$SYS/broker/timestamp", timeStamp.ToString(), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
            }
            return Task.CompletedTask;
        }
        public static List<string> SYSTopics { get; set; } = new List<string>();

        public static IMqttServer mqttServer { get; internal set; }
        public string Version { get; private set; }
    }

}
