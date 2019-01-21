using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using MQTT.Chat.Extensions;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MQTT.Chat
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var host = CreateWebHostBuilder(args).Build();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && host.IsRunningAsAService())
            {
                System.IO.Directory.SetCurrentDirectory(AppContext.BaseDirectory);
                host.RunAsService();
            }
            else
            {
                host.Run();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
             .UseKestrel(options => options.ListenAnyIP(5000))
             .UseStartup<Startup>();
    }
}