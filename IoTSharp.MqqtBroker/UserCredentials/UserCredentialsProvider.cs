using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IoTSharp.MqqtBroker.UserCredentials
{
    public enum ProvidersName
    {
        LiteDB,
        PostgreSQL
    }
    public class UserCredentialsProvider
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public  ProvidersName Provider { get;  set; }
    }
}
