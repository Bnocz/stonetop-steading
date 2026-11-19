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
    }

    private static string RangeSql(SteadingStat stat)
    {
        var min = $"\"{stat}\" >= {StatRules.Min(stat)}";
        return StatRules.Max(stat) is { } max ? $"{min} AND \"{stat}\" <= {max}" : min;
    }
}
