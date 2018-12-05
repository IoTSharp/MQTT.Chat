using LiteDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTT.Chat.Data;
using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MQTT.Chat
{
    public class RetainedMessageHandler : IMqttServerStorage
    {
        static ApplicationDbContext _context;
        static ILogger _logger;
        public RetainedMessageHandler(ILogger<RetainedMessageHandler> logger, DbContextOptions<ApplicationDbContext> contextOptions)
        {
            _context =new ApplicationDbContext (contextOptions);
            _logger = logger;

        }

        public static IMqttServerStorage Instance { get;  set; }

        public async Task<IList<MqttApplicationMessage>> LoadRetainedMessagesAsync()
        {
            await Task.CompletedTask;
            try
            {
                return await _context.RetainedMessages.ToArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"load RetainedMessage error {ex.Message} ");
                return new List<MqttApplicationMessage>();
            }
        }

        public Task SaveRetainedMessagesAsync(IList<MqttApplicationMessage> messages)
        {
            Task.Factory.StartNew(() =>
            {
                _context.Database.BeginTransaction();
                try
                {
                    DateTime dateTime = DateTime.Now;
                    var needsave = from mam in messages select new RetainedMessage(mam);
                    var ids = needsave.Select(x => x.Id).ToList();
                    var dbids = _context.RetainedMessages.Select(x => x.Id).ToArray();
                    var needdelete = dbids.Except(ids);//.Except(dbids);
                    var del = from f in _context.RetainedMessages where needdelete.Contains(f.Id) select f;
                    var needadd = ids.Except(dbids);
                    var add = from f in needsave where needadd.Contains(f.Id) select f;
                    if (del.Any()) _context.RetainedMessages.RemoveRange(del);
                    if (add.Any()) _context.RetainedMessages.AddRange(add);
                    int ret = _context.SaveChanges();
                    _context.Database.CommitTransaction();
                    _logger.LogInformation($"SaveRetainedMessagesAsync 处理{ret}条数据，耗时{DateTime.Now.Subtract(dateTime).TotalSeconds}");
                }
                catch (Exception ex)
                {
                    _context.Database.RollbackTransaction();
                    _logger.LogError(ex, $"SaveRetainedMessagesAsync 时遇到异常{ex.Message}");
                }
            }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            return Task.CompletedTask;
        }
    }
}