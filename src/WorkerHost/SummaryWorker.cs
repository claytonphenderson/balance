using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MailKit.Net.Smtp;
using Messaging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace WorkerHost;

public class SummaryWorker:BackgroundService
{
    private readonly ILogger<SummaryWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly EmailService _emailService;
    private readonly IMongoCollection<Expense> _collection;

    public SummaryWorker(Subject<Unit> subject,
        ILogger<SummaryWorker> logger,
        IConfiguration configuration,
        EmailService emailService,
        IMongoCollection<Expense> collection)
    {
        _logger = logger;
        _configuration = configuration;
        _emailService = emailService;
        _collection = collection;

        subject.Throttle(TimeSpan.FromSeconds(60)).Subscribe(async _ =>
        {
            //perform aggregation to get current balance
            var summary = AggregateSpend();
            var categories = AggregateCategories();
            
            using var client = new SmtpClient();
            await SendEmail(client, await summary, await categories);
        });
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
    /// Gets the running balance for each spend category
    /// </summary>
    private async Task<Dictionary<string, double>> AggregateCategories()
    {
        var result = await _collection.Aggregate()
            .Group(new BsonDocument
            {
                { "_id", "$category" },
                { "total", new BsonDocument("$sum", "$total") }
            }).ToListAsync();

        var categoryTotals = result.ToDictionary(
            r => r["_id"].AsString,
            r => r["total"].AsDouble
        );

        return categoryTotals;
    }

    /// <summary>
    /// Send a summary email with aggregation results via SMTP.
    /// </summary>
    private async Task SendEmail(SmtpClient client, Summary summary, Dictionary<string, double> categoryTotals)
    {
        var message = _emailService.BuildMessage(summary,
            categoryTotals,
            _configuration.GetSection("EmailTo").Get<string[]>() ?? throw new Exception("to is missing"),
            _configuration["EmailFrom"] ?? throw new Exception("from is missing"));

        await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_configuration["EmailFrom"],
            _configuration["EmailAppPassword"]); // app password here
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Sent email to recipients");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}