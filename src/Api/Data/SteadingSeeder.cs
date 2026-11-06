using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public static class SteadingSeeder
{
    public static async Task SeedAsync(SteadingDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Steadings.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Steadings.Add(Steading.CreateStonetop());
        await db.SaveChangesAsync(cancellationToken);
    }
}
