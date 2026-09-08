using MongoDB.Driver;
using MVCCoreApp.Models;

namespace MVCCoreApp.Services;

public class SeedService
{
    private readonly MongoDbService _mongo;
    private readonly ILogger<SeedService> _logger;

    public SeedService(MongoDbService mongo, ILogger<SeedService> logger)
    {
        _mongo = mongo;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await EnsureIndexesAsync();
        _logger.LogInformation("Índices de MongoDB verificados. Los usuarios y el contenido se gestionan solo en la base de datos.");
    }

    private async Task EnsureIndexesAsync()
    {
        var users = _mongo.GetCollection<User>("users");
        await users.Indexes.CreateOneAsync(new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.Username), new CreateIndexOptions { Unique = true }));

        var newsCol = _mongo.GetCollection<News>("news");
        await newsCol.Indexes.CreateOneAsync(new CreateIndexModel<News>(Builders<News>.IndexKeys.Ascending(x => x.Slug), new CreateIndexOptions { Unique = true }));
        await newsCol.Indexes.CreateOneAsync(new CreateIndexModel<News>(Builders<News>.IndexKeys.Descending(x => x.PublishedAt)));

        var shows = _mongo.GetCollection<Show>("shows");
        await shows.Indexes.CreateOneAsync(new CreateIndexModel<Show>(Builders<Show>.IndexKeys.Ascending(x => x.Date)));

        var members = _mongo.GetCollection<Member>("members");
        await members.Indexes.CreateOneAsync(new CreateIndexModel<Member>(Builders<Member>.IndexKeys.Ascending(x => x.Order)));

        var tracks = _mongo.GetCollection<Track>("tracks");
        await tracks.Indexes.CreateOneAsync(new CreateIndexModel<Track>(Builders<Track>.IndexKeys.Ascending(x => x.TrackNumber)));
        await tracks.Indexes.CreateOneAsync(new CreateIndexModel<Track>(Builders<Track>.IndexKeys.Ascending(x => x.ExternalId), new CreateIndexOptions { Unique = false, Sparse = true }));

        var subscribers = _mongo.GetCollection<Subscriber>("subscribers");
        await subscribers.Indexes.CreateOneAsync(new CreateIndexModel<Subscriber>(Builders<Subscriber>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true }));
    }
}