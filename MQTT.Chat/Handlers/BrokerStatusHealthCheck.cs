using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MQTT.Chat.Handlers
{
    public static class BrokerStatusHealthCheckBuilderExtensions
    {
        public static IHealthChecksBuilder AddBrokerCheck(
            this IHealthChecksBuilder builder,
            string name,
            HealthStatus? failureStatus = null,
            IEnumerable<string> tags = null,
            long? thresholdInBytes = null)
        {
            builder.AddCheck<BrokerHealthCheck>(name, failureStatus ?? HealthStatus.Degraded, tags);
            return builder;
        }
    }

    public class BrokerHealthCheck : IHealthCheck
    {
        private readonly IOptionsMonitor<MQTTBrokerOption> _options;

        public BrokerHealthCheck(IOptionsMonitor<MQTTBrokerOption> options)
        {
            _options = options;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = _options.Get(context.Registration.Name);
            Dictionary<string, object> keyValues = new Dictionary<string, object>();
          Newtonsoft.Json.Linq.JObject.FromObject(MQTTBroker.Status)?.Children().ToList().ForEach(jk =>
            {
                var jt = ((Newtonsoft.Json.Linq.JProperty)jk);
                keyValues.Add(jt.Name, jt.Value);
            });
            var result =MQTTBroker.Status ==null    ? context.Registration.FailureStatus : HealthStatus.Healthy;
            return Task.FromResult(new HealthCheckResult(
                result,
                description: "report broker status ",
                data: keyValues));
        }
    }

   
}
