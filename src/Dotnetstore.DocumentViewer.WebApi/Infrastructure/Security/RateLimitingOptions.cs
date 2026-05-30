namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public const string AuthPolicy = "auth";

    public AuthRateLimit Auth { get; init; } = new();

    public sealed class AuthRateLimit
    {
        public int PermitLimit { get; init; } = 10;
        public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
    }
}
