using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTT.Chat.Data;
using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MQTT.Chat
{
    public class MqttEventsHandler
    {
        public MqttEventsHandler(ILogger<MqttEventsHandler> logger, IOptions<MQTTBrokerOption> options, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
            _options = options.Value;
        }

        private MQTTBrokerOption _options;
        private ApplicationDbContext _context;
        private ILogger<MqttEventsHandler> _logger;
        public static MqttEventsHandler Instance { get; internal set; }

        private static long clients = 0;

        internal void Server_ClientConnected(object sender, MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation($"客户端[{e.ClientId}]已连接");
            clients++;
            Task.Run(() => ((IMqttServer)sender).PublishAsync("$SYS/broker/clients/total", clients.ToString()));
        }

        private static DateTime uptime = DateTime.MinValue;

        internal void Server_Started(object sender, EventArgs e)
        {
            _logger.LogInformation($"服务器已启动");
            uptime = DateTime.Now;
        }

        internal void Server_Stopped(object sender, EventArgs e)
        {
            _logger.LogInformation($"服务器已终止");
        }

        private static Dictionary<string, int> lstTopics = new Dictionary<string, int>();
        private static long received = 0;

        internal void Server_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogInformation($"服务器收到客户端{e.ClientId}的消息: Topic=[{e.ApplicationMessage.Topic }],Retain=[{e.ApplicationMessage.Retain}],QualityOfServiceLevel=[{e.ApplicationMessage.QualityOfServiceLevel}]");
            if (!lstTopics.ContainsKey(e.ApplicationMessage.Topic))
            {
                lstTopics.Add(e.ApplicationMessage.Topic, 1);
                Task.Run(() => ((IMqttServer)sender).PublishAsync("$SYS/broker/subscriptions/count", lstTopics.Count.ToString()));
            }
            else
            {
                lstTopics[e.ApplicationMessage.Topic]++;
            }
            received += e.ApplicationMessage.Payload.Length;
        }

        private static long Subscribed;

        internal void Server_ClientSubscribedTopic(object sender, MqttClientSubscribedTopicEventArgs e)
        {
            _logger.LogInformation($"客户端[{e.ClientId}]订阅[{e.TopicFilter}]");
            if (e.TopicFilter.Topic.StartsWith("$SYS/"))
            {
                if (e.TopicFilter.Topic.StartsWith("$SYS/broker/version"))
                {
                    var mename = typeof(MqttEventsHandler).Assembly.GetName();
                    var mqttnet = typeof(MqttClientSubscribedTopicEventArgs).Assembly.GetName();
                    Task.Run(() => ((IMqttServer)sender).PublishAsync("$SYS/broker/version", $"{mename.Name}V{mename.Version.ToString()},{mqttnet.Name}.{mqttnet.Version.ToString()}"));
                }
                else if (e.TopicFilter.Topic.StartsWith("$SYS/broker/uptime"))
                {
                    Task.Run(() => ((IMqttServer)sender).PublishAsync("$SYS/broker/uptime", uptime.ToString()));
                }
            }
            else
            {
                Subscribed++;
                Task.Run(() => ((IMqttServer)sender).PublishAsync("$SYS/broker/subscriptions/count", Subscribed.ToString()));
            }
        }

        internal void Server_ClientUnsubscribedTopic(object sender, MqttClientUnsubscribedTopicEventArgs e)
        {
            _logger.LogInformation($"客户端[{e.ClientId}]取消订阅[{e.TopicFilter}]");
            if (!e.TopicFilter.StartsWith("$SYS/"))
            {
                Subscribed--;
                Task.Run(() => ((IMqttServer)sender).PublishAsync("$SYS/broker/subscriptions/count", Subscribed.ToString()));
            }
        }

        internal void MqttConnectionValidatorContext(MqttConnectionValidatorContext obj)
        {
            _logger.LogInformation($"ClientId={obj.ClientId},Endpoint={obj.Endpoint},Username={obj.Username}，Password={obj.Password},WillMessage={obj.WillMessage?.ConvertPayloadToString()}");
            obj.ReturnCode = MQTTnet.Protocol.MqttConnectReturnCode.ConnectionAccepted;
        }
    }
}