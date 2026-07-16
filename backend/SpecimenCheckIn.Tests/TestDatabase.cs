using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Models;

namespace SpecimenCheckIn.Tests;

/// <summary>
/// A real SQL Server database for the tests to run against.
/// </summary>
/// <remarks>
/// Deliberately not an in-memory provider. The tenant isolation these tests exist to prove
/// is enforced by row-level security inside SQL Server, so a fake database would test the
/// one layer that is not the safety net and quietly skip the one that is.
/// </remarks>
public sealed class TestDatabase : IAsyncLifetime
{
    /// <summary>The xUnit collection sharing this database.</summary>
    public const string CollectionName = "Database";

    /// <summary>The lab the tests act as by default.</summary>
    public const int CentralLabId = 1;

    /// <summary>A second lab, used to prove the two cannot see each other.</summary>
    public const int WestsideLabId = 2;

    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SpecimenCheckIn_Tests;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>
    /// Builds the database from the migrations and seeds the two labs.
    /// </summary>
    /// <returns>A task that completes when the database is ready.</returns>
    public async Task InitializeAsync()
    {
        await using SpecimenCheckInContext database = this.CreateUntenantedContext();

        await database.Database.EnsureDeletedAsync();

        // Running the migrations, rather than EnsureCreated, is what puts the security
        // policy in place: the isolation under test is created by a migration.
        await database.Database.MigrateAsync();

        database.Labs.AddRange(
            new Lab { Id = CentralLabId, Name = "Central Lab" },
            new Lab { Id = WestsideLabId, Name = "Westside Pathology Lab" });

        await database.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Opens a context acting as the given lab, as a request would.
    /// </summary>
    /// <param name="labId">The lab to act as.</param>
    /// <returns>A context scoped to that lab.</returns>
    public SpecimenCheckInContext CreateContextFor(int labId)
    {
        TenantContext tenant = new();
        tenant.Resolve(labId);

        return this.CreateContext(tenant);
    }

    /// <summary>
    /// Opens a context with no lab resolved, as untenanted code would.
    /// </summary>
    /// <returns>A context with no tenant.</returns>
    public SpecimenCheckInContext CreateUntenantedContext() => this.CreateContext(new TenantContext());

    private SpecimenCheckInContext CreateContext(ITenantContext tenant)
    {
        DbContextOptionsBuilder<SpecimenCheckInContext> options = new();

        options
            .UseSqlServer(ConnectionString, sql => sql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                SpecimenCheckInContext.Schema))
            .AddInterceptors(new TenantSessionInterceptor(tenant));

        return new SpecimenCheckInContext(options.Options, tenant);
    }
}

/// <summary>
/// Shares one database across the test classes that need it.
/// </summary>
[CollectionDefinition(TestDatabase.CollectionName)]
public class TestDatabaseCollection : ICollectionFixture<TestDatabase>;
