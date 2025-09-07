using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MimeKit;
using MongoDB.Bson;
using MongoDB.Driver;

namespace WorkerHost;

public class ExpenseWorker : BackgroundService
{
    private readonly ILogger<EmailWorker> _logger;
    private readonly Channel<IncomingExpenseEmail> _channel;
    private readonly IMongoCollection<IncomingExpenseEmail> _collection;
    private readonly Subject<Unit> _expenseReceivedSubject = new Subject<Unit>();
    private readonly IConfiguration _configuration;

    public ExpenseWorker(ILogger<EmailWorker> logger,
    Channel<IncomingExpenseEmail> channel,
    IMongoCollection<IncomingExpenseEmail> collection,
    IConfiguration configuration)
    {
        _logger = logger;
        _channel = channel;
        _collection = collection;
        _configuration = configuration;

        // triggering the spend aggregation and summary email after 30 seconds of inactivity
        // so you don't get a ton of emails if you run the card several times quickly
        _expenseReceivedSubject.Throttle(TimeSpan.FromSeconds(30)).Subscribe(async (unit) =>
        {
            try
            {
                //perform aggregation to get current balance
                var summary = await AggregateSpend();
                // email the user
                await SendEmail(summary);
            }
            catch (Exception e)
            {
                _logger.LogError($"Could not aggregate and send email. {e.Message}");
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _channel.Reader.WaitToReadAsync();
            var incoming = await _channel.Reader.ReadAsync();
            _logger.LogInformation($"Received incoming expense from {incoming.Merchant}");
            await _collection.InsertOneAsync(incoming);
            _expenseReceivedSubject.OnNext(Unit.Default);
        }
    }

    /// <summary>
    /// Gets the running balance and calculate the percent of budget currently used.
    /// </summary>
    private async Task<Summary> AggregateSpend()
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
    private async Task SendEmail(Summary summary)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Balance", _configuration["EmailFrom"]));
        var recipients = _configuration.GetSection("EmailTo").Get<string[]>();
        foreach (var recipient in recipients)
        {
            message.To.Add(new MailboxAddress(recipient, recipient));
        }
        var dot = summary.PercentOfLimit > 85 ? "🔴" : summary.PercentOfLimit > 60 ? "🟡" : "🟢";
        message.Subject = $"{dot} You are at {summary.PercentOfLimit}% of budget";

        message.Body = new TextPart("plain")
        {
            Text = $"{dot} You are at {summary.PercentOfLimit}% of budget. The current balance is ${summary.Balance}."
        };

        using (var client = new SmtpClient())
        {
            await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_configuration["EmailFrom"], _configuration["EmailAppPassword"]); // app password here
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
