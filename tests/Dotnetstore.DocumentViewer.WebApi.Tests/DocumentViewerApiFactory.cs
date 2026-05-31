using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

public class DocumentViewerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ApiKey = "test-api-key-please-pretend-this-is-random";
    public const string JwtSigningKey = "test-signing-key-that-is-at-least-32-characters-long-aaaaaaaaaa";
    public const string SignedUrlSigningKey = "test-signed-url-key-that-is-at-least-32-characters-long-bbbbb";

    public const string AdminEmail = "admin@dotnetstore.test";
    public const string AdminPassword = "AdminPass123!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private string _storageRoot = string.Empty;

    /// <summary>
    /// Override in a derived fixture to keep the DocumentConversionWorker registered.
    /// Default true so the standard collection's DOCX upload tests aren't gated on a
    /// running LibreOffice / Gotenberg.
    /// </summary>
    protected virtual bool DisableConversionWorker => true;

    public virtual async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _storageRoot = Path.Combine(Path.GetTempPath(), "dvtests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageRoot);

        // Force host build (this also kicks off hosted-service StartAsync). We then directly
        // await DatabaseInitializer.StartAsync ourselves: relying on /alive to "warm up" the
        // host doesn't prove migrations finished because the alive probe doesn't touch the DB,
        // and the host can begin serving requests before all hosted services complete in some
        // builds. Migrations are idempotent so double-invocation is safe.
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // The seeder marks the admin MustChangePassword=true. The new
        // MustChangePasswordGuardMiddleware would block this admin from every
        // non-/auth endpoint, breaking nearly every test. Clear it once here so
        // the shared fixture's admin is immediately usable; tests that want a
        // flagged user should use CreateViewerAsync(..., mustChangePassword: true).
        await db.Users
            .Where(u => u.Email == AdminEmail)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, false));
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        if (Directory.Exists(_storageRoot))
        {
            try { Directory.Delete(_storageRoot, recursive: true); } catch { /* best effort */ }
        }
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ConfigureAppConfiguration runs AFTER appsettings.{env}.json, so an in-memory provider
        // added here wins. UseSetting alone is overridden by appsettings.Development.json's
        // dev defaults (Seed:Admin:* in particular).
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:documentviewer"] = _postgres.GetConnectionString(),
                ["Jwt:Issuer"] = "documentviewer-tests",
                ["Jwt:Audience"] = "documentviewer-tests-api",
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["ApiKey:Value"] = ApiKey,
                ["SignedUrl:SigningKey"] = SignedUrlSigningKey,
                ["SignedUrl:Lifetime"] = "00:01:00",
                ["DocumentStorage:RootPath"] = _storageRoot,
                ["Seed:Admin:Email"] = AdminEmail,
                ["Seed:Admin:Password"] = AdminPassword,
                // Effectively disable per-IP auth rate limiting for the shared test fixture —
                // ~50+ admin logins across the collection would otherwise saturate the bucket.
                // Dedicated rate-limit tests can override via X-Forwarded-For to isolate.
                ["RateLimiting:Auth:PermitLimit"] = "10000",
                ["RateLimiting:Auth:Window"] = "00:00:30",
            });
        });

        // The DocumentConversionWorker hosted service calls Gotenberg / soffice. By
        // default the shared collection has no converter running, so we strip the worker;
        // dedicated end-to-end fixtures (GotenbergSmokeFactory) override
        // DisableConversionWorker to false so the worker runs against a real Gotenberg.
        if (DisableConversionWorker)
        {
            builder.ConfigureTestServices(services =>
            {
                var worker = services.FirstOrDefault(s =>
                    s.ServiceType == typeof(IHostedService) &&
                    s.ImplementationType == typeof(DocumentConversionWorker));
                if (worker is not null) services.Remove(worker);
            });
        }
    }

    /// <summary>Client with the test API key attached but no JWT.</summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    /// <summary>Client with no API key and no JWT — for testing the API-key gate.</summary>
    public HttpClient CreateBareClient() => CreateClient();

    /// <summary>Client authenticated as the seeded admin (with API key).</summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var token = await LoginAsync(AdminEmail, AdminPassword);
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        using var client = CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        return token.AccessToken;
    }

    public async Task<UserDto> CreateViewerAsync(string email, string password = "ViewerPass123!", bool mustChangePassword = false)
    {
        using var adminClient = await CreateAdminClientAsync();
        var response = await adminClient.PostAsJsonAsync("/users",
            new CreateUserRequest(email, "Viewer User", password, [RoleNames.Viewer]));
        response.EnsureSuccessStatusCode();
        var dto = (await response.Content.ReadFromJsonAsync<UserDto>())!;

        // CreateUserEndpoint always sets MustChangePassword=true (the right production
        // default for admin-minted users). For tests that just want a viewer that can
        // hit the document surface, clear it here so the MCP guard doesn't 403 them.
        if (!mustChangePassword)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users
                .Where(u => u.Id == dto.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, false));
        }
        return dto;
    }

    public async Task<HttpClient> CreateViewerClientAsync(string email, string password = "ViewerPass123!")
    {
        var token = await LoginAsync(email, password);
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Posts a fake PDF to <c>/documents</c> via <paramref name="client"/> (which must already
    /// be authenticated as Admin) and returns the created <see cref="DocumentDto"/>. Centralised
    /// so the half-dozen integration tests that need a "give me a doc to act on" no longer
    /// hand-roll the multipart payload.
    /// </summary>
    public static async Task<DocumentDto> UploadPdfAsync(HttpClient client, string title)
    {
        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(FakePdfBytes(title));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", $"{title.Replace(' ', '_')}.pdf");
        form.Add(new StringContent(title), "Title");
        var response = await client.PostAsync("/documents", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    /// <summary>Posts an IP / CIDR to the document's allow-list via an authenticated admin
    /// client and returns the created entry. Mirrors <see cref="UploadPdfAsync"/>.</summary>
    public static async Task<AllowedIpDto> AddAllowedIpAsync(HttpClient adminClient, Guid documentId, string cidr, string? description = null)
    {
        using var response = await adminClient.PostAsJsonAsync(
            $"/documents/{documentId}/allowed-ips",
            new AddAllowedIpRequest(cidr, description));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AllowedIpDto>())!;
    }

    /// <summary>Directly sets <c>Document.PageCount</c> in the DB so endpoints that lazily
    /// derive it via <see cref="Infrastructure.Rendering.IPdfPageRenderer.GetPageCount"/>
    /// don't have to actually rasterise — fake-PDF test payloads would otherwise throw.</summary>
    public async Task SetPageCountAsync(Guid documentId, int pageCount)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Documents
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.PageCount, pageCount));
    }

    /// <summary>Bytes that start with the <c>%PDF-</c> magic so the upload endpoint's
    /// content-sniffing classifier accepts them as a PDF.</summary>
    public static byte[] FakePdfBytes(string label = "sample")
    {
        var header = "%PDF-1.4\n"u8.ToArray();
        var payload = System.Text.Encoding.UTF8.GetBytes(label + new string('a', 64));
        var footer = "\n%%EOF\n"u8.ToArray();
        return [.. header, .. payload, .. footer];
    }

    /// <summary>Bytes that start with the ZIP local-file-header magic so the upload
    /// classifier accepts them as a DOCX (DOCX is a zip).</summary>
    public static byte[] FakeDocxBytes(string label = "sample")
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var payload = System.Text.Encoding.UTF8.GetBytes(label + new string('z', 64));
        return [.. header, .. payload];
    }

    /// <summary>Bytes that match NEITHER PDF nor ZIP magic — the upload endpoint should reject.</summary>
    public static byte[] FakeUnsupportedBytes() =>
        System.Text.Encoding.UTF8.GetBytes("not-a-document-payload-just-text-bytes");
}
