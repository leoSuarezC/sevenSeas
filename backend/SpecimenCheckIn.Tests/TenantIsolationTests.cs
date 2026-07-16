using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Models;

namespace SpecimenCheckIn.Tests;

/// <summary>
/// Proves one lab cannot reach another's data.
/// </summary>
/// <param name="database">The shared test database.</param>
[Collection(TestDatabase.CollectionName)]
public class TenantIsolationTests(TestDatabase database)
{
    [Fact]
    public async Task A_lab_reads_only_its_own_manifests()
    {
        await GiveManifestTo(TestDatabase.CentralLabId, "MF-CENTRAL-READ");
        await GiveManifestTo(TestDatabase.WestsideLabId, "MF-WESTSIDE-READ");

        await using SpecimenCheckInContext central = database.CreateContextFor(TestDatabase.CentralLabId);
        List<Manifest> visible = await central.Manifests.ToListAsync();

        Assert.Contains(visible, manifest => manifest.Code == "MF-CENTRAL-READ");
        Assert.DoesNotContain(visible, manifest => manifest.Code == "MF-WESTSIDE-READ");
        Assert.All(visible, manifest => Assert.Equal(TestDatabase.CentralLabId, manifest.LabId));
    }

    [Fact]
    public async Task Another_labs_manifest_is_not_found_even_by_id()
    {
        Guid westsideManifestId = await GiveManifestTo(TestDatabase.WestsideLabId, "MF-WESTSIDE-BY-ID");

        await using SpecimenCheckInContext central = database.CreateContextFor(TestDatabase.CentralLabId);
        Manifest? stolen = await central.Manifests.FirstOrDefaultAsync(manifest => manifest.Id == westsideManifestId);

        // Knowing the id buys nothing: to the Central Lab the row does not exist, which is what
        // lets the API answer 404 rather than leaking that it exists somewhere else.
        Assert.Null(stolen);
    }

    [Fact]
    public async Task A_query_that_ignores_the_global_filter_still_cannot_see_another_lab()
    {
        await GiveManifestTo(TestDatabase.CentralLabId, "MF-CENTRAL-UNFILTERED");
        await GiveManifestTo(TestDatabase.WestsideLabId, "MF-WESTSIDE-UNFILTERED");

        await using SpecimenCheckInContext central = database.CreateContextFor(TestDatabase.CentralLabId);

        // Simulates the mistake the query filter cannot catch: code that deliberately or
        // accidentally opts out of it. Row-level security is why this still comes back
        // scoped — the isolation does not depend on the application remembering.
        List<string> visible = await central.Manifests
            .IgnoreQueryFilters()
            .Select(manifest => manifest.Code)
            .ToListAsync();

        // Asserted in both directions: that the lab's own row survives the query proves
        // the result is not simply empty, which would pass a "sees nothing else" check
        // without proving anything at all.
        Assert.Contains("MF-CENTRAL-UNFILTERED", visible);
        Assert.DoesNotContain("MF-WESTSIDE-UNFILTERED", visible);
    }

    [Fact]
    public async Task The_database_stamps_the_lab_on_insert()
    {
        Guid manifestId = await GiveManifestTo(TestDatabase.WestsideLabId, "MF-WESTSIDE-STAMPED");

        await using SpecimenCheckInContext westside = database.CreateContextFor(TestDatabase.WestsideLabId);
        Manifest stored = await westside.Manifests.SingleAsync(manifest => manifest.Id == manifestId);

        // Nothing in the test set LabId — it is not settable. The value can only have come
        // from the session context the interceptor published.
        Assert.Equal(TestDatabase.WestsideLabId, stored.LabId);
    }

    [Fact]
    public async Task Writing_without_a_tenant_is_rejected()
    {
        await using SpecimenCheckInContext untenanted = database.CreateUntenantedContext();
        untenanted.Manifests.Add(NewManifest("MF-NO-TENANT"));

        // Fails closed: with no lab in session context the LabId default resolves to NULL
        // and the column refuses it, so tenant data cannot be written by tenant-less code.
        await Assert.ThrowsAnyAsync<Exception>(() => untenanted.SaveChangesAsync());
    }

    [Fact]
    public async Task Reading_without_a_tenant_is_rejected()
    {
        await using SpecimenCheckInContext untenanted = database.CreateUntenantedContext();

        // The query filter cannot name a lab, so the read throws rather than falling back
        // to "no filter", which is the failure mode that would return everyone's rows.
        await Assert.ThrowsAsync<InvalidOperationException>(() => untenanted.Manifests.ToListAsync());
    }

    private static Manifest NewManifest(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        OriginClinic = "Somewhere Clinic — Bay 1",
        Status = ManifestStatus.Open,
        SentAt = DateTime.UtcNow,
    };

    private async Task<Guid> GiveManifestTo(int labId, string code)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(labId);

        Manifest manifest = NewManifest(code);
        context.Manifests.Add(manifest);
        await context.SaveChangesAsync();

        return manifest.Id;
    }
}
