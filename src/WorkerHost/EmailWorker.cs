using System.Threading.Channels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Caching.Memory;

namespace WorkerHost;

public class EmailWorker : BackgroundService
{
    private readonly ILogger<EmailWorker> _logger;
    private readonly ImapClient _client = new ImapClient();
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
        var inbox = Reconnect();
        _logger.LogInformation($"Connected. {inbox.Count} messages in inbox.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Delivered today and contains the magic subject
                var query = SearchQuery.DeliveredAfter(DateTime.Now)
                    .And(SearchQuery.SubjectContains("You made a")
                    .And(SearchQuery.SubjectContains("transaction with")));

                foreach (var uid in inbox.Search(query))
                {
                    await ProcessIncoming(inbox, uid);
                }

                // Wait 10s before executing again.
                Thread.Sleep(10_000);
            }
            catch (Exception e)
            {
                _logger.LogError("Caught error in processing " + e.Message);
                _logger.LogInformation("Reconnecting...");
                Thread.Sleep(10_000);
                inbox = Reconnect();
                _logger.LogInformation("Reconnected");
            }
        }
    }

    private IMailFolder Reconnect()
    {
        if (_client.IsConnected) _client.Disconnect(true);

        _client.Connect("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
        _client.Authenticate(_configuration["EmailFrom"], _configuration["EmailAppPassword"]);

        var inbox = _client.Inbox;
        inbox.Open(FolderAccess.ReadOnly);
        return inbox;
    }

    public async Task ProcessIncoming(IMailFolder inbox, UniqueId uid)
    {
        // Make sure you don't double process messages
        if (_cache.Get(uid) != null) return;

        // Get messages since last run since IMAP server doesn't seem to support more granular filtering.  Also 
        // using last 30 seconds here specifically allowing for overlap with previous runs so we don't miss any
        var message = await inbox.GetMessageAsync(uid);
        if (DateTimeOffset.Compare(message.Date, DateTimeOffset.Now.AddSeconds(-30)) < 0) return;

        // Parse out interesting info
        var parsedTotalSuccessfully = Double.TryParse(message.Subject.Replace("$", "").Split(" ")[3], out var totalCost);
        if (!parsedTotalSuccessfully)
        {
            _logger.LogError($"Could not parse total for subject {message.Subject}");
            return;
        }

        var merchant = message.Subject.Split("transaction with")[1].Trim();

        // Push to channel to be consumed in a separate worker
        _channel.Writer.TryWrite(new IncomingExpenseEmail
        {
            Date = DateTime.UtcNow,
            RawSubject = message.Subject,
            Total = totalCost,
            Merchant = merchant,
        });

        _logger.LogInformation($"Pushed message {uid} to expense processing queue");

        // Remember that we processed this email
        _cache.Set(uid, uid, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });
    }
}
