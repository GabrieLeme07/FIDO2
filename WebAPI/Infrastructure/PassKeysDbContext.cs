using MongoDB.Driver;

namespace WebAPI.Infrastructure;


public interface IPassKeysDbContext
{
    IMongoCollection<T> Collection<T>();

    IMongoCollection<T> Collection<T>(string collectionName);
}

public class PassKeysDbContext : IPassKeysDbContext
{
    private readonly IMongoDatabase _database;

    public IClientSessionHandle Session { get; set; }

    public PassKeysDbContext(IConfiguration configuration)
    {
        var mongoClient = new MongoClient(configuration["Database-ConnectionString"]);
        _database = mongoClient.GetDatabase(configuration["Database-Name"]);
    }

    public IMongoCollection<T> Collection<T>()
    {
        return _database.GetCollection<T>(typeof(T).Name);
    }

    public IMongoCollection<T> Collection<T>(string collectionName)
    {
        return _database.GetCollection<T>(collectionName);
    }

    public void Dispose()
    {
        Session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
