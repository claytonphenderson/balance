using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MimeKit;
using MongoDB.Bson;
using MongoDB.Driver;

namespace WorkerHost;

public class EnrichmentWorker : BackgroundService
{
    private readonly ILogger<EnrichmentWorker> _logger;
    private readonly Channel<Expense> _channel;
    private readonly IMongoCollection<Expense> _collection;
    private readonly Subject<Unit> _expenseReceivedSubject = new Subject<Unit>();
    private readonly IConfiguration _configuration;
    private readonly ExpenseCategorizer _expenseCategorizer;

    public EnrichmentWorker(ILogger<EnrichmentWorker> logger,
    Channel<Expense> channel,
    IMongoCollection<Expense> collection,
    IConfiguration configuration,
    ExpenseCategorizer expenseCategorizer)
    {
        _logger = logger;
        _channel = channel;
        _collection = collection;
        _configuration = configuration;
        _expenseCategorizer = expenseCategorizer;

        // triggering the spend aggregation and summary email after 30 seconds of inactivity
        // so you don't get a ton of emails if you run the card several times quickly
        _expenseReceivedSubject.Throttle(TimeSpan.FromSeconds(30)).Subscribe(async (unit) =>
        {
            try
            {
                //perform aggregation to get current balance
                var summary = await AggregateSpend();
                // email the user
                using (var client = new SmtpClient())
                {
                    await SendEmail(client, summary);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Could not aggregate and send email.");
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _channel.Reader.WaitToReadAsync();
                var incoming = await _channel.Reader.ReadAsync();
                if (incoming.Merchant != null)
                {
                    var category = await _expenseCategorizer.CategorizeMerchant(incoming.Merchant);
                    _logger.LogInformation($"Categorized merchant {incoming.Merchant} as {category}");
                    await _collection.FindOneAndUpdateAsync(
                        Builders<Expense>.Filter.Eq(e => e.Id, incoming.Id),
                        Builders<Expense>.Update
                            .Set(e => e.Category, category)
                            .Set(e => e.CategorizedDate, DateTime.UtcNow)
                    );
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not process expense worker task");
            }
        }
    }

    /// <summary>
    /// Gets the running balance and calculate the percent of budget currently used.
    /// </summary>
    public async Task<Summary> AggregateSpend()
    {
        var result = await _collection.Aggregate()
            .Group(new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "total", new BsonDocument("$sum", "$total") }
            }).FirstOrDefaultAsync();

        var balance = result?["total"].AsDouble ?? throw new Exception("Could not perform aggregation");

        return new Summary
        {
            Balance = balance,
            PercentOfLimit = Math.Round(balance / Double.Parse(_configuration["SpendLimit"]!) * 100, 1)
        };
    }

    /// <summary>
    /// Send a summary email with aggregation results via SMTP.
    /// </summary>
    public async Task SendEmail(SmtpClient client, Summary summary)
    {
        var message = EmailHelpers.BuildMessage(summary,
            _configuration.GetSection("EmailTo").Get<string[]>() ?? throw new Exception("to is missing"),
            _configuration["EmailFrom"] ?? throw new Exception("from is missing"));

        await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_configuration["EmailFrom"], _configuration["EmailAppPassword"]); // app password here
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Sent email to recipients");
    }
}
