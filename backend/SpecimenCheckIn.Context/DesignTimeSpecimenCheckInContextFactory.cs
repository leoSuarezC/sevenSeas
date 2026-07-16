using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SpecimenCheckIn.Context;

/// <summary>
/// Lets the EF Core tools build a context at design time (migrations, scaffolding)
/// without booting the API.
/// </summary>
/// <remarks>
/// Only ever used by the <c>dotnet ef</c> tooling. At runtime the API composes the
/// context through dependency injection instead.
/// </remarks>
public class DesignTimeSpecimenCheckInContextFactory : IDesignTimeDbContextFactory<SpecimenCheckInContext>
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SpecimenCheckIn;Trusted_Connection=True;TrustServerCertificate=True";

    /// <inheritdoc/>
    public SpecimenCheckInContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__SpecimenCheckIn")
            ?? DefaultConnectionString;

        DbContextOptionsBuilder<SpecimenCheckInContext> options = new();
        options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            SpecimenCheckInContext.Schema));

        return new SpecimenCheckInContext(options.Options);
    }
}
