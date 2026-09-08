using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/site-settings")]
public class SiteSettingsController : ControllerBase
{
    private readonly MongoDbService _mongo;
    public SiteSettingsController(MongoDbService mongo) => _mongo = mongo;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<SiteSettingsDto>> Get()
    {
        try
        {
            var col = _mongo.GetCollection<SiteSettings>("siteSettings");
            var s = await col.Find(x => x.Id == "main").FirstOrDefaultAsync();
            if (s == null) return NotFound();
            return Ok(Map(s));
        }
        catch { return StatusCode(503); }
    }

    [HttpPut]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<SiteSettingsDto>> Update([FromBody] UpdateSiteSettingsDto dto)
    {
        var col = _mongo.GetCollection<SiteSettings>("siteSettings");
        var user = User.Identity?.Name ?? "system";
        var existing = await col.Find(x => x.Id == "main").FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        existing.SiteTitle = SanitizeService.SanitizeText(dto.SiteTitle);
        existing.Slogan = SanitizeService.SanitizeText(dto.Slogan);
        existing.HeroEyebrow = SanitizeService.SanitizeText(dto.HeroEyebrow);
        existing.HeroLocation = SanitizeService.SanitizeText(dto.HeroLocation);
        existing.HeroImageUrl = SanitizeService.SanitizeText(dto.HeroImageUrl);
        existing.BandTitle = SanitizeService.SanitizeText(dto.BandTitle);
        existing.BandHighlight = SanitizeService.SanitizeText(dto.BandHighlight);
        existing.BandHighlightSub = SanitizeService.SanitizeText(dto.BandHighlightSub);
        existing.BandDescription = SanitizeService.Sanitize(dto.BandDescription);
        existing.SocialLinks = dto.SocialLinks;
        existing.SpotifyArtistId = dto.SpotifyArtistId;
        existing.SpotifyArtistUrl = dto.SpotifyArtistUrl;
        existing.YouTubeChannelId = dto.YouTubeChannelId;
        existing.YouTubeChannelUrl = dto.YouTubeChannelUrl;
        existing.ContactEmail = dto.ContactEmail;
        existing.SeoTitle = SanitizeService.SanitizeText(dto.SeoTitle);
        existing.SeoDescription = SanitizeService.SanitizeText(dto.SeoDescription);
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = user;

        await col.ReplaceOneAsync(x => x.Id == "main", existing);
        return Ok(Map(existing));
    }

    private static SiteSettingsDto Map(SiteSettings s) => new(
        s.Id, s.SiteTitle, s.Slogan, s.HeroEyebrow, s.HeroLocation, s.HeroImageUrl, s.BandTitle, s.BandHighlight, s.BandHighlightSub, s.BandDescription,
        s.SocialLinks, s.SpotifyArtistId, s.SpotifyArtistUrl, s.YouTubeChannelId, s.YouTubeChannelUrl, s.ContactEmail, s.SeoTitle, s.SeoDescription, s.UpdatedAt);
}
