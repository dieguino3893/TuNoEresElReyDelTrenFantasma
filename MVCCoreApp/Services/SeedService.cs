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
        await EnsureSiteTextsAsync();
        await EnsureHeroChipsAsync();
        _logger.LogInformation("Estructura verificada: índices, textos del sitio y chips de portada.");
    }

    private async Task EnsureSiteTextsAsync()
    {
        var col = _mongo.GetCollection<SiteText>("siteTexts");
        var existing = await col.Find(_ => true).Project(t => t.Key).ToListAsync();
        var missing = SiteTextDefaults.All.Where(d => !existing.Contains(d.Key)).ToList();
        foreach (var d in missing)
            await col.InsertOneAsync(new SiteText { Key = d.Key, Group = d.Group, Label = d.Label, Value = d.Value, Multiline = d.Multiline });
        if (missing.Count > 0) _logger.LogInformation("Textos sembrados: {Count}", missing.Count);
    }

    private async Task EnsureHeroChipsAsync()
    {
        var col = _mongo.GetCollection<HeroChip>("heroChips");
        var count = await col.CountDocumentsAsync(x => !x.IsDeleted);
        if (count > 0) return;
        var settings = await _mongo.GetCollection<SiteSettings>("siteSettings").Find(x => x.Id == "main").FirstOrDefaultAsync();
        var first = string.IsNullOrWhiteSpace(settings?.HeroLocation) ? "En vivo" : settings.HeroLocation;
        var chips = new[]
        {
            new HeroChip { Text = first, Icon = "fa-solid fa-location-dot", Order = 0, IsVisible = true },
            new HeroChip { Text = "Estudio", Icon = "fa-solid fa-compact-disc", Order = 1, IsVisible = true },
            new HeroChip { Text = "Gira 2026", Icon = "fa-solid fa-route", Order = 2, IsVisible = true },
        };
        await col.InsertManyAsync(chips);
        _logger.LogInformation("Chips de portada sembrados.");
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