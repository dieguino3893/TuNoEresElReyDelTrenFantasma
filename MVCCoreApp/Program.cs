using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using MVCCoreApp.Models;
using MVCCoreApp.Services;
using MVCCoreApp.Services.Auth;

// Carga .env si existe (para desarrollo local sin launchSettings)
var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
if (!File.Exists(envPath))
    envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;
        var key = trimmed[..idx].Trim();
        var value = trimmed[(idx + 1)..].Trim().Trim('"', '\'');
        Environment.SetEnvironmentVariable(key, value);
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configuración MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<SeedService>();
builder.Services.AddSingleton<IFileService, CloudinaryService>();
builder.Services.AddSingleton<IGeoLocationService, GeoLocationService>();
builder.Services.AddHttpClient("GeoIp", c => c.Timeout = TimeSpan.FromSeconds(3));

// CORS para front desacoplado (GitHub Pages)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("Pages", p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
    );
    opt.AddPolicy("PublicApi", p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
    );
});

// Auth: Cookies para Admin HTML + Basic para API
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "Cookies";
})
.AddCookie("Cookies", opt =>
{
    opt.LoginPath = "/auth/login";
    opt.AccessDeniedPath = "/auth/login";
    opt.ExpireTimeSpan = TimeSpan.FromHours(8);
    opt.SlidingExpiration = true;
    opt.Cookie.Name = "ElRey.Auth";
    opt.Cookie.HttpOnly = true;
    opt.Cookie.SameSite = SameSiteMode.Lax;
})
.AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("Basic", null)
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BasicOrCookie", policy =>
        policy.AddAuthenticationSchemes("Cookies", "Basic", "ApiKey").RequireAuthenticatedUser());
});

builder.Services.AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN");
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 10 * 1024 * 1024);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "El Rey del Tren Fantasma API", Version = "v1", Description = "API desacoplada para front GitHub Pages + Admin. Trazabilidad Mongo, CORS, Basic/ApiKey." });
    c.OperationFilter<SwaggerFileUploadFilter>();
    c.AddSecurityDefinition("basic", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "basic",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Basic Auth: usuario de la colección users en MongoDB",
    });
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "ApiKey por sesión: POST /api/v1/auth/api-keys (con sesión) y usa X-Api-Key: elrey_..."
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("basic", doc, null), new List<string>() },
        { new OpenApiSecuritySchemeReference("ApiKey", doc, null), new List<string>() }
    });
});

var app = builder.Build();

// Índices MongoDB al arrancar (no bloqueante). No crea usuarios ni contenido.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
        await seed.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Verificación de índices falló - verifica MongoDB connection");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ElRey API v1");
    c.RoutePrefix = "swagger";
});

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' https: data:; script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; connect-src 'self' https://res.cloudinary.com; frame-src https://open.spotify.com;";
    await next();
});
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("PublicApi");
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
