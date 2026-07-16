using SpecimenCheckIn.Models;
using SpecimenCheckIn.Models.Errors;

namespace SpecimenCheckIn.Tests;

/// <summary>
/// The rules that decide when a manifest may be closed, and what closing it means.
/// </summary>
/// <remarks>
/// No database here on purpose: these are the domain's rules, and they are worth being
/// able to state and check without a server involved.
/// </remarks>
public class ReconciliationTests
{
    private static readonly DateTime At = new(2026, 5, 26, 11, 2, 0, DateTimeKind.Utc);

    [Fact]
    public void A_manifest_with_pending_bottles_cannot_be_closed()
    {
        Manifest manifest = ManifestOf(SpecimenStatus.Received, SpecimenStatus.Pending);

        RuleViolationException refused = Assert.Throws<RuleViolationException>(manifest.Close);

        Assert.Equal("manifest_not_reconciled", refused.Code);
        Assert.Equal(ManifestStatus.Open, manifest.Status);
    }

    [Fact]
    public void A_manifest_whose_bottles_all_arrived_closes_cleanly()
    {
        Manifest manifest = ManifestOf(SpecimenStatus.Received, SpecimenStatus.Received);

        manifest.Close();

        Assert.Equal(ManifestStatus.Closed, manifest.Status);
    }

    [Fact]
    public void A_manifest_with_a_flagged_bottle_closes_as_ClosedWithDiscrepancy()
    {
        Manifest manifest = ManifestOf(SpecimenStatus.Received, SpecimenStatus.Flagged);

        manifest.Close();

        // Reconciled is not the same as complete: the lab knows the bottle is missing, so
        // it can close — but the outcome has to stay distinguishable from a clean close.
        Assert.Equal(ManifestStatus.ClosedWithDiscrepancy, manifest.Status);
    }

    [Fact]
    public void A_closed_manifest_cannot_be_closed_again()
    {
        Manifest manifest = ManifestOf(SpecimenStatus.Received);
        manifest.Close();

        RuleViolationException refused = Assert.Throws<RuleViolationException>(manifest.Close);

        Assert.Equal("manifest_already_closed", refused.Code);
    }

    [Fact]
    public void An_empty_manifest_counts_as_reconciled()
    {
        Manifest manifest = ManifestOf();

        manifest.Close();

        // Nothing pending means nothing unknown. Worth pinning down: the alternative
        // reading — that an empty manifest is somehow unfinished — would strand it open
        // with nothing a technician could do to resolve it.
        Assert.Equal(ManifestStatus.Closed, manifest.Status);
    }

    [Fact]
    public void Counts_report_what_the_screen_shows()
    {
        Manifest manifest = ManifestOf(
            SpecimenStatus.Received,
            SpecimenStatus.Received,
            SpecimenStatus.Pending,
            SpecimenStatus.Flagged);

        ManifestCounts counts = manifest.Count();

        Assert.Equal(new ManifestCounts(Expected: 4, Received: 2, Pending: 1, Flagged: 1), counts);
        Assert.False(counts.IsReconciled);
    }

    [Fact]
    public void Receiving_a_bottle_twice_changes_nothing_the_second_time()
    {
        Specimen specimen = SpecimenOf(SpecimenStatus.Pending);

        Assert.True(specimen.Receive("Lab Tech 1", At));
        Assert.False(specimen.Receive("Lab Tech 2", At.AddHours(3)));

        // The second scan is reported as a no-op, which is what keeps counts from drifting,
        // and the record still says who actually received it and when.
        Assert.Equal("Lab Tech 1", specimen.ReceivedBy);
        Assert.Equal(At, specimen.ReceivedAt);
    }

    [Fact]
    public void Flagging_a_received_bottle_clears_its_receipt()
    {
        Specimen specimen = SpecimenOf(SpecimenStatus.Pending);
        specimen.Receive("Lab Tech 1", At);

        Assert.True(specimen.Flag());

        // A bottle cannot be both missing and in hand; leaving the receipt behind would
        // let the record claim someone received what is not there.
        Assert.Equal(SpecimenStatus.Flagged, specimen.Status);
        Assert.Null(specimen.ReceivedBy);
        Assert.Null(specimen.ReceivedAt);
    }

    [Fact]
    public void A_bottle_can_be_received_after_being_flagged()
    {
        Specimen specimen = SpecimenOf(SpecimenStatus.Flagged);

        // Missing bottles turn up — under a bench, in the next tray. Flagging is a
        // statement about now, not a verdict, so it has to be reversible.
        Assert.True(specimen.Receive("Lab Tech 1", At));
        Assert.Equal(SpecimenStatus.Received, specimen.Status);
    }

    private static Manifest ManifestOf(params SpecimenStatus[] specimens)
    {
        Manifest manifest = new()
        {
            Id = Guid.NewGuid(),
            Code = "MF-2026-0042",
            OriginClinic = "Riverside Clinic — Bay 2",
            Status = ManifestStatus.Open,
            SentAt = At,
        };

        foreach (SpecimenStatus status in specimens)
        {
            manifest.Specimens.Add(SpecimenOf(status));
        }

        return manifest;
    }

    private static Specimen SpecimenOf(SpecimenStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Code = $"SP-{Guid.NewGuid():N}"[..12],
        Patient = "Sarah Lin",
        Site = "Right cheek",
        Provider = "Dr. Patel",
        Status = status,
    };
}
