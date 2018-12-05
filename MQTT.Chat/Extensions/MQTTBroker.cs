using IoTSharp.X509Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTT.Chat.Data;
using MQTT.Chat.Handlers;
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace MQTT.Chat
{
    public static class MQTTBroker
    {
        public static BrokerStatus Status { get; set; } = new BrokerStatus();
 

        public static void AddMQTTDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var _DataBase = configuration["DataBase"] ?? "sqlite";
            var _ConnectionString = Environment.ExpandEnvironmentVariables(configuration.GetConnectionString(_DataBase) ?? "Data Source=%APPDATA%\\MQTT.Chat\\MQTTChat.db;Pooling=true;");
            switch (_DataBase)
            {
                case "mssql":
                    services.AddEntityFrameworkSqlServer();
                    services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(_ConnectionString), ServiceLifetime.Transient);
                    break;

                case "npgsql":
                    services.AddEntityFrameworkNpgsql();
                    services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_ConnectionString), ServiceLifetime.Transient);
                    break;

                case "memory":
                    services.AddEntityFrameworkInMemoryDatabase();
                    services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(nameof(ApplicationDbContext)), ServiceLifetime.Transient);
                    break;

                case "sqlite":
                default:
                    services.AddEntityFrameworkSqlite();
                    services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_ConnectionString), ServiceLifetime.Transient);
                    break;
            }
        }
        internal static MqttEventsHandler _mqttEventsHandler;
        internal static MQTTBrokerOption MQTTBrokerOption;
        public static void UseMqttBrokerOption(this MqttServerOptionsBuilder builder,  IMqttServerStorage storage )
        {
            var options = MQTTBrokerOption;
                if (options.BrokerCertificate != null)
                {
                    builder.WithEncryptionCertificate(options.BrokerCertificate.Export(X509ContentType.Pfx))
                    .WithEncryptedEndpoint()
                    .WithEncryptedEndpointPort(options.SSLPort);
                }
                builder.WithDefaultEndpoint()
                .WithDefaultEndpointPort(options.Port)
                .WithStorage(storage)
                .WithConnectionValidator(_mqttEventsHandler.MqttConnectionValidatorContextAsync)
               .Build();
   
        }
        public static void UseEventsHander(this IApplicationBuilder app, MqttEventsHandler mqttEventsHandler)
        {
            _mqttEventsHandler = mqttEventsHandler;
            _mqttEventsHandler._signInManager = app.ApplicationServices.CreateScope().ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
        }
        public static void AddMqttBrokerOption(this IServiceCollection services, IConfiguration Configuration)
        {
            services.AddOptions()
                    .Configure<MQTTBrokerOption>(Configuration.GetSection(nameof(MQTTBrokerOption)))
                    .Configure((Action<MQTTBrokerOption>)(option =>
                    {
                        MQTTBrokerOption = option;
                        if (string.IsNullOrEmpty(option.CACertificateFile)) option.CACertificateFile = GetFullPathName("ca.crt");
                        if (string.IsNullOrEmpty(option.CAPrivateKeyFile)) option.CAPrivateKeyFile = GetFullPathName("ca.key");
                        if (string.IsNullOrEmpty(option.CertificateFile)) option.CertificateFile = GetFullPathName("broker.crt");
                        if (string.IsNullOrEmpty(option.PrivateKeyFile)) option.PrivateKeyFile = GetFullPathName("broker.key");

                        option.CACertificateFile = Environment.ExpandEnvironmentVariables(option.CACertificateFile);
                        option.CAPrivateKeyFile = Environment.ExpandEnvironmentVariables(option.CAPrivateKeyFile);
                        option.CertificateFile = Environment.ExpandEnvironmentVariables(option.CertificateFile);
                        option.PrivateKeyFile = Environment.ExpandEnvironmentVariables(option.PrivateKeyFile);
                        if (System.IO.File.Exists(option.CACertificateFile) && System.IO.File.Exists(option.CAPrivateKeyFile))
                        {
                            option.CACertificate = new X509Certificate2().LoadPem(option.CACertificateFile, option.CAPrivateKeyFile);
                        }
                        if (System.IO.File.Exists(option.CertificateFile) && System.IO.File.Exists(option.PrivateKeyFile))
                        {
                            option.BrokerCertificate = new X509Certificate2().LoadPem(option.CertificateFile, option.PrivateKeyFile);
                        }
                        if (!new FileInfo(option.CertificateFile).Directory.Exists) new FileInfo(option.CertificateFile).Directory.Create();
                        if (!new FileInfo(option.CACertificateFile).Directory.Exists) new FileInfo(option.CACertificateFile).Directory.Create();
                        option.Port = option.Port == 0 ? 1883 : option.Port;
                        option.SSLPort = option.SSLPort == 0 ? 8883 : option.SSLPort;
                    }));
        }

        private static string GetFullPathName(string filename)
        {
            FileInfo fi = new FileInfo(System.IO.Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create)
              , MethodBase.GetCurrentMethod().DeclaringType.Assembly.GetName().Name, filename));
            if (!fi.Directory.Exists) fi.Directory.Create();
            return fi.FullName;
        }

        public static void UseMqttBrokerLogger(this IApplicationBuilder app)
        {
            var mqttNetLogger = app.ApplicationServices.GetService<IMqttNetLogger>();
            var _loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
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

    public class MQTTBrokerOption
    {
        public X509Certificate2 CACertificate { get; set; }

        public X509Certificate2 BrokerCertificate { get; set; }
        public string CACertificateFile { get; set; } //   "CACertificateFile": "%APPDATA%\\IoT.MqqtBroker\\ca.crt,",
        public string CAPrivateKeyFile { get; set; } // "CAPrivateKeyFile": "%APPDATA%\\IoT.MqqtBroker\\ca.key",
        public string CertificateFile { get; set; } // "CertificateFile": "%APPDATA%\\IoT.MqqtBroker\\server.crt",
        public string PrivateKeyFile { get; set; } //"PrivateKeyFile": "%APPDATA%\\IoT.MqqtBroker\\server.key",
        public string KeyPassword { get; set; } // "KeyPassword": "",
        public int  SSLPort { get;  set; }=8883;
        public int Port { get; set; } = 1883;
    }
}