using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WorkerHost;

public record IncomingExpenseEmail
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public required string Id { get; set; }
    public object Meta = new object { };
    public DateTime Date;
    public double Total;
    public string? Merchant;
    public required string RawSubject;
}