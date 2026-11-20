using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers;

/// <summary>
/// Página pública de ensayo por link compartido: /ensayo/play/{id}?key=...
/// Solo muestra el reproductor; el resto del admin sigue con login.
/// </summary>
[AllowAnonymous]
[Route("ensayo")]
public class EnsayoController : Controller
{
    private readonly MongoDbService _mongo;

    public EnsayoController(MongoDbService mongo) { _mongo = mongo; }

    [HttpGet("play/{id}")]
    public async Task<IActionResult> Play(string id)
    {
        var esperada = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Playback:ShareKey"];
        var recibida = Request.Query["key"].ToString();
        if (string.IsNullOrWhiteSpace(esperada) || recibida.Trim() != esperada.Trim())
            return Unauthorized("Falta la llave del link de ensayo (?key=...).");

        var m = await _mongo.GetCollection<Setlist>("setlists")
            .Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        return View("~/Views/Admin/SetlistPlay.cshtml", m);
    }
}
