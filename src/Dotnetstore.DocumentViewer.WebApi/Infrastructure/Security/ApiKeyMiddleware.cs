using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

public sealed class ApiKeyMiddleware(RequestDelegate next, IOptionsMonitor<ApiKeyOptions> options)
{
    private static readonly PathString HealthPath = new("/health");
    private static readonly PathString AlivePath = new("/alive");

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(HealthPath) ||
            context.Request.Path.StartsWithSegments(AlivePath))
        {
            await next(context);
            return;
        }

        var expected = Encoding.UTF8.GetBytes(options.CurrentValue.Value);
        if (!context.Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var presented) ||
            presented.Count != 1 ||
            !FixedTimeEquals(presented[0], expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("API key missing or invalid.");
            return;
        }

        await next(context);
    }

    private static bool FixedTimeEquals(string? presented, byte[] expected)
    {
        if (presented is null) return false;
        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        return CryptographicOperations.FixedTimeEquals(presentedBytes, expected);
    }
}
