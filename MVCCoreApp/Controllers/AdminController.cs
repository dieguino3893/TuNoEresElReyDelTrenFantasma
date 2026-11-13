using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Bson;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Models.Enums;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers;

[Authorize(AuthenticationSchemes = "Cookies,Basic")]
[AutoValidateAntiforgeryToken]
[Route("admin")]
public class AdminController : Controller
{
    private readonly MongoDbService _mongo;
    private readonly SeedService _seed;
    private readonly IFileService _files;
    private readonly IConfiguration _config;
    private readonly AuditoriaService _auditoria;
    private readonly SitioPreviewService _sitioPreview;
    private readonly ILogger<AdminController> _log;
    public AdminController(MongoDbService mongo, SeedService seed, IFileService files, IConfiguration config, AuditoriaService auditoria, SitioPreviewService sitioPreview, ILogger<AdminController> log) { _mongo = mongo; _seed = seed; _files = files; _config = config; _auditoria = auditoria; _sitioPreview = sitioPreview; _log = log; }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.SiteUrl = _config["Frontend:PublicUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        try
        {
            var members = await _mongo.GetCollection<Member>("members").CountDocumentsAsync(x => !x.IsDeleted);
            var shows = await _mongo.GetCollection<Show>("shows").CountDocumentsAsync(x => !x.IsDeleted);
            var news = await _mongo.GetCollection<News>("news").CountDocumentsAsync(x => !x.IsDeleted);
            var tracks = await _mongo.GetCollection<Track>("tracks").CountDocumentsAsync(x => !x.IsDeleted);
            var users = await _mongo.GetCollection<User>("users").CountDocumentsAsync(x => !x.IsDeleted);
            ViewBag.Stats = new { members, shows, news, tracks, users };

            ViewBag.NextShow = (await _mongo.GetCollection<Show>("shows").Find(x => !x.IsDeleted && x.IsVisible && x.Date >= DateTime.UtcNow).SortBy(x => x.Date).Limit(1).ToListAsync()).FirstOrDefault();
            ViewBag.LatestNews = (await _mongo.GetCollection<News>("news").Find(x => !x.IsDeleted && x.IsPublished).SortByDescending(x => x.PublishedAt).Limit(1).ToListAsync()).FirstOrDefault();
        }
        catch
        {
            ViewBag.Stats = new { members = 0, shows = 0, news = 0, tracks = 0, users = 0 };
            ViewBag.NextShow = null;
            ViewBag.LatestNews = null;
            ViewBag.Warning = "Mongo no disponible";
        }
        return View();
    }

