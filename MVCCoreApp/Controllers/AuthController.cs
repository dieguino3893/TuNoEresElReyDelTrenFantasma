using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers;

public class AuthController : Controller
{
    private readonly MongoDbService _mongo;
    public AuthController(MongoDbService mongo) => _mongo = mongo;

    [HttpGet("auth/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect(returnUrl ?? "/admin");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        var col = _mongo.GetCollection<User>("users");
        var user = await col.Find(u => u.Username == username && !u.IsDeleted && u.IsActive).FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            ViewData["Error"] = "Usuario o contraseña incorrectos";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("UserId", user.Id)
        };
        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
        return Redirect(returnUrl ?? "/admin");
    }

    [HttpPost("auth/logout")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/auth/login");
    }

    [HttpGet("auth/logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/auth/login");
    }
}
