using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Channels;
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
            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            var messages = await emailService.GetTransactionEmails();
            foreach (var message in messages)
            {
                await ProcessIncoming(message);
            }
            Thread.Sleep(60_000);
        }
    }

    private async Task ProcessIncoming(IMimeMessage message)
        {
            try
            {
                // Avoid double processing messages
                if (_cache.Get(message.MessageId) != null) return;

                // Get messages since last run since IMAP server doesn't seem to support more granular filtering.
                if (DateTimeOffset.Compare(message.Date, DateTimeOffset.Now.AddDays(-10)) < 0) return;
                logger.LogInformation($"Processing incoming message {message.MessageId}");

                var expense = emailService.ParseMessage(message);
                if (expense == null) return;

                // Store in MongoDB.  Will throw an exception if duplicate.
                await collection.InsertOneAsync(expense);

                // Push to channel to be consumed in a separate worker
                await channels.Enrichment.Writer.WriteAsync(expense);
                newMessageSubject.OnNext(Unit.Default);

                logger.LogInformation($"Pushed message {message.MessageId} to expense processing queue");

                // Remember that we processed this email
                _cache.Set(message.MessageId, message.MessageId, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                });
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                logger.LogWarning(e,
                    $"Duplicate key detected {message.MessageId}, email has already been processed. Skipping");
                // Remember that we processed this email
                _cache.Set(message.MessageId, message.MessageId, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when attempting to process incoming email");
            }
        }
    }