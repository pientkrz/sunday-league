using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SundayLeague.Api.Domain;

namespace SundayLeague.Api.Data;

public sealed class LeagueDbContext(DbContextOptions<LeagueDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<LeagueMatch> Matches => Set<LeagueMatch>();
    public DbSet<BoardMembership> BoardMemberships => Set<BoardMembership>();
    public DbSet<BoardInvitation> BoardInvitations => Set<BoardInvitation>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<League>(entity => { entity.HasIndex(x => x.Slug).IsUnique(); entity.Property(x => x.Name).HasMaxLength(120); });
        builder.Entity<Team>(entity => { entity.HasIndex(x => new { x.LeagueId, x.Name }).IsUnique(); entity.Property(x => x.Name).HasMaxLength(120); entity.Property(x => x.ShortName).HasMaxLength(5); });
        builder.Entity<LeagueMatch>(entity =>
        {
            entity.HasIndex(x => new { x.LeagueId, x.RoundNumber });
            entity.HasOne(x => x.HomeTeam).WithMany().HasForeignKey(x => x.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AwayTeam).WithMany().HasForeignKey(x => x.AwayTeamId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Version).IsConcurrencyToken();
        });
        builder.Entity<BoardMembership>(entity => entity.HasIndex(x => new { x.UserId, x.LeagueId }).IsUnique());
        builder.Entity<BoardInvitation>(entity => { entity.HasIndex(x => x.TokenHash).IsUnique(); entity.HasIndex(x => new { x.Email, x.AcceptedAt }); entity.Property(x => x.Email).HasMaxLength(256); });
        builder.Entity<AuditEvent>(entity => entity.HasIndex(x => x.OccurredAt));
    }
}
