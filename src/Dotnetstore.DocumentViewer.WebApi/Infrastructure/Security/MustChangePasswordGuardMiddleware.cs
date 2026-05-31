namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

/// <summary>
/// Blocks authenticated users whose access token carries <c>mcp=1</c> (must change
/// password) from any endpoint outside a narrow allowlist. The Avalonia client already
/// navigates to the change-password screen on this flag, but a non-Avalonia caller
/// could otherwise ignore the prompt entirely and keep using the JWT — this middleware
/// is the server-side teeth behind the flag.
/// </summary>
public sealed class MustChangePasswordGuardMiddleware(RequestDelegate next)
{
    private static readonly string[] Allowed =
    [
        "/auth/login",
        "/auth/refresh",
        "/auth/logout",
        "/auth/me",
        "/auth/change-password",
        "/health",
        "/alive",
        "/swagger",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirst("mcp")?.Value == "1" &&
            !IsAllowed(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Password change required before this endpoint can be used.");
            return;
        }
        await next(context);
    }

    private static bool IsAllowed(PathString path)
    {
        foreach (var allowed in Allowed)
        {
            if (path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
