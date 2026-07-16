using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Commands.Manifests;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Models;
using SpecimenCheckIn.Models.Errors;

namespace SpecimenCheckIn.Tests;

/// <summary>
/// The check-in actions, exercised against a real database.
/// </summary>
/// <param name="database">The shared test database.</param>
[Collection(TestDatabase.CollectionName)]
public class CheckInCommandTests(TestDatabase database)
{
    [Fact]
    public async Task Receiving_the_same_bottle_twice_does_not_corrupt_the_counts()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending, SpecimenStatus.Pending);
        Guid specimenId = await FirstSpecimenOf(manifestId);

        await WhenActingAsCentralLab(commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));
        await WhenActingAsCentralLab(commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        ManifestCounts counts = await CountsOf(manifestId);

        // The double scan is the point: one received, not two, and nothing invented.
        Assert.Equal(new ManifestCounts(Expected: 2, Received: 1, Pending: 1, Flagged: 0), counts);
    }

    [Fact]
    public async Task Flagging_the_same_bottle_twice_raises_one_discrepancy()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await FirstSpecimenOf(manifestId);

        await WhenActingAsCentralLab(commands => commands.FlagSpecimenAsync(manifestId, specimenId));
        await WhenActingAsCentralLab(commands => commands.FlagSpecimenAsync(manifestId, specimenId));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);
        int raised = await context.Discrepancies.CountAsync(discrepancy => discrepancy.SpecimenId == specimenId);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Flagging_a_bottle_raises_a_discrepancy_against_it()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await FirstSpecimenOf(manifestId);

        await WhenActingAsCentralLab(commands => commands.FlagSpecimenAsync(manifestId, specimenId, "Not in shipment"));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);
        Discrepancy discrepancy = await context.Discrepancies.SingleAsync(item => item.SpecimenId == specimenId);

        Assert.Equal(DiscrepancyType.Missing, discrepancy.Type);
        Assert.Equal(DiscrepancyStatus.Open, discrepancy.Status);
        Assert.Equal("Not in shipment", discrepancy.Notes);
    }

    [Fact]
    public async Task A_manifest_cannot_be_closed_while_a_bottle_is_pending()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Received, SpecimenStatus.Pending);

        RuleViolationException refused = await Assert.ThrowsAsync<RuleViolationException>(
            () => WhenActingAsCentralLab(commands => commands.CloseManifestAsync(manifestId)));

        Assert.Equal("manifest_not_reconciled", refused.Code);
    }

    [Fact]
    public async Task Flagging_the_last_pending_bottle_makes_a_manifest_closable()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Received, SpecimenStatus.Pending);
        Guid pendingSpecimenId = await SpecimenOf(manifestId, SpecimenStatus.Pending);

        await WhenActingAsCentralLab(commands => commands.FlagSpecimenAsync(manifestId, pendingSpecimenId));
        await WhenActingAsCentralLab(commands => commands.CloseManifestAsync(manifestId));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);
        Manifest manifest = await context.Manifests.SingleAsync(item => item.Id == manifestId);

        // The whole workflow in one test: a bottle never turned up, the technician said so,
        // and the manifest closed carrying that fact rather than pretending otherwise.
        Assert.Equal(ManifestStatus.ClosedWithDiscrepancy, manifest.Status);
    }

    [Fact]
    public async Task A_closed_manifest_will_not_accept_more_bottles()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Received);
        Guid specimenId = await FirstSpecimenOf(manifestId);

        await WhenActingAsCentralLab(commands => commands.CloseManifestAsync(manifestId));

        RuleViolationException refused = await Assert.ThrowsAsync<RuleViolationException>(
            () => WhenActingAsCentralLab(commands => commands.ReceiveSpecimenAsync(manifestId, specimenId)));

        Assert.Equal("manifest_closed", refused.Code);
    }

    [Fact]
    public async Task Another_labs_manifest_cannot_be_acted_on()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await FirstSpecimenOf(manifestId);

        // Same ids, wrong lab. The isolation is not a UI concern: the command itself cannot
        // reach across, and it reports the manifest as absent rather than forbidden.
        NotFoundException refused = await Assert.ThrowsAsync<NotFoundException>(
            () => WhenActingAs(TestDatabase.WestsideLabId, commands => commands.ReceiveSpecimenAsync(manifestId, specimenId)));

        Assert.Equal("not_found", refused.Code);
    }

    private async Task<Guid> GivenManifestWith(params SpecimenStatus[] specimens)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        Manifest manifest = new()
        {
            Id = Guid.NewGuid(),
            Code = $"MF-{Guid.NewGuid():N}"[..12],
            OriginClinic = "Riverside Clinic — Bay 2",
            Status = ManifestStatus.Open,
            SentAt = new DateTime(2026, 5, 26, 10, 48, 0, DateTimeKind.Utc),
        };

        int index = 0;

        foreach (SpecimenStatus status in specimens)
        {
            manifest.Specimens.Add(new Specimen
            {
                Id = Guid.NewGuid(),
                Code = $"SP-{index++:0000}",
                Patient = "Sarah Lin",
                Site = "Right cheek",
                Provider = "Dr. Patel",
                Status = status,
            });
        }

        context.Manifests.Add(manifest);
        await context.SaveChangesAsync();

        return manifest.Id;
    }

    private async Task<Guid> FirstSpecimenOf(Guid manifestId)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        return await context.Specimens
            .Where(specimen => specimen.ManifestId == manifestId)
            .OrderBy(specimen => specimen.Code)
            .Select(specimen => specimen.Id)
            .FirstAsync();
    }

    private async Task<Guid> SpecimenOf(Guid manifestId, SpecimenStatus status)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        return await context.Specimens
            .Where(specimen => specimen.ManifestId == manifestId && specimen.Status == status)
            .Select(specimen => specimen.Id)
            .FirstAsync();
    }

    private async Task<ManifestCounts> CountsOf(Guid manifestId)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        Manifest manifest = await context.Manifests
            .Include(item => item.Specimens)
            .SingleAsync(item => item.Id == manifestId);

        return manifest.Count();
    }

    private Task WhenActingAsCentralLab(Func<CheckInCommands, Task> action) =>
        this.WhenActingAs(TestDatabase.CentralLabId, action);

    private async Task WhenActingAs(int labId, Func<CheckInCommands, Task> action)
    {
        // A fresh context per action, as a request would get: nothing carries over in the
        // change tracker to make an operation look like it worked when it would not.
        await using SpecimenCheckInContext context = database.CreateContextFor(labId);

        CheckInCommands commands = new(context, new StubUser(), TimeProvider.System);

        await action(commands);
    }

    private sealed class StubUser : IUserContext
    {
        public string LabTech => "Lab Tech 1";
    }
}
