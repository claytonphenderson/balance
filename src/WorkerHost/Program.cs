using System.Threading.Channels;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using WorkerHost;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddHostedService<ExpenseWorker>();
builder.Services.AddSingleton(Channel.CreateUnbounded<IncomingExpenseEmail>());

// Mongo setup
ConventionRegistry.Register(
    "CamelCaseConvention",
    new ConventionPack
    {
        new CamelCaseElementNameConvention()
    },
    t => true // apply to all types
);
builder.Services.AddSingleton<MongoClient>(c=>
{
    var client = new MongoClient(builder.Configuration.GetSection("Database")["MongoUrl"]);
    return client;
});
builder.Services.AddSingleton<IMongoCollection<IncomingExpenseEmail>>(x =>
{
    var client = x.GetRequiredService<MongoClient>();
    var db = client.GetDatabase("balance");
    // creates if not existing
    db.CreateCollection("expenses", new CreateCollectionOptions
    {
        TimeSeriesOptions = new TimeSeriesOptions("date", "meta", TimeSeriesGranularity.Minutes)
    });

    return db.GetCollection<IncomingExpenseEmail>("expenses");
});

var host = builder.Build();
host.Run();
