using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTT.Chat.Data;
using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MQTT.Chat
{
    public class MqttEventsHandler
    {
        public MqttEventsHandler(ILogger<MqttEventsHandler> logger,
            IOptions<MQTTBrokerOption> options
       )
        {
            _logger = logger;
            _options = options.Value;

        }
        internal SignInManager<IdentityUser> _signInManager;
        private MQTTBrokerOption _options;
        private ILogger<MqttEventsHandler> _logger;


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
        SortedDictionary<string, IdentityUser> _sessions = new SortedDictionary<string, IdentityUser>();
        internal void MqttConnectionValidatorContextAsync(MqttConnectionValidatorContext obj)
        {
            Task.Run(async () =>
            {
               
                if (!_sessions.ContainsKey(obj.ClientId) )
                {
                    var user = await _signInManager.UserManager.FindByNameAsync(obj.Username);
                    var claims = await _signInManager.UserManager.GetClaimsAsync(user);

                    if (await _signInManager.CanSignInAsync(user) && claims.Any(c=>c.Type== ClaimTypes.GivenName &&  c.Value ==obj.ClientId))
                    {
                     
                        var sresult = await _signInManager.CheckPasswordSignInAsync(user, obj.Password, false);
                        if (sresult.Succeeded)
                        {
                            obj.ReturnCode = MQTTnet.Protocol.MqttConnectReturnCode.ConnectionAccepted;
                            var loginfo = await _signInManager.UserManager.GetLoginsAsync(user);

                            _sessions.Add(obj.ClientId, user);
                        }
                        else if (sresult.IsLockedOut)
                        {
                            obj.ReturnCode = MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedIdentifierRejected;
                        }
                        else if (sresult.IsNotAllowed)
                        {
                            obj.ReturnCode = MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedNotAuthorized;
                        }
                        else if (sresult.RequiresTwoFactor)
                        {
                            obj.ReturnCode = MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedIdentifierRejected;
                        }
                        else
                        {
                            obj.ReturnCode = MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedNotAuthorized;
                        }
                    }
                }
                else
                {

                    obj.ReturnCode = MQTTnet.Protocol.MqttConnectReturnCode.ConnectionRefusedIdentifierRejected;
                }
            }).Wait(TimeSpan.FromSeconds(10));
        }
        internal void Server_ClientDisconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            Task.Run(async () =>
            {
                var lst = await ((IMqttServer)sender).GetClientSessionsStatusAsync();
                if (_sessions.ContainsKey(e.ClientId))
                {
                    _sessions.Remove(e.ClientId);
                }
            });
        }
    }
}