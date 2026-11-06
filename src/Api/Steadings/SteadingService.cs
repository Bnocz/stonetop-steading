using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Steadings;

public sealed class SteadingService(SteadingDbContext db, TimeProvider timeProvider)
{
    private const int LogLimit = 50;

    // The app tracks one steading, created by SteadingSeeder.
    public Task<Steading?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        db.Steadings.SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<LogEntryResponse>> GetLogAsync(
        Guid steadingId,
        CancellationToken cancellationToken = default) =>
        await db.LogEntries
            .AsNoTracking()
            .Where(entry => entry.SteadingId == steadingId)
            .OrderByDescending(entry => entry.ChangedAt)
            .Take(LogLimit)
            .Select(entry => new LogEntryResponse(entry.Stat, entry.OldValue, entry.NewValue, entry.ChangedAt))
            .ToListAsync(cancellationToken);

    public async Task ChangeStatAsync(
        Guid steadingId,
        SteadingStat stat,
        int newValue,
        CancellationToken cancellationToken = default)
    {
        var steading = await db.Steadings.SingleOrDefaultAsync(s => s.Id == steadingId, cancellationToken)
            ?? throw new KeyNotFoundException($"Steading {steadingId} not found.");

        var logEntry = steading.ChangeStat(stat, newValue, timeProvider.GetUtcNow());
        if (logEntry is null)
        {
            return;
        }

        // A single SaveChanges writes the stat and its log entry in one transaction.
        db.LogEntries.Add(logEntry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
