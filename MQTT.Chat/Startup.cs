using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTT.Chat.Data;
using MQTTnet.AspNetCore;
using MQTTnet.Server;
using NJsonSchema;
using NSwag.AspNetCore;
using System.Reflection;

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
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            services.AddMQTTDbContext(Configuration);
            services.AddTransient<MqttEventsHandler>();
            services.AddSingleton<IMqttServerStorage, RetainedMessageHandler>();
            services.AddDefaultIdentity<IdentityUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddAuthentication().AddJwtBearer();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
            services.AddHostedMqttServer(builder => builder.UseMqttBrokerOption());
            services.AddMqttTcpServerAdapter();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IOptions<MQTTBrokerOption> options, MqttEventsHandler mqttEventsHandler, ApplicationDbContext context, IMqttServerStorage serverStorage)
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
            app.UseHttpsRedirection();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseMvc();
            app.UseMqttEndpoint();
            MqttEventsHandler.Instance = mqttEventsHandler;
            RetainedMessageHandler.Instance = serverStorage;
            app.UseMqttServer(server =>
            {
                server.ClientConnected += mqttEventsHandler.Server_ClientConnected;
                server.Started += mqttEventsHandler.Server_Started;
                server.Stopped += mqttEventsHandler.Server_Stopped;
                server.ApplicationMessageReceived += mqttEventsHandler.Server_ApplicationMessageReceived;
                server.ClientSubscribedTopic += mqttEventsHandler.Server_ClientSubscribedTopic;
                server.ClientUnsubscribedTopic += mqttEventsHandler.Server_ClientUnsubscribedTopic;
            });
            app.UseMqttBrokerLogger();
            context.Database.EnsureCreated();
            app.UseAuthentication();
        }
    }
}