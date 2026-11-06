using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed class SteadingDbContext(DbContextOptions<SteadingDbContext> options) : DbContext(options)
{
    public DbSet<Steading> Steadings => Set<Steading>();

    public DbSet<LogEntry> LogEntries => Set<LogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Steading>(steading =>
        {
            steading.Ignore(s => s.Stats);
            steading.Ignore(s => s.Debilities);
            steading.Property(s => s.Name).HasMaxLength(100);
            steading.Property(s => s.Version).IsRowVersion();

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
    }

    private static string RangeSql(SteadingStat stat)
    {
        var min = $"\"{stat}\" >= {StatRules.Min(stat)}";
        return StatRules.Max(stat) is { } max ? $"{min} AND \"{stat}\" <= {max}" : min;
    }
}
