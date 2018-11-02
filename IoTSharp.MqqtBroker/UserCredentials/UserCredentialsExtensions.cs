using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IoTSharp.MqqtBroker.UserCredentials
{
    public static class UserCredentialsExtensions
    {
        public static  void  AddUserCredentials(this IServiceCollection services, UserCredentialsProvider provider)
        {
            switch (provider.Provider)
            {
                case ProvidersName.PostgreSQL:
                    break;
                case ProvidersName.LiteDB:
                default:
                    services.AddLiteDBProvider();
                    break;
            }
        }

        private static void AddLiteDBProvider(this IServiceCollection services)
        {
            services.AddSingleton<AspNetCore.Identity.LiteDB.Data.LiteDbContext>();
            services.AddIdentity<AspNetCore.Identity.LiteDB.Models.ApplicationUser, AspNetCore.Identity.LiteDB.IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddUserStore<AspNetCore.Identity.LiteDB.LiteDbUserStore<AspNetCore.Identity.LiteDB.Models.ApplicationUser>>()
            .AddRoleStore<AspNetCore.Identity.LiteDB.LiteDbRoleStore<AspNetCore.Identity.LiteDB.IdentityRole>>()
            .AddDefaultTokenProviders();
        }
    }
}
