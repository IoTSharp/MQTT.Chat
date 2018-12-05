using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MQTT.Chat.Extensions
{
    public static class MiscExtension
    {
        public static Task Forget(this Task task)
        {
            return Task.CompletedTask;
        }

    }
}
