﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTT.Chat.Data;
using MQTT.Chat.Handlers;
using MQTTnet.AspNetCore;
using MQTTnet.AspNetCoreEx;
using MQTTnet.Client.Receiving;
using MQTTnet.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NSwag.AspNetCore;
using Quartz;
using QuartzHostedService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MQTT.Chat
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
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMqttBrokerOption(Configuration);

            services.AddMQTTDbContext(Configuration);
            services.AddTransient<MqttEventsHandler>();
            services.AddTransient<IMqttServerStorage, RetainedMessageHandler>();
            services.AddDefaultIdentity<IdentityUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddAuthentication().AddJwtBearer();
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            services.AddHostedMqttServerEx(builder => builder.UseMqttBrokerOption(_storage));
            services.AddMqttTcpServerAdapter();
            services.AddMqttConnectionHandler();
            services.AddMqttWebSocketServerAdapter();
            services.AddSwaggerDocument(configure =>
            {
                Assembly assembly = typeof(Startup).GetTypeInfo().Assembly;
                var description = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute));
                configure.Title = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;
                configure.Version = typeof(Startup).GetTypeInfo().Assembly.GetName().Version.ToString();
                configure.Description = description?.Description;
            });
            services.UseQuartzHostedService()
            .RegiserJob<BrokerStatus>(() =>
            {
                var result = new List<TriggerBuilder>();

                result.Add(TriggerBuilder.Create()
                        .WithSimpleSchedule(x => x.WithIntervalInSeconds(10).RepeatForever()));
                return result;
            });
        }

        private IMqttServerStorage _storage;

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IOptions<MQTTBrokerOption> options, MqttEventsHandler mqttEventsHandler, IMqttServerStorage storage)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwaggerUi3();
            //app.UseHealthChecks("/health", new HealthCheckOptions()
            //{
            //    Predicate = _ => true,
            //    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            //});

            //      app.UseHealthChecksUI();
            app.UseHttpsRedirection();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseMvc();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseMqttEndpoint();
            _storage = storage;
            app.UseMqttServerEx(server =>
            {
                server.ClientConnectedHandler = new MqttServerClientConnectedHandlerDelegate(args => mqttEventsHandler.Server_ClientConnected(server, args));
                server.StartedHandler = new MqttServerStartedHandlerDelegate(args => mqttEventsHandler.Server_Started(server, args));
                server.StoppedHandler = new MqttServerStoppedHandlerDelegate(args => mqttEventsHandler.Server_Stopped(server, args));
                server.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(args => mqttEventsHandler.Server_ApplicationMessageReceived(server, args));
                server.ClientSubscribedTopicHandler = new MqttServerClientSubscribedHandlerDelegate(args => mqttEventsHandler.Server_ClientSubscribedTopic(server, args));
                server.ClientUnsubscribedTopicHandler = new MqttServerClientUnsubscribedTopicHandlerDelegate(args => mqttEventsHandler.Server_ClientUnsubscribedTopic(server, args));
                server.ClientConnectionValidatorHandler = new MqttServerClientConnectionValidatorHandlerDelegate(args => mqttEventsHandler.MqttConnectionValidatorContextAsync(args.Context));
            });
            app.UseMqttBrokerLogger();
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
            app.UseSwagger(config => config.PostProcess = (document, request) =>
            {
                if (request.Headers.ContainsKey("X-External-Host"))
                {
                    // Change document server settings to public
                    document.Host = request.Headers["X-External-Host"].First();
                    document.BasePath = request.Headers["X-External-Path"].First();
                }
            });
            app.UseSwaggerUi3(config => config.TransformToExternalPath = (internalUiRoute, request) =>
            {
                // The header X-External-Path is set in the nginx.conf file
                var externalPath = request.Headers.ContainsKey("X-External-Path") ? request.Headers["X-External-Path"].First() : "";
                return externalPath + internalUiRoute;
            });
            app.UseAuthentication();
        }
    }
}