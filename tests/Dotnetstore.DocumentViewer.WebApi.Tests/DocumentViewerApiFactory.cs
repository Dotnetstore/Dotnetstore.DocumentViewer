using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Conversion;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

public sealed class DocumentViewerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ApiKey = "test-api-key-please-pretend-this-is-random";
    public const string JwtSigningKey = "test-signing-key-that-is-at-least-32-characters-long-aaaaaaaaaa";
    public const string SignedUrlSigningKey = "test-signed-url-key-that-is-at-least-32-characters-long-bbbbb";

    public const string AdminEmail = "admin@dotnetstore.test";
    public const string AdminPassword = "AdminPass123!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private string _storageRoot = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _storageRoot = Path.Combine(Path.GetTempPath(), "dvtests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageRoot);

        // Force the host to spin up so DatabaseInitializer applies migrations + seeds the admin.
        using var bootstrap = CreateBareClient();
        _ = await bootstrap.GetAsync("/alive");
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
            });
        });

        // The DocumentConversionWorker hosted service shells out to LibreOffice. Tests
        // can't assume soffice is installed, so we remove the worker registration entirely.
        // DOCX upload tests assert the initial Status=Converting without waiting for the
        // worker to run.
        builder.ConfigureTestServices(services =>
        {
            var worker = services.FirstOrDefault(s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType == typeof(DocumentConversionWorker));
            if (worker is not null) services.Remove(worker);
        });
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

    public async Task<UserDto> CreateViewerAsync(string email, string password = "ViewerPass123!")
    {
        using var adminClient = await CreateAdminClientAsync();
        var response = await adminClient.PostAsJsonAsync("/users",
            new CreateUserRequest(email, "Viewer User", password, [RoleNames.Viewer]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    public async Task<HttpClient> CreateViewerClientAsync(string email, string password = "ViewerPass123!")
    {
        var token = await LoginAsync(email, password);
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Bytes labeled as a PDF. The upload endpoint validates content type, not PDF structure.</summary>
    public static byte[] FakePdfBytes(string label = "sample")
    {
        var header = "%PDF-1.4\n"u8.ToArray();
        var payload = System.Text.Encoding.UTF8.GetBytes(label + new string('a', 64));
        var footer = "\n%%EOF\n"u8.ToArray();
        return [.. header, .. payload, .. footer];
    }

    /// <summary>Bytes labeled as a DOCX. Like the PDF helper, only enough to pass the content-type gate.</summary>
    public static byte[] FakeDocxBytes(string label = "sample") =>
        System.Text.Encoding.UTF8.GetBytes("PK" + label + new string('z', 64));
}
