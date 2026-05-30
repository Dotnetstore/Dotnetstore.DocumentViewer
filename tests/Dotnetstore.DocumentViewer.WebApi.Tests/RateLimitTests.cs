using System.Net;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

/// <summary>
/// Exercises the per-IP auth rate limiter against a fixture that exposes a very low
/// PermitLimit. Each test isolates its partition with a unique X-Forwarded-For so
/// the bucket starts at zero independently of any other test in this class.
/// </summary>
public sealed class RateLimitTests(LowRateLimitFactory factory) : IClassFixture<LowRateLimitFactory>
{
    [Fact]
    public async Task Login_returns_429_after_exceeding_per_ip_limit()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", DocumentViewerApiFactory.ApiKey);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{Random.Shared.Next(1, 250)}");

        // Use a non-existent email — bounces at FindByEmail with 401 without touching
        // Identity lockout, but still flows through the rate limiter.
        var creds = new LoginRequest("nobody-rl@test.invalid", "DoesNotMatter1!");

        // First N succeed (return 401, but pass the rate-limit gate).
        for (var i = 0; i < LowRateLimitFactory.PermitLimit; i++)
        {
            var resp = await client.PostAsJsonAsync("/auth/login", creds);
            resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"call {i + 1} should hit the endpoint");
        }

        var tooMany = await client.PostAsJsonAsync("/auth/login", creds);
        tooMany.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Refresh_shares_the_same_per_ip_bucket_as_login()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", DocumentViewerApiFactory.ApiKey);
        var ipKey = $"192.0.2.{Random.Shared.Next(1, 250)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ipKey);

        // Burn the bucket via login attempts.
        for (var i = 0; i < LowRateLimitFactory.PermitLimit; i++)
        {
            _ = await client.PostAsJsonAsync("/auth/login",
                new LoginRequest("nobody-rl2@test.invalid", "DoesNotMatter1!"));
        }

        // A refresh call from the SAME ip is in the same partition because both
        // endpoints use the "auth" policy keyed on remote IP.
        var refresh = await client.PostAsJsonAsync("/auth/refresh",
            new RefreshTokenRequest("anything-the-rate-limit-fires-first"));
        refresh.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Different_xff_values_get_independent_buckets()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", DocumentViewerApiFactory.ApiKey);

        var ipA = $"192.0.2.{Random.Shared.Next(1, 250)}";
        var ipB = $"192.0.2.{Random.Shared.Next(1, 250)}";
        var creds = new LoginRequest("nobody-rl3@test.invalid", "DoesNotMatter1!");

        // Saturate bucket A.
        for (var i = 0; i < LowRateLimitFactory.PermitLimit + 1; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
            {
                Content = JsonContent.Create(creds)
            };
            req.Headers.Add("X-Forwarded-For", ipA);
            _ = await client.SendAsync(req);
        }

        // Bucket B is fresh — first call should bounce on 401, not 429.
        using var bRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(creds)
        };
        bRequest.Headers.Add("X-Forwarded-For", ipB);
        var bResp = await client.SendAsync(bRequest);
        bResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
