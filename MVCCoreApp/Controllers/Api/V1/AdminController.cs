using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly MongoDbService _mongo;
    private readonly SeedService _seed;
    public AdminController(MongoDbService mongo, SeedService seed) { _mongo = mongo; _seed = seed; }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public IActionResult Me() => Ok(new { username = User.Identity?.Name, role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value });

    [HttpPost("seed")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey", Roles = "Admin")]
    public async Task<IActionResult> Seed()
    {
        await _seed.SeedAsync();
        return Ok(new { message = "Seed completed" });
    }

    [HttpGet("stats")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<IActionResult> Stats()
    {
        var members = await _mongo.GetCollection<Member>("members").CountDocumentsAsync(x => !x.IsDeleted);
        var shows = await _mongo.GetCollection<Show>("shows").CountDocumentsAsync(x => !x.IsDeleted);
        var news = await _mongo.GetCollection<News>("news").CountDocumentsAsync(x => !x.IsDeleted);
        var tracks = await _mongo.GetCollection<Track>("tracks").CountDocumentsAsync(x => !x.IsDeleted);
        var users = await _mongo.GetCollection<User>("users").CountDocumentsAsync(x => !x.IsDeleted);
        return Ok(new { members, shows, news, tracks, users });
    }
}
