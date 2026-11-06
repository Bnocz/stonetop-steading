using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Steadings;

public sealed class SteadingService(SteadingDbContext db, TimeProvider timeProvider)
{
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
