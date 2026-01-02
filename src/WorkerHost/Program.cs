using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using Azure;
using Azure.AI.OpenAI;
using Enrichment;
using Messaging;
using Microsoft.Extensions.Logging.Console;
using Models;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using OpenAI.Chat;
using WorkerHost;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
        options.ColorBehavior = LoggerColorBehavior.Enabled;
    });

builder.Services.AddHostedService<IngressWorker>();
builder.Services.AddHostedService<EnrichmentWorker>();
builder.Services.AddHostedService<SummaryWorker>();

builder.Services.AddSingleton<WorkerChannels>();
builder.Services.AddSingleton<ExpenseCategorizer>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<ChatClient>(c =>
{
    var configuration = c.GetRequiredService<IConfiguration>();
    Uri openaiEndpoint = new Uri(configuration["AzOpenAIEndpoint"]);
    string apiKey = configuration["AzOpenAIApiKey"];
    AzureOpenAIClient client = new(openaiEndpoint, new AzureKeyCredential(apiKey));
    return client.GetChatClient("gpt-4.1-mini");
});
builder.Services.AddSingleton<Subject<Unit>>();

// Mongo setup
ConventionRegistry.Register(
    "CamelCaseConvention",
    new ConventionPack
    {
        new CamelCaseElementNameConvention()
    },
    t => true // apply to all types
);
builder.Services.AddSingleton<MongoClient>(c =>
{
    var client = new MongoClient(builder.Configuration.GetSection("Database")["MongoUrl"]);
    return client;
});
builder.Services.AddSingleton<IMongoCollection<Expense>>(x =>
{
    var client = x.GetRequiredService<MongoClient>();
    var db = client.GetDatabase("balance");
    // creates if not existing
    db.CreateCollection("expenses");

    return db.GetCollection<Expense>("expenses");
});

var host = builder.Build();
host.Run();
