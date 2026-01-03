using System.Reactive;
using System.Reactive.Subjects;
using Messaging;
using Microsoft.Extensions.Caching.Memory;
using MimeKit;
using Models;
using MongoDB.Driver;

namespace WorkerHost;

public class IngressWorker(
    ILogger<IngressWorker> logger,
    WorkerChannels channels,
    IMongoCollection<Expense> collection,
    EmailService emailService,
    Subject<Unit> newMessageSubject)
    : BackgroundService
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    /**
     * Check the email inbox for emails from the credit card company.
     * - parse them
     * - save to database
     * - write the expense data to the enrichment channel
     */
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Ingress worker running at: {time}", DateTimeOffset.Now);
            // Get all transaction messages from last day
            var messageIds = await emailService.QueryInboxForTransactions();
            // filter out the ones we've aready seen
            var filtered = messageIds.Where(x => _cache.Get(x) == null).ToList();
            // download the emails
            var messages = await emailService.DownloadEmails(filtered);
            
            // process each of those
            foreach (var message in messages)
            {
                await ProcessIncoming(message);
            }
            
            // remember that we've seen them and avoid re-loading them from inbox
            filtered.ForEach(uid =>
            {
                _cache.Set(uid, uid, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                });
            });
            
            Thread.Sleep(60_000);
        }
    }

    private async Task ProcessIncoming(IMimeMessage message)
        {
            try
            {
                logger.LogInformation($"Processing incoming message {message.MessageId}");
                var expense = emailService.ParseMessage(message);
                if (expense == null) return;

                // Store in MongoDB.  Will throw an exception if duplicate.
                await collection.InsertOneAsync(expense);

                // Push to channel to be consumed in a separate worker
                await channels.Enrichment.Writer.WriteAsync(expense);
                newMessageSubject.OnNext(Unit.Default);

                logger.LogInformation($"Pushed message {message.MessageId} to expense processing queue");
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                logger.LogWarning(e,
                    $"Duplicate key detected {message.MessageId}, email has already been processed. Skipping");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when attempting to process incoming email");
            }
        }
    }