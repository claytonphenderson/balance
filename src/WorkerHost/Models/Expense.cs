using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WorkerHost;

public class Expense
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public required string Id { get; init; }
    public required DateTime Date { get; init; }
    public required double Total { get; init; }
    public string? Merchant { get; init; }
    public required string RawSubject { get; init; }
    public required DateTime IngressDate { get; init; }
    public string? Category { get; set; }
    public DateTime? CategorizedDate { get; set; }
}