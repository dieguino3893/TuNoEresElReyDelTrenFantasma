using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;
using MVCCoreApp.Services.Auth;

namespace MVCCoreApp.Controllers;

public class AuthController : Controller
{
    private readonly MongoDbService _mongo;
    private readonly IntentosLogin _intentos;
    private readonly AuditoriaService _auditoria;
    public AuthController(MongoDbService mongo, IntentosLogin intentos, AuditoriaService auditoria)
    {
        _mongo = mongo;
        _intentos = intentos;
        _auditoria = auditoria;
    }

    [HttpGet("auth/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirigir(returnUrl);
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewData["Error"] = "Usuario o contraseña incorrectos";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        if (_intentos.EstaBloqueado(username))
        {
            await _auditoria.RegistrarAsync("login_bloqueado", $"IP {HttpContext.Connection.RemoteIpAddress}", username.Trim());
            Response.Headers.RetryAfter = "900";
            ViewData["Error"] = "Demasiados intentos. Espera 15 minutos.";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var col = _mongo.GetCollection<User>("users");
        var user = await col.Find(u => u.Username == username && !u.IsDeleted && u.IsActive).FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _intentos.RegistrarFallo(username);
            await _auditoria.RegistrarAsync("login_fallido", $"IP {HttpContext.Connection.RemoteIpAddress}", username.Trim());
            ViewData["Error"] = "Usuario o contraseña incorrectos";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        _intentos.RegistrarExito(username);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("UserId", user.Id)
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
        await _auditoria.RegistrarAsync("login", $"IP {HttpContext.Connection.RemoteIpAddress}", user.Username);
        return Redirigir(returnUrl);
    }

    [HttpPost("auth/logout")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/auth/login");
    }

    // returnUrl viene del query: solo se permiten rutas locales (anti phishing).
    private IActionResult Redirigir(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : Redirect("/admin");
}
