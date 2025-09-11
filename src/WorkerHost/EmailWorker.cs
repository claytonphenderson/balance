using System.Threading.Channels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Caching.Memory;
using MimeKit;

namespace WorkerHost;

public class EmailWorker : BackgroundService
{
    private readonly ILogger<EmailWorker> _logger;
    private readonly Channel<IncomingExpenseEmail> _channel;
    private readonly IConfiguration _configuration;
    private readonly MemoryCache _cache;

    public EmailWorker(ILogger<EmailWorker> logger, Channel<IncomingExpenseEmail> channel, IConfiguration configuration)
    {
        _logger = logger;
        _channel = channel;
        _configuration = configuration;
        _cache = new MemoryCache(new MemoryCacheOptions());
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

                        ProcessIncoming(message, uid);
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

    private void ProcessIncoming(IMimeMessage message, UniqueId uid)
    {
        try
        {
            // Avoid double processing messages
            if (_cache.Get(uid) != null) return;

            // Get messages since last run since IMAP server doesn't seem to support more granular filtering.
            if (DateTimeOffset.Compare(message.Date, DateTimeOffset.Now.AddDays(-1)) < 0) return;

            var expense = EmailHelpers.ParseMessage(message, uid);
            if (expense == null) return;

            // Push to channel to be consumed in a separate worker
            _channel.Writer.TryWrite(expense);

            _logger.LogInformation($"Pushed message {uid} to expense processing queue");

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
