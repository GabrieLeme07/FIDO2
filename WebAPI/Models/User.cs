using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace WebAPI.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; }
    public string Uuid { get; set; }
    public string UserName { get; set; }
    public string DisplayName { get; set; }
    public IEnumerable<Credential> Credentials { get; set; }
}