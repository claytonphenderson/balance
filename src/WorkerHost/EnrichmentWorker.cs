using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Channels;
using Enrichment;
using MailKit.Net.Smtp;
using Messaging;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace WorkerHost;

public class EnrichmentWorker(
    ILogger<EnrichmentWorker> logger,
    WorkerChannels channels,
    IMongoCollection<Expense> collection,
    ExpenseCategorizer expenseCategorizer)
    : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await channels.Enrichment.Reader.WaitToReadAsync();
                var incoming = await channels.Enrichment.Reader.ReadAsync();
                if (incoming.Merchant != null)
                {
                    var category = await expenseCategorizer.CategorizeMerchant(incoming.Merchant);
                    logger.LogInformation($"Categorized merchant {incoming.Merchant} as {category}");
                    
                    await collection.FindOneAndUpdateAsync(
                        Builders<Expense>.Filter.Eq(e => e.Id, incoming.Id),
                        Builders<Expense>.Update
                            .Set(e => e.Category, category)
                            .Set(e => e.CategorizedDate, DateTime.UtcNow)
                    );
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not process expense worker task");
            }
        }
    }
}
