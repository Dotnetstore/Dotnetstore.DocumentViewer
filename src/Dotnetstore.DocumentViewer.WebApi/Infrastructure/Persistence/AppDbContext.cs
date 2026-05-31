using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentAccess> DocumentAccesses => Set<DocumentAccess>();
    public DbSet<AccessAuditLog> AccessAuditLogs => Set<AccessAuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RevokedAccessToken> RevokedAccessTokens => Set<RevokedAccessToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.DisplayName).HasMaxLength(200);
        });

        builder.Entity<Document>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Title).HasMaxLength(300).IsRequired();
            b.Property(d => d.OriginalFileName).HasMaxLength(260).IsRequired();
            b.Property(d => d.ContentType).HasMaxLength(120).IsRequired();
            b.Property(d => d.StoragePath).HasMaxLength(1024).IsRequired();
            b.Property(d => d.Status).HasConversion<int>();
            b.HasIndex(d => d.UploadedAtUtc);
        });

        builder.Entity<DocumentAccess>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.DocumentId, a.UserId }).IsUnique();
            b.HasIndex(a => a.UserId);
        });

        builder.Entity<AccessAuditLog>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.Action).HasMaxLength(64).IsRequired();
            b.Property(a => a.IpAddress).HasMaxLength(64);
            b.HasIndex(a => a.AtUtc);
            b.HasIndex(a => new { a.UserId, a.AtUtc });
            b.HasIndex(a => new { a.DocumentId, a.AtUtc });
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.TokenHash).HasMaxLength(200).IsRequired();
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => t.UserId);
        });

        builder.Entity<RevokedAccessToken>(b =>
        {
            b.HasKey(t => t.Jti);
            b.Property(t => t.Jti).HasMaxLength(64);
            b.HasIndex(t => t.ExpiresAt);
            b.HasIndex(t => t.UserId);
        });
    }
}
