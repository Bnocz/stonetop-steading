using Api.Steadings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public static class DatabaseSetup
{
    public static IServiceCollection AddSteadingDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        // Retries cover Neon waking from suspend. Multi-step transactions would need an execution strategy.
        services.AddDbContext<SteadingDbContext>(
            options => options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SteadingService>();

        // Render's disk is wiped on restart, so keep the keys that sign session cookies in the database.
        services.AddDataProtection()
            .SetApplicationName("stonetop-steading")
            .PersistKeysToDbContext<SteadingDbContext>();
        return services;
    }

    // Without a connection string the app still starts, so /health works with no database.
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<SteadingDbContext>();
        if (db is null)
        {
            app.Logger.LogWarning("ConnectionStrings:Default is not set; skipping database setup.");
            return;
        }

        await db.Database.MigrateAsync();
        await SteadingSeeder.SeedAsync(db);
    }
}
