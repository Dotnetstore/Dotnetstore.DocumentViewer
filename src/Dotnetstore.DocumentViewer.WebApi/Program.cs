using System.IdentityModel.Tokens.Jwt;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.RateLimiting;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Caching;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Rendering;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

// The PDF rasterizer (PDFtoImage/PDFium) is supported only on these host platforms.
[assembly: SupportedOSPlatform("windows")]
[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("macos")]

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// Database. Non-pooled so we can rely on scoped dependencies later if needed;
// EnrichNpgsqlDbContext layers on Aspire's retries, health check, logging and telemetry.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("documentviewer")));
// Manual transactions (e.g. multi-step uploads) would be incompatible with the retrying
// execution strategy that EnrichNpgsqlDbContext enables by default — disable up front.
builder.EnrichNpgsqlDbContext<AppDbContext>(s => s.DisableRetry = true);

// Cross-cutting persistence services (centralise rules + writes so the endpoints stay thin).
builder.Services.AddScoped<IDocumentAccessPolicy, DocumentAccessPolicy>();
builder.Services.AddScoped<IDocumentIpPolicy, DocumentIpPolicy>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// DatabaseInitializer must register BEFORE any other hosted service so the host's
// sequential StartAsync runs migrations before workers (RevokedAccessTokenCleanupWorker,
// CacheEvictionWorker, DocumentConversionWorker) start querying their tables.
builder.Services.AddHostedService<DatabaseInitializer>();

// Identity
builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequireDigit = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.User.RequireUniqueEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<ApplicationRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<AppDbContext>();

// JWT. JwtBearerOptions is bound via the options pattern so test fixtures that
// override Jwt:* settings via ConfigureAppConfiguration (which runs AFTER this file's
// top-level statements) still flow through to the bearer middleware. Reading the raw
// Configuration here would snapshot the values too early and silently miss test overrides.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOpts) =>
    {
        var jwt = jwtOpts.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role",
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jti)) return;
                var store = ctx.HttpContext.RequestServices.GetRequiredService<IAccessTokenRevocationStore>();
                if (await store.IsRevokedAsync(jti, ctx.HttpContext.RequestAborted))
                    ctx.Fail("Access token has been revoked.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAccessTokenRevocationStore, AccessTokenRevocationStore>();
builder.Services.AddHostedService<RevokedAccessTokenCleanupWorker>();

// Forwarded headers — translates X-Forwarded-For / -Proto / -Host onto the request
// when the connecting peer is a trusted reverse proxy. In Aspire local dev the WebApi
// is direct on localhost (loopback is trusted by default), so this is a no-op there
// but keeps deployments behind nginx/Azure App Service/Container Apps honest.
builder.Services
    .AddOptions<ForwardedHeadersConfig>()
    .Bind(builder.Configuration.GetSection(ForwardedHeadersConfig.SectionName));

builder.Services
    .Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                   | ForwardedHeaders.XForwardedProto
                                   | ForwardedHeaders.XForwardedHost;
    })
    .AddOptions<ForwardedHeadersOptions>()
    .Configure<IOptions<ForwardedHeadersConfig>>((fh, cfg) =>
    {
        var c = cfg.Value;
        fh.ForwardLimit = c.ForwardLimit;
        foreach (var net in c.KnownNetworks)
        {
            if (System.Net.IPNetwork.TryParse(net, out var parsed))
                fh.KnownIPNetworks.Add(parsed);
        }
        foreach (var proxy in c.KnownProxies)
        {
            if (System.Net.IPAddress.TryParse(proxy, out var parsed))
                fh.KnownProxies.Add(parsed);
        }
    });

// ApiKey
builder.Services
    .AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyOptions.SectionName))
    .ValidateOnStart();

// Rate limiting. Per-IP fixed-window on the auth endpoints to slow brute-force
// password attempts. The partition key honours X-Forwarded-For first so deployments
// behind a trusted reverse proxy throttle by the real client IP, not the proxy's.
builder.Services
    .AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

