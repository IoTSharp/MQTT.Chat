using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MQTT.Chat.Extensions;
using System.Linq;

namespace MQTT.Chat.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

            if (Database.GetPendingMigrations().Count() > 0)
            {
                Database.Migrate();
            }
        }
        public DbSet<RetainedMessage> RetainedMessages { get; set; }
        public DbSet<StoreCertPem> StoreCertPem { get; set; }

    }
}