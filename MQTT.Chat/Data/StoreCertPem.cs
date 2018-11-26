using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace MQTT.Chat.Data
{
    public class StoreCertPem
    {
        [Key]
        public string Id { get; set; }
        public string ClientCert { get; set; }
        public string ClientKey { get; set; }

    }
}
