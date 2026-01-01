using System.Threading.Channels;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging.Console;
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
builder.Services.AddSingleton(Channel.CreateUnbounded<Expense>());
builder.Services.AddSingleton<ExpenseCategorizer>();
builder.Services.AddSingleton<ChatClient>(c =>
{
    var configuration = c.GetRequiredService<IConfiguration>();
    Uri openaiEndpoint = new Uri("https://clayt-mju9na87-eastus.cognitiveservices.azure.com/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2025-01-01-preview");
    // string apiKey = "";
    AzureOpenAIClient client = new(openaiEndpoint, new AzureKeyCredential(apiKey));
    return client.GetChatClient("gpt-4.1-mini");
});


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