    // ===== MEMBERS =====
    [HttpGet("members")]
    public async Task<IActionResult> Members()
    {
        try { var list = await _mongo.GetCollection<Member>("members").Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync(); return View(list); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return View(new List<Member>()); }
    }
    [HttpGet("members/create")]
    public IActionResult MemberCreate() { ViewBag.IsEdit = false; return View("MemberForm", new Member()); }
    [HttpPost("members/create")]
    public async Task<IActionResult> MemberCreate(Member m, IFormFile? imageFile)
    {
        var user = User.Identity?.Name ?? "system";
        m.Id = ObjectId.GenerateNewId().ToString(); m.IsDeleted = false;
        if (imageFile != null && imageFile.Length > 0) { try { var (url,_) = await _files.UploadAsync(imageFile, "elrey/members"); m.ImageUrl = url; } catch(Exception ex){ TempData["err"] = "Upload Cloudinary falló: "+ex.Message; return View("MemberForm", m); } }
        m.Name = SanitizeService.Sanitize(m.Name); m.Role = SanitizeService.Sanitize(m.Role); m.ImageAlt = SanitizeService.Sanitize(m.ImageAlt);
        m.CreatedBy = user; m.UpdatedBy = user; m.CreatedAt = DateTime.UtcNow; m.UpdatedAt = DateTime.UtcNow;
        try { await _mongo.GetCollection<Member>("members").InsertOneAsync(m); TempData["ok"] = $"Miembro {m.Name} creado"; }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "Mongo no disponible (whitelist/IP): "+ex.Message; return View("MemberForm", m); }
        return RedirectToAction("Members");
    }
    [HttpGet("members/edit/{id}")]
    public async Task<IActionResult> MemberEdit(string id)
    {
        var col = _mongo.GetCollection<Member>("members");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        ViewBag.IsEdit = true;
        return View("MemberForm", m);
    }
    [HttpPost("members/edit/{id}")]
    public async Task<IActionResult> MemberEdit(string id, Member dto, IFormFile? imageFile)
    {
        var col = _mongo.GetCollection<Member>("members");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        if (imageFile != null && imageFile.Length > 0) { var (url,_) = await _files.UploadAsync(imageFile, "elrey/members"); dto.ImageUrl = url; }
        m.Name = SanitizeService.Sanitize(dto.Name); m.Role = SanitizeService.Sanitize(dto.Role); m.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? m.ImageUrl : dto.ImageUrl;
        m.ImageAlt = SanitizeService.Sanitize(dto.ImageAlt); m.ProfileUrl = dto.ProfileUrl; m.Order = dto.Order; m.IsActive = dto.IsActive;
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = $"Miembro {m.Name} actualizado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("MemberForm", dto); }
        return RedirectToAction("Members");
    }
    [HttpPost("members/delete/{id}")]
    public async Task<IActionResult> MemberDelete(string id)
    {
        var col = _mongo.GetCollection<Member>("members");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Miembro eliminado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; }
        return RedirectToAction("Members");
    }

    // ===== SHOWS =====
    [HttpGet("shows")]
    public async Task<IActionResult> Shows()
    {
        try
        {
            var list = await _mongo.GetCollection<Show>("shows").Find(x => !x.IsDeleted).SortBy(x => x.Date).ToListAsync();
            var reh = await _mongo.GetCollection<Rehearsal>("rehearsals").Find(x => !x.IsDeleted && x.ShowId != null).ToListAsync();
            ViewBag.RehCounts = reh.GroupBy(x => x.ShowId!).ToDictionary(g => g.Key, g => g.Count());
            return View(list);
        }
        catch
        {
            ViewBag.RehCounts = new Dictionary<string, int>();
            return View(new List<Show>());
        }
    }
    [HttpGet("shows/create")]
    public IActionResult ShowCreate() { ViewBag.IsEdit = false; return View("ShowForm", new Show{ Date = DateTime.UtcNow.AddDays(7), Time="21:00" }); }
    [HttpPost("shows/create")]
    public async Task<IActionResult> ShowCreate(Show s)
    {
        var user = User.Identity?.Name ?? "system";
        s.Id = ObjectId.GenerateNewId().ToString(); s.IsDeleted = false;
        s.CreatedBy = user; s.UpdatedBy = user; s.CreatedAt = DateTime.UtcNow; s.UpdatedAt = DateTime.UtcNow;
        try{ await _mongo.GetCollection<Show>("shows").InsertOneAsync(s); TempData["ok"] = $"Show {s.Venue} creado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("ShowForm", s); }
        return RedirectToAction("Shows");
    }
    [HttpGet("shows/edit/{id}")]
    public async Task<IActionResult> ShowEdit(string id)
    {
        var m = await LoadByIdAsync<Show>("shows", id);
        if (m == null) return NotFound();
        ViewBag.IsEdit = true;
        var (rehearsals, setlists) = await ShowRehearsalsAsync(id);
        ViewBag.ShowRehearsals = rehearsals;
        ViewBag.ShowSetlistNames = setlists;
        return View("ShowForm", m);
    }
    [HttpPost("shows/edit/{id}")]
    public async Task<IActionResult> ShowEdit(string id, Show dto)
    {
        var col = _mongo.GetCollection<Show>("shows");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.Date = dto.Date; m.Time = dto.Time; m.Venue = dto.Venue; m.CityState = dto.CityState; m.GoogleMapsUrl = dto.GoogleMapsUrl; m.IsVisible = dto.IsVisible; m.Order = dto.Order;
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Show actualizado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("ShowForm", dto); }
        return RedirectToAction("Shows");
    }
    [HttpPost("shows/delete/{id}")]
    public async Task<IActionResult> ShowDelete(string id)
    {
        var col = _mongo.GetCollection<Show>("shows");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m != null) { m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system"; try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Show eliminado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; } } else TempData["ok"]="Show eliminado";
        return RedirectToAction("Shows");
    }

    // ===== SETLISTS =====
    [HttpGet("setlists")]
    public async Task<IActionResult> Setlists()
    {
        try { var list = await _mongo.GetCollection<Setlist>("setlists").Find(x => !x.IsDeleted).SortBy(x => x.Name).ToListAsync(); return View(list); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return View(new List<Setlist>()); }
    }
    [HttpGet("setlists/create")]
    public async Task<IActionResult> SetlistCreate()
    {
        ViewBag.IsEdit = false;
        ViewBag.Tracks = await TrackCatalogAsync();
        return View("SetlistForm", new Setlist());
    }
    [HttpPost("setlists/create")]
    public async Task<IActionResult> SetlistCreate(Setlist s)
    {
        var user = User.Identity?.Name ?? "system";
        s.Id = ObjectId.GenerateNewId().ToString(); s.IsDeleted = false;
        s.Name = SanitizeService.SanitizeText(s.Name);
        s.Description = SanitizeService.SanitizeText(s.Description);
        NormalizeSetlistItems(s, await TrackCatalogAsync());
        s.CreatedBy = user; s.UpdatedBy = user; s.CreatedAt = DateTime.UtcNow; s.UpdatedAt = DateTime.UtcNow;
        try { await _mongo.GetCollection<Setlist>("setlists").InsertOneAsync(s); TempData["ok"] = $"Setlist \"{s.Name}\" guardado"; }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "No se pudo guardar (revisa tu conexión a la base)."; return View("SetlistForm", s); }
        return RedirectToAction("Setlists");
    }
    [HttpGet("setlists/edit/{id}")]
    public async Task<IActionResult> SetlistEdit(string id)
    {
        var m = await LoadByIdAsync<Setlist>("setlists", id);
        if (m == null) return NotFound();
        ViewBag.IsEdit = true;
        ViewBag.Tracks = await TrackCatalogAsync();
        return View("SetlistForm", m);
    }
    [HttpPost("setlists/edit/{id}")]
    public async Task<IActionResult> SetlistEdit(string id, Setlist dto)
    {
        var col = _mongo.GetCollection<Setlist>("setlists");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.Name = SanitizeService.SanitizeText(dto.Name);
        m.Description = SanitizeService.SanitizeText(dto.Description);
        m.Items = dto.Items ?? new List<SetlistItem>();
        NormalizeSetlistItems(m, await TrackCatalogAsync());
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try { await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Setlist actualizado"; }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "No se pudo actualizar (revisa tu conexión a la base)."; return View("SetlistForm", dto); }
        return RedirectToAction("Setlists");
    }
    [HttpPost("setlists/delete/{id}")]
    public async Task<IActionResult> SetlistDelete(string id)
    {
        var col = _mongo.GetCollection<Setlist>("setlists");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m != null) { m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system"; try { await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Setlist eliminado"; } catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "No se pudo eliminar (revisa tu conexión a la base)."; } }
        return RedirectToAction("Setlists");
    }

    // ===== PLAYER DE ENSAYO (uso interno: API resuelve, <audio> suena) =====
    [HttpGet("setlists/play/{id}")]
    public async Task<IActionResult> SetlistPlay(string id)
    {
        var m = await LoadByIdAsync<Setlist>("setlists", id);
        if (m == null) return NotFound();
        return View("SetlistPlay", m);
    }

    // ===== REHEARSALS =====
    [HttpGet("rehearsals")]
    public async Task<IActionResult> Rehearsals(string? show)
    {
        try
        {
            var list = await _mongo.GetCollection<Rehearsal>("rehearsals").Find(x => !x.IsDeleted).SortBy(x => x.Date).ToListAsync();
            var setlists = await _mongo.GetCollection<Setlist>("setlists").Find(x => !x.IsDeleted).ToListAsync();
            var shows = await _mongo.GetCollection<Show>("shows").Find(x => !x.IsDeleted).ToListAsync();
            ViewBag.Setlists = setlists.ToDictionary(x => x.Id, x => x.Name);
            ViewBag.Shows = shows.ToDictionary(x => x.Id, x => x);
            if (!string.IsNullOrWhiteSpace(show)) list = list.Where(x => x.ShowId == show).ToList();
            return View(list);
        }
        catch
        {
            ViewBag.Setlists = new Dictionary<string, string>();
            ViewBag.Shows = new Dictionary<string, Show>();
            return View(new List<Rehearsal>());
        }
    }
    [HttpGet("rehearsals/create")]
    public async Task<IActionResult> RehearsalCreate(string? showId)
    {
        ViewBag.IsEdit = false;
        ViewBag.SetlistOptions = await SetlistOptionsAsync(null);
        ViewBag.ShowOptions = await ShowOptionsAsync(showId);
        var r = new Rehearsal { Date = DateTime.UtcNow.AddDays(3) };
        if (!string.IsNullOrWhiteSpace(showId))
        {
            var show = await LoadShowAsync(showId);
            if (show != null) { r.ShowId = showId; r.Date = show.Date; }
        }
        ViewBag.ShowDataJson = await ShowDataJsonAsync(showId);
        return View("RehearsalForm", r);
    }
    [HttpPost("rehearsals/create")]
    public async Task<IActionResult> RehearsalCreate(Rehearsal r, string? kind, string? setlistId, string? showId, string? showNotes, string? standNotes)
    {
        var user = User.Identity?.Name ?? "system";
        r.SetlistId = string.IsNullOrWhiteSpace(setlistId) ? null : setlistId;
        r.Notes = SanitizeService.SanitizeText(kind == "show" ? showNotes : standNotes);
        if (kind == "show")
        {
            var show = await LoadShowAsync(showId);
            if (show == null) { r.ShowId = showId; TempData["err"] = "Elige un show para este ensayo"; ViewBag.IsEdit=false; ViewBag.SetlistOptions = await SetlistOptionsAsync(r.SetlistId); ViewBag.ShowOptions = await ShowOptionsAsync(showId); ViewBag.ShowDataJson = await ShowDataJsonAsync(showId); return View("RehearsalForm", r); }
            r.ShowId = show.Id; r.Date = show.Date;
            r.Place = string.IsNullOrWhiteSpace(show.CityState) ? show.Venue : $"{show.Venue} — {show.CityState}";
            r.StartTime = SanitizeService.SanitizeText(r.StartTime); r.EndTime = SanitizeService.SanitizeText(r.EndTime);
        }
        else
        {
            if (r.Date.Year < 2000) { TempData["err"] = "Elige la fecha y hora del ensayo suelto"; ViewBag.IsEdit=false; ViewBag.SetlistOptions = await SetlistOptionsAsync(r.SetlistId); ViewBag.ShowOptions = await ShowOptionsAsync(null); ViewBag.ShowDataJson = await ShowDataJsonAsync(null); return View("RehearsalForm", r); }
            r.ShowId = null; r.StartTime = null; r.EndTime = null;
            r.Place = SanitizeService.SanitizeText(r.Place);
        }
        r.CreatedAt = DateTime.UtcNow; r.UpdatedAt = DateTime.UtcNow; r.CreatedBy = user; r.UpdatedBy = user;
        try { await _mongo.GetCollection<Rehearsal>("rehearsals").InsertOneAsync(r); TempData["ok"] = kind == "show" ? "Ensayo del show guardado" : "Ensayo suelto guardado"; }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "No se pudo guardar (revisa tu conexión a la base)."; ViewBag.IsEdit=false; ViewBag.SetlistOptions = await SetlistOptionsAsync(r.SetlistId); ViewBag.ShowOptions = await ShowOptionsAsync(r.ShowId); ViewBag.ShowDataJson = await ShowDataJsonAsync(r.ShowId); return View("RehearsalForm", r); }
        return RedirectToAction("Rehearsals", new { show = r.ShowId });
    }
    [HttpGet("rehearsals/edit/{id}")]
    public async Task<IActionResult> RehearsalEdit(string id)
    {
        var m = await LoadByIdAsync<Rehearsal>("rehearsals", id);
        if (m == null) return NotFound();
        ViewBag.IsEdit = true;
        ViewBag.SetlistOptions = await SetlistOptionsAsync(m.SetlistId);
        ViewBag.ShowOptions = await ShowOptionsAsync(m.ShowId);
        ViewBag.ShowDataJson = await ShowDataJsonAsync(m.ShowId);
        return View("RehearsalForm", m);
    }
    [HttpPost("rehearsals/edit/{id}")]
    public async Task<IActionResult> RehearsalEdit(string id, Rehearsal dto, string? kind, string? setlistId, string? showId, string? showNotes, string? standNotes)
    {
        var col = _mongo.GetCollection<Rehearsal>("rehearsals");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.Notes = SanitizeService.SanitizeText(kind == "show" ? showNotes : standNotes);
        m.SetlistId = string.IsNullOrWhiteSpace(setlistId) ? null : setlistId;
        if (kind == "show")
        {
            var show = await LoadShowAsync(showId);
            if (show == null) { dto.ShowId = showId; TempData["err"] = "Elige un show para este ensayo"; ViewBag.IsEdit=true; ViewBag.SetlistOptions = await SetlistOptionsAsync(dto.SetlistId); ViewBag.ShowOptions = await ShowOptionsAsync(showId); ViewBag.ShowDataJson = await ShowDataJsonAsync(showId); return View("RehearsalForm", dto); }
            m.ShowId = show.Id; m.Date = show.Date;
            m.Place = string.IsNullOrWhiteSpace(show.CityState) ? show.Venue : $"{show.Venue} — {show.CityState}";
            m.StartTime = SanitizeService.SanitizeText(dto.StartTime); m.EndTime = SanitizeService.SanitizeText(dto.EndTime);
        }
        else
        {
            if (dto.Date.Year < 2000) { TempData["err"] = "Elige la fecha y hora del ensayo suelto"; ViewBag.IsEdit=true; ViewBag.SetlistOptions = await SetlistOptionsAsync(dto.SetlistId); ViewBag.ShowOptions = await ShowOptionsAsync(null); ViewBag.ShowDataJson = await ShowDataJsonAsync(null); return View("RehearsalForm", dto); }
            m.ShowId = null; m.StartTime = null; m.EndTime = null;
            m.Place = SanitizeService.SanitizeText(dto.Place);
        }
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try { await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Ensayo actualizado"; }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "No se pudo actualizar (revisa tu conexión a la base)."; ViewBag.IsEdit=true; ViewBag.SetlistOptions = await SetlistOptionsAsync(dto.SetlistId); ViewBag.ShowOptions = await ShowOptionsAsync(dto.ShowId); ViewBag.ShowDataJson = await ShowDataJsonAsync(dto.ShowId); return View("RehearsalForm", dto); }
        return RedirectToAction("Rehearsals");
    }
    [HttpPost("rehearsals/delete/{id}")]
    public async Task<IActionResult> RehearsalDelete(string id)
    {
        var col = _mongo.GetCollection<Rehearsal>("rehearsals");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m != null) { m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system"; try { await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Ensayo eliminado"; } catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "No se pudo eliminar (revisa tu conexión a la base)."; } }
        return RedirectToAction("Rehearsals");
    }

    private async Task<List<Show>> LoadShowsAsync()
    {
        try { return await _mongo.GetCollection<Show>("shows").Find(x => !x.IsDeleted).SortBy(x => x.Date).ToListAsync(); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return new List<Show>(); }
    }

    private async Task<T?> LoadByIdAsync<T>(string collection, string id) where T : Models.Base.AuditableDocument
    {
        try { return await _mongo.GetCollection<T>(collection).Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return default; }
    }

    private async Task<Show?> LoadShowAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _mongo.GetCollection<Show>("shows").Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
    }

    private async Task<List<SelectListItem>> ShowOptionsAsync(string? selectedId)
    {
        var es = new CultureInfo("es-ES");
        return (await LoadShowsAsync()).OrderBy(s => s.Date).Select(s => new SelectListItem
        {
            Value = s.Id,
            Text = $"{s.Venue} — {s.Date.ToString("dd MMM yyyy", es)}",
            Selected = s.Id == selectedId
        }).ToList();
    }

    private async Task<string> ShowDataJsonAsync(string? selectedId)
    {
        var es = new CultureInfo("es-ES");
        var data = (await LoadShowsAsync()).Select(s => new { id = s.Id, venue = s.Venue, date = s.Date.ToString("D", es) }).ToList();
        return System.Text.Json.JsonSerializer.Serialize(data);
    }

    private async Task<(List<Rehearsal> list, Dictionary<string, string> setlists)> ShowRehearsalsAsync(string showId)
    {
        try
        {
            var list = await _mongo.GetCollection<Rehearsal>("rehearsals").Find(x => !x.IsDeleted && x.ShowId == showId).SortBy(x => x.Date).ToListAsync();
            var setlists = await _mongo.GetCollection<Setlist>("setlists").Find(x => !x.IsDeleted).ToListAsync();
            return (list, setlists.ToDictionary(x => x.Id, x => x.Name));
        }
        catch
        {
            return (new List<Rehearsal>(), new Dictionary<string, string>());
        }
    }

    private async Task<List<Track>> TrackCatalogAsync()
    {
        try { return await _mongo.GetCollection<Track>("tracks").Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync(); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return new List<Track>(); }
    }

    private async Task<SelectList?> SetlistOptionsAsync(string? selectedId)
    {
        try
        {
            var all = await _mongo.GetCollection<Setlist>("setlists").Find(x => !x.IsDeleted).SortBy(x => x.Name).ToListAsync();
            return new SelectList(all, "Id", "Name", selectedId);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return new SelectList(new List<Setlist>(), "Id", "Name", selectedId); }
    }

    private void NormalizeSetlistItems(Setlist s, List<Track> catalog)
    {
        s.Items = s.Items?.Where(x => !string.IsNullOrWhiteSpace(x.Title)).Select(item =>
        {
            var it = new SetlistItem { Title = SanitizeService.SanitizeText(item.Title) };
            var track = catalog.FirstOrDefault(t => t.Id == item.TrackId);
            if (track != null) { it.TrackId = track.Id; it.Source = "Track"; it.Title = track.Title; }
            else { it.TrackId = null; it.Source = "Custom"; }
            return it;
        }).ToList() ?? new List<SetlistItem>();
    }

    // ===== NEWS =====
    [HttpGet("news")]
    public async Task<IActionResult> News()
    {
        try { var list = await _mongo.GetCollection<News>("news").Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync(); return View(list); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return View(new List<News>()); }
    }
    [HttpGet("news/create")]
    public IActionResult NewsCreate() { ViewBag.IsEdit = false; return View("NewsForm", new News{ PublishedAt = DateTime.UtcNow }); }
    [HttpPost("news/create")]
    public async Task<IActionResult> NewsCreate(News n, IFormFile? imageFile)
    {
        var user = User.Identity?.Name ?? "system";
        n.Id = ObjectId.GenerateNewId().ToString(); n.IsDeleted = false;
        if (imageFile != null && imageFile.Length > 0) { var (url,_) = await _files.UploadAsync(imageFile, "elrey/news"); n.ImageUrl = url; }
        if (string.IsNullOrWhiteSpace(n.Slug)) n.Slug = SanitizeService.GenerarSlug(n.Title);
        n.Title = SanitizeService.Sanitize(n.Title); n.Excerpt = SanitizeService.Sanitize(n.Excerpt); n.ContentHtml = SanitizeService.Sanitize(n.ContentHtml);
        n.CreatedBy = user; n.UpdatedBy = user; n.CreatedAt = DateTime.UtcNow; n.UpdatedAt = DateTime.UtcNow;
        try{ await _mongo.GetCollection<News>("news").InsertOneAsync(n); TempData["ok"] = $"Noticia {n.Title} creada"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("NewsForm", n); }
        return RedirectToAction("News");
    }
    [HttpGet("news/edit/{id}")]
    public async Task<IActionResult> NewsEdit(string id)
    {
        var m = await _mongo.GetCollection<News>("news").Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        ViewBag.IsEdit = true;
        return View("NewsForm", m);
    }
    [HttpPost("news/edit/{id}")]
    public async Task<IActionResult> NewsEdit(string id, News dto, IFormFile? imageFile)
    {
        var col = _mongo.GetCollection<News>("news");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        if (imageFile != null && imageFile.Length > 0) { var (url,_) = await _files.UploadAsync(imageFile, "elrey/news"); dto.ImageUrl = url; }
        m.Title = SanitizeService.Sanitize(dto.Title); m.Slug = string.IsNullOrWhiteSpace(dto.Slug) ? SanitizeService.GenerarSlug(dto.Title) : SanitizeService.GenerarSlug(dto.Slug); m.Tag = dto.Tag; m.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? m.ImageUrl : dto.ImageUrl;
        m.ImageAlt = dto.ImageAlt; m.Excerpt = SanitizeService.Sanitize(dto.Excerpt); m.ContentHtml = SanitizeService.Sanitize(dto.ContentHtml); m.PublishedAt = dto.PublishedAt; m.IsPublished = dto.IsPublished; m.Order = dto.Order;
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Noticia actualizada"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("NewsForm", dto); }
        return RedirectToAction("News");
    }
    [HttpPost("news/delete/{id}")]
    public async Task<IActionResult> NewsDelete(string id)
    {
        var col = _mongo.GetCollection<News>("news");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m != null) { m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system"; try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Noticia eliminada"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; } } else TempData["ok"]="Noticia eliminada";
        return RedirectToAction("News");
    }

    // ===== TRACKS =====
    [HttpGet("tracks")]
    public async Task<IActionResult> Tracks()
    {
        try { var list = await _mongo.GetCollection<Track>("tracks").Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync(); return View(list); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return View(new List<Track>()); }
    }
    [HttpGet("tracks/create")]
    public IActionResult TrackCreate() { ViewBag.IsEdit = false; return View("TrackForm", new Track()); }
    [HttpPost("tracks/create")]
    public async Task<IActionResult> TrackCreate(Track t)
    {
        var user = User.Identity?.Name ?? "system";
        t.Id = ObjectId.GenerateNewId().ToString(); t.IsDeleted = false;
        t.Source = "Manual"; t.CreatedBy = user; t.UpdatedBy = user; t.CreatedAt = DateTime.UtcNow; t.UpdatedAt = DateTime.UtcNow;
        try{ await _mongo.GetCollection<Track>("tracks").InsertOneAsync(t); TempData["ok"] = $"Track {t.Title} creado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("TrackForm", t); }
        return RedirectToAction("Tracks");
    }
    [HttpGet("tracks/edit/{id}")]
    public async Task<IActionResult> TrackEdit(string id)
    {
        var m = await _mongo.GetCollection<Track>("tracks").Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        ViewBag.IsEdit = true;
        return View("TrackForm", m);
    }
    [HttpPost("tracks/edit/{id}")]
    public async Task<IActionResult> TrackEdit(string id, Track dto)
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.Title = dto.Title; m.TrackNumber = dto.TrackNumber; m.Type = dto.Type; m.Links = dto.Links; m.ExternalId = dto.ExternalId; m.IsVisible = dto.IsVisible; m.Order = dto.Order;
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Track actualizado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("TrackForm", dto); }
        return RedirectToAction("Tracks");
    }
    [HttpPost("tracks/delete/{id}")]
    public async Task<IActionResult> TrackDelete(string id)
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m != null) { m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system"; try{ await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Track eliminado"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; } } else TempData["ok"]="Track eliminado";
        return RedirectToAction("Tracks");
    }

    // ===== HERO CHIPS (sellos de la portada) =====
    [HttpGet("chips")]
    public async Task<IActionResult> Chips()
    {
        try { var list = await _mongo.GetCollection<HeroChip>("heroChips").Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync(); return View(list); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return View(new List<HeroChip>()); }
    }
    [HttpGet("chips/create")]
    public IActionResult ChipCreate() { ViewBag.IsEdit = false; return View("ChipForm", new HeroChip()); }
    [HttpPost("chips/create")]
    public async Task<IActionResult> ChipCreate(HeroChip c)
    {
        var user = User.Identity?.Name ?? "system";
        c.Id = ObjectId.GenerateNewId().ToString(); c.IsDeleted = false;
        c.Text = SanitizeService.SanitizeText(c.Text); c.Icon = SanitizeService.SanitizeText(c.Icon);
        c.CreatedBy = user; c.UpdatedBy = user; c.CreatedAt = DateTime.UtcNow; c.UpdatedAt = DateTime.UtcNow;
        try { await _mongo.GetCollection<HeroChip>("heroChips").InsertOneAsync(c); TempData["ok"] = $"Chip {c.Text} creado"; }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "Mongo no disponible: "+ex.Message; return View("ChipForm", c); }
        return RedirectToAction("Chips");
    }
    [HttpGet("chips/edit/{id}")]
    public async Task<IActionResult> ChipEdit(string id)
    {
        var m = await LoadByIdAsync<HeroChip>("heroChips", id);
        if (m == null) return NotFound();
        ViewBag.IsEdit = true;
        return View("ChipForm", m);
    }
    [HttpPost("chips/edit/{id}")]
    public async Task<IActionResult> ChipEdit(string id, HeroChip dto)
    {
        var col = _mongo.GetCollection<HeroChip>("heroChips");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.Text = SanitizeService.SanitizeText(dto.Text); m.Icon = SanitizeService.SanitizeText(dto.Icon);
        m.Order = dto.Order; m.IsVisible = dto.IsVisible;
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        try { await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Chip actualizado"; }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "Mongo no disponible: "+ex.Message; return View("ChipForm", dto); }
        return RedirectToAction("Chips");
    }
    [HttpPost("chips/delete/{id}")]
    public async Task<IActionResult> ChipDelete(string id)
    {
        var col = _mongo.GetCollection<HeroChip>("heroChips");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m != null) { m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system"; try { await col.ReplaceOneAsync(x => x.Id == id, m); TempData["ok"] = "Chip eliminado"; } catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "Mongo no disponible: "+ex.Message; } }
        else TempData["ok"] = "Chip eliminado";
        return RedirectToAction("Chips");
    }

    // ===== TEXTOS DEL SITIO (clave-valor por grupos) =====
    [HttpGet("texts")]
    public async Task<IActionResult> Texts()
    {
        var col = _mongo.GetCollection<SiteText>("siteTexts");
        Dictionary<string, SiteText> current;
        try { current = (await col.Find(_ => true).ToListAsync()).GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First()); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); current = new Dictionary<string, SiteText>(); }
        var model = SiteTextDefaults.All.Select(d => current.TryGetValue(d.Key, out var t)
            ? t
            : new SiteText { Key = d.Key, Group = d.Group, Label = d.Label, Value = d.Value, Multiline = d.Multiline }).ToList();
        return View(model);
    }
    [HttpPost("texts")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public async Task<IActionResult> Texts(Dictionary<string, string> values)
    {
        var col = _mongo.GetCollection<SiteText>("siteTexts");
        var user = User.Identity?.Name ?? "system";
        var now = DateTime.UtcNow;
        try
        {
            foreach (var d in SiteTextDefaults.All)
            {
                if (values == null || !values.TryGetValue(d.Key, out var v) || v == null) continue;
                var clean = d.Multiline ? SanitizeService.Sanitize(v) : SanitizeService.SanitizeText(v);
                await col.ReplaceOneAsync(t => t.Key == d.Key,
                    new SiteText { Key = d.Key, Group = d.Group, Label = d.Label, Value = clean, Multiline = d.Multiline, UpdatedAt = now, UpdatedBy = user },
                    new ReplaceOptions { IsUpsert = true });
            }
            TempData["ok"] = "Textos guardados";
        }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); TempData["err"] = "Mongo no disponible: "+ex.Message; }
        return RedirectToAction("Texts");
    }

    // ===== SETTINGS =====
    [HttpGet("settings")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public async Task<IActionResult> Settings()
    {
        try 
        { 
            var s = await _mongo.GetCollection<SiteSettings>("siteSettings").Find(x => x.Id == "main").FirstOrDefaultAsync();
            if (s == null) 
            {
                s = new SiteSettings{ Id="main" };
            }
            // Ensure social links are initialized
            if (s.SocialLinks == null || s.SocialLinks.Count == 0)
            {
                s.SocialLinks = new List<SocialLink>();
                var order = 0;
                foreach (SocialPlatform platform in Enum.GetValues(typeof(SocialPlatform)))
                {
                    string iconClass = platform switch
                    {
                        SocialPlatform.Facebook => "fa-brands fa-facebook",
                        SocialPlatform.Instagram => "fa-brands fa-instagram",
                        SocialPlatform.Spotify => "fa-brands fa-spotify",
                        SocialPlatform.AppleMusic => "fa-brands fa-apple",
                        SocialPlatform.TikTok => "fa-brands fa-tiktok",
                        SocialPlatform.YouTube => "fa-brands fa-youtube",
                        _ => ""
                    };
                    s.SocialLinks.Add(new SocialLink
                    {
                        Platform = platform,
                        IconClass = iconClass,
                        Url = string.Empty,
                        ShowInHero = true,
                        ShowInFooter = true,
                        IsActive = true,
                        Order = order++
                    });
                }
            }
            return View(s);
        }
        catch 
        { 
            SiteSettings? settings = null; 
            return View(settings); 
        }
    }
    [HttpPost("settings")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public async Task<IActionResult> Settings(SiteSettings dto)
    {
        var col = _mongo.GetCollection<SiteSettings>("siteSettings");
        var s = await col.Find(x => x.Id == "main").FirstOrDefaultAsync();
        if (s == null) s = new SiteSettings{ Id="main" };
        s.SiteTitle = SanitizeService.SanitizeText(dto.SiteTitle); s.Slogan = SanitizeService.SanitizeText(dto.Slogan); s.HeroEyebrow = SanitizeService.SanitizeText(dto.HeroEyebrow); s.HeroLocation = SanitizeService.SanitizeText(dto.HeroLocation);
        s.HeroTitle = SanitizeService.SanitizeText(dto.HeroTitle); s.HeroTitleAccent = SanitizeService.SanitizeText(dto.HeroTitleAccent);
        s.HeroImageZoom = dto.HeroImageZoom; s.HeroImagePosX = dto.HeroImagePosX; s.HeroImagePosY = dto.HeroImagePosY;
        s.BandTitle = SanitizeService.SanitizeText(dto.BandTitle); s.BandHighlight = SanitizeService.SanitizeText(dto.BandHighlight); s.BandHighlightSub = SanitizeService.SanitizeText(dto.BandHighlightSub); s.BandDescription = SanitizeService.Sanitize(dto.BandDescription);
        s.HeroImageUrl = SanitizeService.SanitizeText(dto.HeroImageUrl); s.ContactEmail = dto.ContactEmail; s.SpotifyArtistId = dto.SpotifyArtistId; s.SpotifyArtistUrl = dto.SpotifyArtistUrl;
        s.YouTubeChannelId = dto.YouTubeChannelId; s.YouTubeChannelUrl = dto.YouTubeChannelUrl;
        s.SeoTitle = SanitizeService.SanitizeText(dto.SeoTitle); s.SeoDescription = SanitizeService.SanitizeText(dto.SeoDescription); s.SocialLinks = dto.SocialLinks;
        s.NextShowVisible = dto.NextShowVisible; s.BandVisible = dto.BandVisible; s.ShowsVisible = dto.ShowsVisible; s.BlogVisible = dto.BlogVisible;
        s.NewsletterVisible = dto.NewsletterVisible; s.MusicVisible = dto.MusicVisible; s.ContactVisible = dto.ContactVisible;
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = User.Identity?.Name ?? "system";
        try{ await col.ReplaceOneAsync(x => x.Id == "main", s, new ReplaceOptions{ IsUpsert = true }); TempData["ok"] = "Ajustes guardados"; } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("Settings", dto); }
        return RedirectToAction("Settings");
    }

    // ===== EDITOR DEL SITIO =====
    [HttpGet("sitio")]
    public IActionResult Sitio()
    {
        ViewBag.SiteUrl = _config["Frontend:PublicUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        return View();
    }

    [HttpGet("sitio/lienzo")]
    public async Task<IActionResult> SitioLienzo()
    {
        try { return Content(await _sitioPreview.ObtenerAsync($"{Request.Scheme}://{Request.Host}"), "text/html; charset=utf-8"); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Editor del sitio: no se pudo traer el HTML publicado");
            return Content("<p style=\"font:14px system-ui;color:#a3a3a3;padding:24px\">No se pudo cargar el sitio publicado. Revisa Frontend:PublicUrl.</p>", "text/html; charset=utf-8");
        }
    }

    [HttpPost("sitio")]
    public async Task<IActionResult> GuardarSitio([FromBody] SitioCambios cambios)
    {
        var user = User.Identity?.Name ?? "system";
        var now = DateTime.UtcNow;
        try
        {
            if (cambios.Textos.Count > 0)
            {
                var textos = _mongo.GetCollection<SiteText>("siteTexts");
                foreach (var (key, valor) in cambios.Textos)
                {
                    var d = SiteTextDefaults.All.FirstOrDefault(x => x.Key == key);
                    if (d == null) continue;
                    var limpio = d.Multiline ? SanitizeService.Sanitize(valor) : SanitizeService.SanitizeText(valor);
                    await textos.ReplaceOneAsync(t => t.Key == key,
                        new SiteText { Key = key, Group = d.Group, Label = d.Label, Value = limpio, Multiline = d.Multiline, UpdatedAt = now, UpdatedBy = user },
                        new ReplaceOptions { IsUpsert = true });
                }
            }

            if (cambios.Ajustes.Count > 0 || cambios.Secciones.Count > 0)
            {
                var col = _mongo.GetCollection<SiteSettings>("siteSettings");
                var s = await col.Find(x => x.Id == "main").FirstOrDefaultAsync() ?? new SiteSettings { Id = "main" };
                foreach (var (campo, valor) in cambios.Ajustes)
                {
                    switch (campo)
                    {
                        case nameof(SiteSettings.HeroEyebrow): s.HeroEyebrow = SanitizeService.SanitizeText(valor); break;
                        case nameof(SiteSettings.HeroTitle): s.HeroTitle = SanitizeService.SanitizeText(valor); break;
                        case nameof(SiteSettings.HeroTitleAccent): s.HeroTitleAccent = SanitizeService.SanitizeText(valor); break;
                        case nameof(SiteSettings.Slogan): s.Slogan = SanitizeService.SanitizeText(valor); break;
                        case nameof(SiteSettings.BandTitle): s.BandTitle = SanitizeService.SanitizeText(valor); break;
                        case nameof(SiteSettings.BandHighlight): s.BandHighlight = SanitizeService.SanitizeText(valor); break;
                        case nameof(SiteSettings.BandHighlightSub): s.BandHighlightSub = SanitizeService.SanitizeText(valor); break;
                        case nameof(SiteSettings.BandDescription): s.BandDescription = SanitizeService.Sanitize(valor); break;
                    }
                }
                foreach (var (campo, visible) in cambios.Secciones)
                {
                    switch (campo)
                    {
                        case nameof(SiteSettings.NextShowVisible): s.NextShowVisible = visible; break;
                        case nameof(SiteSettings.BandVisible): s.BandVisible = visible; break;
                        case nameof(SiteSettings.ShowsVisible): s.ShowsVisible = visible; break;
                        case nameof(SiteSettings.BlogVisible): s.BlogVisible = visible; break;
                        case nameof(SiteSettings.NewsletterVisible): s.NewsletterVisible = visible; break;
                        case nameof(SiteSettings.MusicVisible): s.MusicVisible = visible; break;
                        case nameof(SiteSettings.ContactVisible): s.ContactVisible = visible; break;
                    }
                }
                s.UpdatedAt = now; s.UpdatedBy = user;
                await col.ReplaceOneAsync(x => x.Id == "main", s, new ReplaceOptions { IsUpsert = true });
            }

            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Editor del sitio: no se pudo guardar");
            return StatusCode(503, new { detail = "Mongo no disponible, intenta de nuevo." });
        }
    }

    [HttpGet("files")]
    public IActionResult Files() => View();

    [HttpGet("users")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public async Task<IActionResult> Users()
    {
        try { var list = await _mongo.GetCollection<User>("users").Find(x => !x.IsDeleted).ToListAsync(); return View(list); }
        catch (Exception ex) { _log.LogWarning(ex, "Admin: operación falló"); return View(new List<User>()); }
    }
    [HttpGet("users/create")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public IActionResult UserCreate() => View("UserForm", new User());
    [HttpPost("users/create")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public async Task<IActionResult> UserCreate(User u, string password)
    {
        if (string.IsNullOrWhiteSpace(password)) { TempData["err"]="Password requerido"; return View("UserForm", u); }
        if (password.Length < 10) { TempData["err"]="Mínimo 10 caracteres"; return View("UserForm", u); }
        u.Id = ObjectId.GenerateNewId().ToString(); u.IsDeleted = false;
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        u.CreatedBy = User.Identity?.Name ?? "system"; u.UpdatedBy = User.Identity?.Name ?? "system"; u.CreatedAt=DateTime.UtcNow; u.UpdatedAt=DateTime.UtcNow;
        try{ await _mongo.GetCollection<User>("users").InsertOneAsync(u); TempData["ok"]="Usuario creado"; await _auditoria.RegistrarAsync("usuario_creado", u.Username); } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; return View("UserForm", u); }
        return RedirectToAction("Users");
    }
    [HttpPost("users/delete/{id}")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public async Task<IActionResult> UserDelete(string id)
    {
        var col = _mongo.GetCollection<User>("users");
        var u = await col.Find(x=>x.Id==id).FirstOrDefaultAsync();
        if(u!=null){ u.IsDeleted=true; u.UpdatedAt=DateTime.UtcNow; u.UpdatedBy=User.Identity?.Name??"system"; try{ await col.ReplaceOneAsync(x=>x.Id==id,u); TempData["ok"]="Usuario eliminado"; await _auditoria.RegistrarAsync("usuario_eliminado", u.Username); } catch(Exception ex){ TempData["err"]="Mongo no disponible: "+ex.Message; } } else TempData["ok"]="Usuario eliminado";
        return RedirectToAction("Users");
    }

    [HttpPost("seed")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic", Roles = "Admin")]
    public async Task<IActionResult> Seed()
    {
        await _seed.SeedAsync();
        TempData["ok"] = "Estructura verificada (textos y chips incluidos)";
        return RedirectToAction("Index");
    }
}
