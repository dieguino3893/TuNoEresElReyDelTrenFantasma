using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using MongoDB.Bson.Serialization.Conventions;
using MVCCoreApp.Models;
using MVCCoreApp.Services;
using MVCCoreApp.Services.Asistente;
using MVCCoreApp.Services.Auth;
using MVCCoreApp.Services.Playback;
using YoutubeExplode;

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
        // Un valor vacío no debe pisar lo que ya trae appsettings.json.
        if (value.Length == 0) continue;
        Environment.SetEnvironmentVariable(key, value);
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configuración MongoDB
// shortcut: los documentos viejos pueden traer campos ya eliminados del código;
// se ignoran en vez de romper la lectura (el driver por defecto lanza error).
ConventionRegistry.Register("IgnorarCamposViejos",
    new ConventionPack { new IgnoreExtraElementsConvention(true) }, _ => true);
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<SeedService>();
builder.Services.AddSingleton<IFileService, CloudinaryService>();
builder.Services.AddSingleton<IGeoLocationService, GeoLocationService>();
builder.Services.AddHttpClient("GeoIp", c => c.Timeout = TimeSpan.FromSeconds(3));

// Detrás de proxy/IIS: respeta X-Forwarded-For/Proto para IP real y esquema.
// Ojo: se acepta el XFF que mande el cliente; un atacante puede rotarlo para
// esquivar el rate limit por IP (el bloqueo por usuario de IntentosLogin sí lo frena).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Services.AddSingleton<LimiteAsistente>();
builder.Services.AddSingleton<IntentosLogin>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<AsistenteCliente>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<AsistenteDatos>();
builder.Services.AddScoped<AsistenteEjecutor>();
builder.Services.AddScoped<AsistenteServicio>();

// Módulo playback (ensayo interno): API resuelve, <audio> del navegador suena.
// Aislado tras IAudioResolver para que un cambio en la API no oficial no rompa setlists.
builder.Services.AddMemoryCache();
builder.Services.Configure<PlaybackOptions>(builder.Configuration.GetSection("Playback"));
builder.Services.AddSingleton<YoutubeClient>(_ =>
{
    // Playback:ProxyUrl permite resolver el audio desde una IP no bloqueada por YouTube.
    var proxyUrl = builder.Configuration["Playback:ProxyUrl"];
    HttpClient http = new();
    if (!string.IsNullOrWhiteSpace(proxyUrl))
    {
        var proxy = new System.Net.WebProxy(proxyUrl);
        if (Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            proxy.Credentials = new System.Net.NetworkCredential(parts[0], parts.Length > 1 ? parts[1] : "");
        }
        http = new HttpClient(new SocketsHttpHandler { Proxy = proxy, UseProxy = true });
    }

    // Playback:YouTubeCookies acepta cookies.txt (Netscape) o un header "SID=...; HSID=...".
    var cookies = new List<System.Net.Cookie>();
    var cookieRaw = builder.Configuration["Playback:YouTubeCookies"];
    if (!string.IsNullOrWhiteSpace(cookieRaw))
    {
        foreach (var linea in cookieRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var l = linea.TrimEnd('\r').Trim();
            if (l.Length == 0) continue;
            if (l.StartsWith("#HttpOnly_", StringComparison.Ordinal)) l = l["#HttpOnly_".Length..];
            else if (l.StartsWith("#")) continue;

            var campos = l.Split('\t');
            if (campos.Length >= 7)
            {
                var dominio = campos[0].Trim();
                if (!dominio.StartsWith('.')) dominio = "." + dominio;
                var ruta = string.IsNullOrWhiteSpace(campos[2]) ? "/" : campos[2];
                try { cookies.Add(new System.Net.Cookie(campos[5].Trim(), campos[6].Trim(), ruta, dominio)); }
                catch { /* cookie inválida, se omite */ }
                continue;
            }

            foreach (var part in l.Split(';'))
            {
                var kv = part.Trim();
                var idx = kv.IndexOf('=');
                if (idx <= 0) continue;
                var nombre = kv[..idx].Trim();
                if (nombre.Equals("Path", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Equals("Domain", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Equals("Expires", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Equals("Secure", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Equals("HttpOnly", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Equals("SameSite", StringComparison.OrdinalIgnoreCase)) continue;
                try { cookies.Add(new System.Net.Cookie(nombre, kv[(idx + 1)..].Trim(), "/", ".youtube.com")); }
                catch { /* cookie inválida, se omite */ }
            }
        }
    }

    return cookies.Count > 0 ? new YoutubeClient(http, cookies) : new YoutubeClient(http);
});
builder.Services.AddHttpClient("Saavn", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("PlaybackRemote", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient("Deezer", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<SitioPreviewService>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<YoutubeExplodeResolver>();
builder.Services.AddSingleton<SaavnFallbackResolver>();
builder.Services.AddSingleton<DeezerArlClient>();
builder.Services.AddSingleton<AudioResolverFactory>();

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
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null)
.AddScheme<AuthenticationSchemeOptions, ShareKeyAuthHandler>("ShareKey", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BasicOrCookie", policy =>
        policy.AddAuthenticationSchemes("Cookies", "Basic", "ApiKey", "ShareKey").RequireAuthenticatedUser());
});

builder.Services.AddAntiforgery(o => o.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = false;
});
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Login: 10 por minuto por IP.
    opciones.AddPolicy("login", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    // Asistente: 30 por minuto por usuario (ventana fija) + 3 por segundo y
    // 300 por hora por usuario (contadores en memoria, ver LimiteAsistente).
    opciones.AddPolicy("asistente", contexto =>
    {
        var clave = contexto.User?.Identity?.Name
            ?? contexto.Connection.RemoteIpAddress?.ToString()
            ?? "desconocida";
        return RateLimitPartition.GetFixedWindowLimiter(
            clave,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
    opciones.OnRejected = async (contexto, cancelacion) =>
    {
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        contexto.HttpContext.Response.ContentType = "application/json";
        await contexto.HttpContext.Response.WriteAsJsonAsync(
            new { mensaje = "Demasiados intentos. Espera un momento." }, cancelacion);
    };
});
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
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ElRey API v1");
        c.RoutePrefix = "swagger";
    });
}

app.Use(async (ctx, next) =>
{
    // El editor del sitio mete el HTML del front (otro origen) en un iframe del panel.
    var esEditor = ctx.Request.Path.StartsWithSegments("/admin/sitio");
    var esLienzo = ctx.Request.Path.StartsWithSegments("/admin/sitio/lienzo");
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = esEditor ? "SAMEORIGIN" : "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Content-Security-Policy"] = esLienzo
        ? "default-src 'self' http: https: data: 'unsafe-inline'; frame-src https://open.spotify.com https://www.youtube.com;"
        : "default-src 'self'; img-src 'self' https: data:; media-src 'self' https: blob:; script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://www.youtube.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; connect-src 'self' https://res.cloudinary.com https://*.googlevideo.com https://*.youtube.com https://*.saavncdn.com https://www.jiosaavn.com; frame-src 'self' https://open.spotify.com https://www.youtube.com https://www.youtube-nocookie.com;";
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.Headers.CacheControl = "no-store";
    }
    await next();
});
app.UseForwardedHeaders();
app.UseRouting();
app.UseRateLimiter();
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
