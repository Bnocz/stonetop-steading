using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Data;

// Lets `dotnet ef` build the model without a configured database. It never connects.
public sealed class SteadingDbContextFactory : IDesignTimeDbContextFactory<SteadingDbContext>
{
    public SteadingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SteadingDbContext>().UseNpgsql("Host=localhost").Options);
}
