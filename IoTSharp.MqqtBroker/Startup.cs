using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using IoTSharp.MqqtBroker.Settings;
using IoTSharp.MqqtBroker.UserCredentials;
using IoTSharp.X509Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.AspNetCore;
using MQTTnet.Diagnostics;
using MQTTnet.Server;
using NJsonSchema;
using NSwag.AspNetCore;

namespace IoT.MqqtBroker
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
            services.AddUserCredentials(Configuration.GetSection("UserCredentials").Get<UserCredentialsProvider>());

            var ServerCert = new X509Certificate2().LoadPem(Configuration["CertificateFile"], Configuration["PrivateKeyFile"], Configuration["KeyPassword"]);
            services.AddHostedMqttServer(builder => builder
                 .WithEncryptionCertificate(ServerCert.Export(X509ContentType.Pfx))
                .WithEncryptedEndpoint()
                  .WithConnectionValidator(MqttEventsHandler.MqttConnectionValidatorContext)
                 .WithoutDefaultEndpoint()
                 .WithStorage(new RetainedMessageHandler(Configuration["MqttStorageFile"]))
                 .Build()
            );
            services.AddMqttTcpServerAdapter();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseStaticFiles();
            app.UseSwaggerUi3WithApiExplorer(settings =>
            {
                settings.GeneratorSettings.DefaultPropertyNameHandling =
                    PropertyNameHandling.CamelCase;
                settings.GeneratorSettings.Title = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;
                settings.GeneratorSettings.Version = typeof(Startup).GetTypeInfo().Assembly.GetName().Version.ToString();
            });
            app.UseMvc();
            app.UseMqttEndpoint();
            app.UseMqttServer(server =>
            {
                server.ClientConnected += MqttEventsHandler.Server_ClientConnected;
                server.Started += MqttEventsHandler.Server_Started;
                server.Stopped += MqttEventsHandler.Server_Stopped;
                server.ApplicationMessageReceived += MqttEventsHandler.Server_ApplicationMessageReceived;
                server.ClientSubscribedTopic += MqttEventsHandler.Server_ClientSubscribedTopic;
                server.ClientUnsubscribedTopic += MqttEventsHandler.Server_ClientUnsubscribedTopic;
                MqttEventsHandler.Server = server;
            });
            var mqttNetLogger = app.ApplicationServices.GetService<IMqttNetLogger>();
            var _loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
            MqttEventsHandler.Logger = _loggerFactory.CreateLogger<MqttEventsHandler>();
            var logger = _loggerFactory.CreateLogger<IMqttNetLogger>();
            mqttNetLogger.LogMessagePublished += (object sender, MqttNetLogMessagePublishedEventArgs e) =>
        {
            var message = $"ID:{e.TraceMessage.LogId},ThreadId:{e.TraceMessage.ThreadId},Source:{e.TraceMessage.Source},Timestamp:{e.TraceMessage.Timestamp},Message:{e.TraceMessage.Message}";
            switch (e.TraceMessage.Level)
            {
                case MqttNetLogLevel.Verbose:
                    logger.LogTrace(e.TraceMessage.Exception, message);
                    break;
                case MqttNetLogLevel.Info:
                    logger.LogInformation(e.TraceMessage.Exception, message);
                    break;
                case MqttNetLogLevel.Warning:
                    logger.LogWarning(e.TraceMessage.Exception, message);
                    break;
                case MqttNetLogLevel.Error:
                    logger.LogError(e.TraceMessage.Exception, message);
                    break;
                default:
                    break;
            }
        };
        }
    }
}
