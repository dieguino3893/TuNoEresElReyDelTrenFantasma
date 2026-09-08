using MVCCoreApp.Models;

namespace MVCCoreApp.Dtos;

public record SiteSettingsDto(
    string Id,
    string SiteTitle,
    string Slogan,
    string HeroEyebrow,
    string HeroLocation,
    string HeroImageUrl,
    string BandTitle,
    string BandHighlight,
    string BandHighlightSub,
    string BandDescription,
    List<SocialLink> SocialLinks,
    string SpotifyArtistId,
    string SpotifyArtistUrl,
    string YouTubeChannelId,
    string YouTubeChannelUrl,
    string ContactEmail,
    string SeoTitle,
    string SeoDescription,
    DateTime UpdatedAt
);

public record UpdateSiteSettingsDto(
    string SiteTitle,
    string Slogan,
    string HeroEyebrow,
    string HeroLocation,
    string HeroImageUrl,
    string BandTitle,
    string BandHighlight,
    string BandHighlightSub,
    string BandDescription,
    List<SocialLink> SocialLinks,
    string SpotifyArtistId,
    string SpotifyArtistUrl,
    string YouTubeChannelId,
    string YouTubeChannelUrl,
    string ContactEmail,
    string SeoTitle,
    string SeoDescription
);
