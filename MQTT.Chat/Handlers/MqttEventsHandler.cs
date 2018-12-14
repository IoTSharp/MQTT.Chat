using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTT.Chat.Data;
using MQTT.Chat.Handlers;
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


 

        internal void Server_ClientConnected(object sender, MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation($"Client [{e.ClientId}] Connected");
            MQTTBroker.Status.Clients++;
    
        }

        private static DateTime uptime = DateTime.MinValue;

        internal void Server_Started(object sender, EventArgs e)
        {
            _logger.LogInformation($"Server is started");
            BrokerStatus.mqttServer = (IMqttServer)sender;
            MQTTBroker.Status.Uptime = DateTime.Now;

        }

        internal void Server_Stopped(object sender, EventArgs e)
        {
            _logger.LogInformation($"Server is stopped");
        }

   
 

        internal void Server_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogInformation($"Server received msg from {e.ClientId}: Topic=[{e.ApplicationMessage.Topic }],Retain=[{e.ApplicationMessage.Retain}],QualityOfServiceLevel=[{e.ApplicationMessage.QualityOfServiceLevel}]");
            if (!e.ApplicationMessage.Topic.StartsWith("$SYS"))
            {
                if (!MQTTBroker.Status.Topics.ContainsKey(e.ApplicationMessage.Topic))
                {
                    MQTTBroker.Status.Topics.Add(e.ApplicationMessage.Topic, 1);

                }
                else
                {
                    MQTTBroker.Status.Topics[e.ApplicationMessage.Topic]++;
                }
                MQTTBroker.Status. Received += e.ApplicationMessage.Payload.Length;
            }
          
        }

      

        internal void Server_ClientSubscribedTopic(object sender, MqttClientSubscribedTopicEventArgs e)
        {
            _logger.LogInformation($"Client [{e.ClientId}]Subscribed[{e.TopicFilter}]");
            if (e.TopicFilter.Topic.StartsWith("$SYS/"))
            {
                BrokerStatus.SYSTopics.Add(e.TopicFilter.Topic); 
                
            }
            else
            {
                MQTTBroker.Status.Subscribed++;
            }
        }



        internal void Server_ClientUnsubscribedTopic(object sender, MqttClientUnsubscribedTopicEventArgs e)
        {
            _logger.LogInformation($"Client [{e.ClientId}] Unsubscribed[{e.TopicFilter}]");
            if (!e.TopicFilter.StartsWith("$SYS/"))
            {
                MQTTBroker.Status.Subscribed--;
            }
            else
            {
                BrokerStatus.SYSTopics.Remove(e.TopicFilter);
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
                            _logger.LogInformation($"Server accepted  [{obj.ClientId}]({obj.Username})'s connection");
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
            Task.Run(() =>
           {
               var lst = ((IMqttServer)sender).GetClientSessionsStatus();
               if (_sessions.ContainsKey(e.ClientId))
               {
                   _sessions.Remove(e.ClientId);
               }
           });
        }
    }
}