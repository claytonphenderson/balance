using System.Threading.Channels;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Caching.Memory;
using MimeKit;
using MongoDB.Driver;

namespace WorkerHost;

public class IngressWorker : BackgroundService
{
    private readonly ILogger<IngressWorker> _logger;
    private readonly Channel<Expense> _channel;
    private readonly IConfiguration _configuration;
    private readonly MemoryCache _cache;
    private readonly IMongoCollection<Expense> _collection;

    public IngressWorker(ILogger<IngressWorker> logger, Channel<Expense> channel, IConfiguration configuration, IMongoCollection<Expense> collection)
    {
        _logger = logger;
        _channel = channel;
        _configuration = configuration;
        _cache = new MemoryCache(new MemoryCacheOptions());
        _collection = collection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var client = new ImapClient())
                {
                    var inbox = await GetImapInbox(client);
                    _logger.LogDebug("Checking inbox...");

                    // Delivered today (this method ignores time, just looks at day) 
                    // and contains the magic subject
                    var query = SearchQuery.DeliveredAfter(DateTime.Now)
                        .And(SearchQuery.SubjectContains("You made a")
                        .And(SearchQuery.SubjectContains("transaction with")));

                    foreach (var uid in inbox.Search(query))
                    {
                        var message = await inbox.GetMessageAsync(uid);
                        if (message == null) continue;

                        await ProcessIncoming(message, uid);
                    }

                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Caught error in processing " + e.Message);
            }
            finally
            {
                Thread.Sleep(60_000);
            }
        }
    }

    private async Task<IMailFolder> GetImapInbox(ImapClient client)
    {
        if (client.IsConnected) await client.DisconnectAsync(true);

        await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(_configuration["EmailFrom"], _configuration["EmailAppPassword"]);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly);
        return inbox;
    }

    private async Task ProcessIncoming(IMimeMessage message, UniqueId uid)
    {
        try
        {
            // Avoid double processing messages
            if (_cache.Get(uid) != null) return;

            // Get messages since last run since IMAP server doesn't seem to support more granular filtering.
            if (DateTimeOffset.Compare(message.Date, DateTimeOffset.Now.AddDays(-1)) < 0) return;

            var expense = EmailHelpers.ParseMessage(message, uid);
            if (expense == null) return;

            // Store in MongoDB.  Will throw an exception if duplicate.
            await _collection.InsertOneAsync(expense);

            // Push to channel to be consumed in a separate worker
            await _channel.Writer.WriteAsync(expense);

            _logger.LogInformation($"Pushed message {uid} to expense processing queue");

            // Remember that we processed this email
            _cache.Set(uid, uid, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
            });
        }
        catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning(e, $"Duplicate key detected {uid.ToString()}, email has already been processed. Skipping");
            // Remember that we processed this email
            _cache.Set(uid, uid, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error when attempting to process incoming email");
        }
    }
}