builder.Services.AddRateLimiter(rl =>
{
    rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rl.AddPolicy(RateLimitingOptions.AuthPolicy, httpContext =>
    {
        var rlOpts = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value.Auth;
        var key = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                  ?? httpContext.Connection.RemoteIpAddress?.ToString()
                  ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rlOpts.PermitLimit,
            Window = rlOpts.Window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    });
});

// Seed admin
builder.Services
    .AddOptions<SeedAdminOptions>()
    .Bind(builder.Configuration.GetSection(SeedAdminOptions.SectionName));

// Document storage
builder.Services
    .AddOptions<DocumentStorageOptions>()
    .Bind(builder.Configuration.GetSection(DocumentStorageOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();

// Signed URLs + PDF rendering
builder.Services
    .AddOptions<SignedUrlOptions>()
    .Bind(builder.Configuration.GetSection(SignedUrlOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<ISignedUrlService, SignedUrlService>();
builder.Services.AddSingleton<IPdfPageRenderer, PdfPageRenderer>();

// Page-image disk cache (un-watermarked PNGs) + LRU eviction worker. Skipping the
// PDFium rasterization is the cheapest big win for the render endpoint.
builder.Services
    .AddOptions<CacheOptions>()
    .Bind(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.AddSingleton<IPageImageCache, FileSystemPageImageCache>();
builder.Services.AddHostedService<CacheEvictionWorker>();

// DOCX -> PDF conversion. Default mode is Gotenberg: the AppHost runs a gotenberg
// container and WebApi calls it via service discovery (http://gotenberg). The Soffice
// fallback shells out to a locally-installed LibreOffice binary. DocumentConversionWorker
// is a hosted service that polls Status=Converting documents and runs the converter.
builder.Services
    .AddOptions<DocumentConversionOptions>()
    .Bind(builder.Configuration.GetSection(DocumentConversionOptions.SectionName));

var conversionMode = builder.Configuration
    .GetValue<DocumentConversionMode>($"{DocumentConversionOptions.SectionName}:Mode",
        DocumentConversionMode.Gotenberg);

if (conversionMode == DocumentConversionMode.Gotenberg)
{
    builder.Services.AddHttpClient<IDocumentConverter, GotenbergDocumentConverter>((sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<DocumentConversionOptions>>().Value;
        http.BaseAddress = new Uri(opts.Gotenberg.BaseAddress);
        http.Timeout = opts.ConversionTimeout;
    });
}
else
{
    builder.Services.AddSingleton<IDocumentConverter, SofficeDocumentConverter>();
}

builder.Services.AddHostedService<DocumentConversionWorker>();

// FastEndpoints
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "Dotnetstore DocumentViewer API";
        s.Version = "v1";
    };
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Forwarded headers must run BEFORE anything that consumes the client IP — the rate
// limiter, the ApiKey middleware, and the render endpoint's audit-log entries all
// read Connection.RemoteIpAddress, which this middleware rewrites from X-Forwarded-For
// when the connecting peer is in KnownNetworks / KnownProxies.
var fhEnabled = app.Configuration
    .GetValue($"{ForwardedHeadersConfig.SectionName}:Enabled", defaultValue: true);
if (fhEnabled)
    app.UseForwardedHeaders();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
// Runs AFTER UseAuthentication so claims are populated, BEFORE UseAuthorization
// so a flagged user can't slip through a [Authorize]-decorated endpoint.
app.UseMiddleware<MustChangePasswordGuardMiddleware>();
app.UseAuthorization();

app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
    app.UseSwaggerGen();

app.MapDefaultEndpoints();

// ServiceDefaults only maps /health and /alive in Development. Container orchestrators
// (docker-compose HEALTHCHECK, K8s liveness/readiness) need them in Production too —
// both are already allow-listed by ApiKeyMiddleware (anonymous + no API key required)
// and the /alive probe reveals nothing user-actionable.
if (!app.Environment.IsDevelopment())
{
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/alive", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("live"),
    });
}

app.Run();

public partial class Program;
