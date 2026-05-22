using DataHub.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataHub.Infrastructure.Data;

/// <summary>
/// Used by the EF Core CLI (<c>dotnet ef</c>) at design time to construct the DbContext
/// without going through the API's full DI container. Reads the connection string from
/// <c>src/DataHub.Api/appsettings.Development.json</c>.
/// </summary>
public class DataHubDbContextFactory : IDesignTimeDbContextFactory<DataHubDbContext>
{
    public DataHubDbContext CreateDbContext(string[] args)
    {
        // Find the API project's appsettings so we share a single source of truth for the connection string.
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "DataHub.Api"));
        if (!Directory.Exists(basePath))
        {
            // Fall back to current dir if invoked from the API project itself.
            basePath = Directory.GetCurrentDirectory();
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection not found. Looked in " + basePath);

        var options = new DbContextOptionsBuilder<DataHubDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new DataHubDbContext(options, new NullCurrentUser());
    }

    private sealed class NullCurrentUser : ICurrentUser
    {
        public string? Identifier => "design-time";
    }
}
