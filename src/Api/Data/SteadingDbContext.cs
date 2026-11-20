using Api.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed class SteadingDbContext(DbContextOptions<SteadingDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Steading> Steadings => Set<Steading>();

    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    public DbSet<Npc> Npcs => Set<Npc>();

    public DbSet<MapImage> MapImages => Set<MapImage>();

    public DbSet<PlaySession> PlaySessions => Set<PlaySession>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Steading>(steading =>
        {
            steading.Ignore(s => s.Stats);
            steading.Ignore(s => s.Debilities);
            steading.Property(s => s.Name).HasMaxLength(100);
            steading.Property(s => s.Version).IsRowVersion();
            steading.Property(s => s.ShareToken).HasMaxLength(64);
            steading.HasIndex(s => s.ShareToken).IsUnique();

            steading.ToTable(table =>
            {
                foreach (var stat in Enum.GetValues<SteadingStat>())
                {
                    table.HasCheckConstraint($"CK_Steadings_{stat}", RangeSql(stat));
                }
            });
        });

        modelBuilder.Entity<LogEntry>(entry =>
        {
            entry.Property(e => e.Stat).HasConversion<string>().HasMaxLength(20);
            entry.HasOne<Steading>().WithMany().HasForeignKey(e => e.SteadingId);
            entry.HasIndex(e => new { e.SteadingId, e.ChangedAt });
        });

        modelBuilder.Entity<Npc>(npc =>
        {
            npc.Ignore(n => n.Archived);
            npc.Property(n => n.Name).HasMaxLength(Npc.NameMaxLength);
            npc.Property(n => n.Description).HasMaxLength(Npc.DescriptionMaxLength);
            npc.Property(n => n.GmNotes).HasMaxLength(Npc.GmNotesMaxLength);
            npc.HasOne<Steading>().WithMany().HasForeignKey(n => n.SteadingId);
            npc.HasIndex(n => n.SteadingId);
        });

        modelBuilder.Entity<MapImage>(image =>
        {
            image.Property(i => i.ContentType).HasMaxLength(20);
            image.Property(i => i.Version).HasMaxLength(16);
            image.HasOne<Steading>().WithMany().HasForeignKey(i => i.SteadingId);
            image.HasIndex(i => i.SteadingId).IsUnique();
        });

        modelBuilder.Entity<PlaySession>(session =>
        {
            session.Property(s => s.Title).HasMaxLength(PlaySession.TitleMaxLength);
            session.HasOne<Steading>().WithMany().HasForeignKey(s => s.SteadingId);
            session.HasIndex(s => new { s.SteadingId, s.Number }).IsUnique();
            session.ToTable(table => table.HasCheckConstraint(
                "CK_PlaySessions_Number",
                $"\"Number\" BETWEEN {PlaySession.MinNumber} AND {PlaySession.MaxNumber}"));

            // The route is always read and replaced whole, so it lives in one JSON column, in order.
            session.OwnsMany(s => s.Waypoints, waypoint => waypoint.ToJson());
        });
    }

    private static string RangeSql(SteadingStat stat)
    {
        var min = $"\"{stat}\" >= {StatRules.Min(stat)}";
        return StatRules.Max(stat) is { } max ? $"{min} AND \"{stat}\" <= {max}" : min;
    }
}
