using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HygieneAudit.Infrastructure;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HygieneAuditDbContext>
{
    public HygieneAuditDbContext CreateDbContext(string[] args)
    {
        // Build configuration from the current working directory (the project the
        // migration command runs in). An optional appsettings.json or an environment
        // variable can supply the connection string; otherwise a localhost default is used.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<HygieneAuditDbContext>();

        var connectionString =
            Environment.GetEnvironmentVariable("HYGIENEAUDIT_CONNECTION")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=HygieneAudit;Trusted_Connection=True;TrustServerCertificate=True;";

        optionsBuilder.UseSqlServer(connectionString);

        return new HygieneAuditDbContext(optionsBuilder.Options);
    }
}
