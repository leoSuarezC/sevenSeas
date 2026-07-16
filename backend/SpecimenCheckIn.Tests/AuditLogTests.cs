using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Commands.Manifests;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Models;

namespace SpecimenCheckIn.Tests;

/// <summary>
/// The audit log: what it records, and that it cannot be rewritten afterwards.
/// </summary>
/// <param name="database">The shared test database.</param>
[Collection(TestDatabase.CollectionName)]
public class AuditLogTests(TestDatabase database)
{
    private static readonly DateTime At = new(2026, 5, 26, 11, 2, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Receiving_a_bottle_records_who_did_it_and_when()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);

        await WhenActingAs("Lab Tech 7", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        AuditEvent recorded = await SingleEventFor(manifestId);

        Assert.Equal(AuditAction.SpecimenReceived, recorded.Action);
        Assert.Equal(specimenId, recorded.SpecimenId);
        Assert.Equal("Lab Tech 7", recorded.Actor);
        Assert.Equal(At, recorded.At);
    }

    [Fact]
    public async Task Flagging_a_bottle_is_recorded()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);

        await WhenActingAs("Lab Tech 1", commands => commands.FlagSpecimenAsync(manifestId, specimenId));

        AuditEvent recorded = await SingleEventFor(manifestId);

        Assert.Equal(AuditAction.SpecimenFlagged, recorded.Action);
        Assert.Equal(specimenId, recorded.SpecimenId);
    }

    [Fact]
    public async Task Closing_a_manifest_records_the_outcome()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Received, SpecimenStatus.Flagged);

        await WhenActingAs("Lab Tech 1", commands => commands.CloseManifestAsync(manifestId));

        AuditEvent recorded = await SingleEventFor(manifestId);

        Assert.Equal(AuditAction.ManifestClosed, recorded.Action);
        Assert.Null(recorded.SpecimenId);

        // The outcome is the fact the action alone does not carry: months later, this is
        // what says the shipment finished with something missing.
        Assert.Equal(nameof(ManifestStatus.ClosedWithDiscrepancy), recorded.Details);
    }

    [Fact]
    public async Task A_repeat_scan_records_nothing_new()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);

        await WhenActingAs("Lab Tech 1", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));
        await WhenActingAs("Lab Tech 1", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);
        int recorded = await context.AuditEvents.CountAsync(item => item.ManifestId == manifestId);

        // Idempotent means the second call leaves the system identical — audit log included.
        // A log entry for a change that did not happen would make the history a record of
        // button presses rather than of what became of the shipment.
        Assert.Equal(1, recorded);
    }

    [Fact]
    public async Task An_audit_entry_cannot_be_rewritten()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);
        await WhenActingAs("Lab Tech 1", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);
        AuditEvent recorded = await context.AuditEvents.FirstAsync(item => item.ManifestId == manifestId);

        recorded.Actor = "Someone Else";

        InvalidOperationException refused =
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        Assert.Contains("append-only", refused.Message);
    }

    [Fact]
    public async Task An_audit_entry_cannot_be_deleted()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);
        await WhenActingAs("Lab Tech 1", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);
        AuditEvent recorded = await context.AuditEvents.FirstAsync(item => item.ManifestId == manifestId);

        context.AuditEvents.Remove(recorded);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task An_audit_entry_cannot_be_rewritten_by_going_around_the_application()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);
        await WhenActingAs("Lab Tech 1", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        // Raw SQL: exactly what the SaveChanges guard cannot see. If immutability lived only
        // in the application, this would quietly succeed — and a log that a script can
        // rewrite is not evidence of anything.
        Exception refused = await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync(
                "UPDATE [SpecimenCheckIn].[AuditEvents] SET Actor = 'Someone Else' WHERE ManifestId = {0}",
                manifestId));

        Assert.Contains("append-only", refused.GetBaseException().Message);

        await using SpecimenCheckInContext verification = database.CreateContextFor(TestDatabase.CentralLabId);
        AuditEvent untouched = await verification.AuditEvents.FirstAsync(item => item.ManifestId == manifestId);

        Assert.Equal("Lab Tech 1", untouched.Actor);
    }

    [Fact]
    public async Task An_audit_entry_cannot_be_deleted_by_going_around_the_application()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);
        await WhenActingAs("Lab Tech 1", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync(
                "DELETE FROM [SpecimenCheckIn].[AuditEvents] WHERE ManifestId = {0}",
                manifestId));

        await using SpecimenCheckInContext verification = database.CreateContextFor(TestDatabase.CentralLabId);

        Assert.True(await verification.AuditEvents.AnyAsync(item => item.ManifestId == manifestId));
    }

    [Fact]
    public async Task Another_labs_audit_trail_is_invisible()
    {
        Guid manifestId = await GivenManifestWith(SpecimenStatus.Pending);
        Guid specimenId = await PendingSpecimenOf(manifestId);
        await WhenActingAs("Lab Tech 1", commands => commands.ReceiveSpecimenAsync(manifestId, specimenId));

        await using SpecimenCheckInContext westside = database.CreateContextFor(TestDatabase.WestsideLabId);
        bool anyVisible = await westside.AuditEvents.AnyAsync(item => item.ManifestId == manifestId);

        // The log is tenant-owned like everything else: who handled a lab's specimens is
        // that lab's business.
        Assert.False(anyVisible);
    }

    private async Task<AuditEvent> SingleEventFor(Guid manifestId)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        return await context.AuditEvents.SingleAsync(item => item.ManifestId == manifestId);
    }

    private async Task<Guid> PendingSpecimenOf(Guid manifestId)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        return await context.Specimens
            .Where(specimen => specimen.ManifestId == manifestId && specimen.Status == SpecimenStatus.Pending)
            .Select(specimen => specimen.Id)
            .FirstAsync();
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
            SentAt = At.AddHours(-2),
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

    private async Task WhenActingAs(string labTech, Func<CheckInCommands, Task> action)
    {
        await using SpecimenCheckInContext context = database.CreateContextFor(TestDatabase.CentralLabId);

        // A fixed clock, so the recorded timestamp is something to assert rather than
        // something to tolerate.
        CheckInCommands commands = new(
            context,
            new StubUser(labTech),
            new FixedClock(At));

        await action(commands);
    }

    private sealed class StubUser(string labTech) : IUserContext
    {
        public string LabTech => labTech;
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
