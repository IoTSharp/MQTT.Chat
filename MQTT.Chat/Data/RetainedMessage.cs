using MQTTnet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MQTT.Chat.Data
{
    [Serializable]
    public class RetainedMessage : MqttApplicationMessage
    {
        public RetainedMessage() : base()
        {

        }
        MD5 MD5 = MD5.Create();
        public RetainedMessage(MqttApplicationMessage retained) : base()
        {
            base.Topic = retained.Topic;
            base.Retain = retained.Retain;
            base.QualityOfServiceLevel = retained.QualityOfServiceLevel;
            base.Payload = retained.Payload;
            List<byte> lst = new List<byte>(base.Payload);
            lst.AddRange(System.Text.Encoding.UTF8.GetBytes(base.Topic));
            Id = BitConverter.ToString(MD5.ComputeHash(lst.ToArray())).Replace("-", "");
        }
        [Key]
        public string Id { get; set; }
    }
}
