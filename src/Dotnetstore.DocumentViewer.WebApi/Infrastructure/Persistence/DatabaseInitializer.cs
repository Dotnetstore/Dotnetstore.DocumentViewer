using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IServiceProvider services,
    IOptions<SeedAdminOptions> seedAdmin,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation("Applying database migrations");
        await db.Database.MigrateAsync(cancellationToken);

        await SeedRolesAsync(scope.ServiceProvider);
        await SeedAdminAsync(scope.ServiceProvider);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    private async Task SeedAdminAsync(IServiceProvider services)
    {
        var settings = seedAdmin.Value;
        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogInformation("Seed admin not configured; skipping.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByEmailAsync(settings.Email);
        if (existing is not null)
        {
            await EnsureAdminRoleAsync(userManager, existing);
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = settings.Email,
            Email = settings.Email,
            EmailConfirmed = true,
            DisplayName = settings.DisplayName,
            MustChangePassword = true,
        };

        var result = await userManager.CreateAsync(user, settings.Password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed admin user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await EnsureAdminRoleAsync(userManager, user);
        logger.LogInformation("Seeded admin user: {Email}", settings.Email);
    }

    private static async Task EnsureAdminRoleAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, RoleNames.Admin))
            await userManager.AddToRoleAsync(user, RoleNames.Admin);
    }
}
