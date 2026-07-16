using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Sql;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Models;

namespace SpecimenCheckIn.Tests;

/// <summary>
/// Guards the isolation as the codebase grows.
/// </summary>
/// <remarks>
/// The isolation itself is proven by <see cref="TenantIsolationTests"/>, but those tests
/// only cover the tables someone remembered to write a test for. These read the model and
/// assert that <em>every</em> tenant-owned table is protected — so a table added next year
/// cannot slip in unfiltered and unpoliced without a test going red.
/// </remarks>
public class TenantCoverageTests
{
    private static readonly IModel Model = BuildModel();

    [Fact]
    public void Every_tenant_owned_table_is_covered_by_the_security_policy()
    {
        IEnumerable<string> tenantOwnedTables = TenantOwnedEntityTypes()
            .Select(entityType => entityType.GetTableName()!)
            .Order();

        Assert.Equal(RowLevelSecurity.ProtectedTables.Order(), tenantOwnedTables);
    }

    [Fact]
    public void Every_tenant_owned_entity_is_filtered_to_the_current_lab()
    {
        IEnumerable<IEntityType> unfiltered = TenantOwnedEntityTypes()
            .Where(entityType => entityType.GetQueryFilter() is null);

        Assert.Empty(unfiltered.Select(entityType => entityType.ClrType.Name));
    }

    [Fact]
    public void The_lab_registry_itself_is_not_tenant_scoped()
    {
        IEntityType lab = Model.FindEntityType(typeof(Lab))!;

        // Resolving the tenant means reading this table, which has to work before a tenant
        // exists. Its absence from the policy is a decision, so it is asserted rather than
        // left to be rediscovered as a surprise.
        Assert.Null(lab.GetQueryFilter());
        Assert.DoesNotContain(lab.GetTableName(), RowLevelSecurity.ProtectedTables);
    }

    private static IEnumerable<IEntityType> TenantOwnedEntityTypes() =>
        Model.GetEntityTypes()
            .Where(entityType => typeof(TenantOwnedEntity).IsAssignableFrom(entityType.ClrType));

    private static IModel BuildModel()
    {
        DbContextOptionsBuilder<SpecimenCheckInContext> options = new();
        options.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SpecimenCheckIn_ModelOnly");

        TenantContext tenant = new();
        tenant.Resolve(TestDatabase.CentralLabId);

        using SpecimenCheckInContext context = new(options.Options, tenant);

        // Reads the shape of the model only; never opens a connection.
        return context.Model;
    }
}
