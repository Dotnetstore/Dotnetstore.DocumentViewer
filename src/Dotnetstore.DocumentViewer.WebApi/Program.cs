using System.IdentityModel.Tokens.Jwt;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.RateLimiting;
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
    });
builder.Services.AddAuthorization();

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

// DOCX -> PDF conversion via LibreOffice (soffice). DocumentConversionWorker is a
// hosted service that polls Status=Converting documents and runs the converter.
// LibreOffice must be installed on the WebApi host (or routed via the configured SofficePath).
builder.Services
    .AddOptions<DocumentConversionOptions>()
    .Bind(builder.Configuration.GetSection(DocumentConversionOptions.SectionName));
builder.Services.AddSingleton<IDocumentConverter, SofficeDocumentConverter>();
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

builder.Services.AddHostedService<DatabaseInitializer>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseMiddleware<ApiKeyMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
    app.UseSwaggerGen();

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
