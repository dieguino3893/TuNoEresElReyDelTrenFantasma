using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/home")]
public class HomeDataController : ControllerBase
{
    private readonly MongoDbService _mongo;
    public HomeDataController(MongoDbService mongo) => _mongo = mongo;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetHome()
    {
        try
        {
            var settingsTask = _mongo.GetCollection<SiteSettings>("siteSettings").Find(x => x.Id == "main").FirstOrDefaultAsync();
            var membersTask = _mongo.GetCollection<Member>("members").Find(x => !x.IsDeleted && x.IsActive).SortBy(x => x.Order).ToListAsync();
            var showsTask = _mongo.GetCollection<Show>("shows").Find(x => !x.IsDeleted && x.IsVisible).SortBy(x => x.Date).ToListAsync();
            var newsTask = _mongo.GetCollection<News>("news").Find(x => !x.IsDeleted && x.IsPublished).SortBy(x => x.Order).ToListAsync();
            var tracksTask = _mongo.GetCollection<Track>("tracks").Find(x => !x.IsDeleted && x.IsVisible).SortBy(x => x.Order).ToListAsync();

            await Task.WhenAll(settingsTask, membersTask, showsTask, newsTask, tracksTask);

            var now = DateTime.UtcNow;
            var allShows = showsTask.Result ?? new List<Show>();
            var nextShow = allShows.Where(s => s.Date >= now).OrderBy(s => s.Date).FirstOrDefault();

            return Ok(new
            {
                settings = settingsTask.Result,
                members = membersTask.Result,
                nextShow,
                shows = allShows,
                news = newsTask.Result,
                tracks = tracksTask.Result,
                source = "mongodb"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { detail = "Mongo no disponible (whitelist/IP)." + ex.Message });
        }
    }
}
