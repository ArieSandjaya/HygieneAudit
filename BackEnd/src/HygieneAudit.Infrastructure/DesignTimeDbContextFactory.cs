using HygieneAudit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HygieneAudit.Infrastructure;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HygieneAuditDbContext>
{
    public HygieneAuditDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json in API project
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "HygieneAudit.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<HygieneAuditDbContext>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=HygieneAudit;Trusted_Connection=True;TrustServerCertificate=True;";

        optionsBuilder.UseSqlServer(connectionString);

        return new HygieneAuditDbContext(optionsBuilder.Options);
    }
}
