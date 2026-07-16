using SpecimenCheckIn.Models.Errors;

namespace SpecimenCheckIn.Models;

/// <summary>
/// An itemised list of the bottles a clinic says it shipped. The technician checks
/// physical arrivals against it and closes it once it reconciles.
/// </summary>
public class Manifest : TenantOwnedEntity
{
    /// <summary>
    /// Gets or sets the manifest id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the human-readable code shown to technicians, e.g. "MF-2026-0042".
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets where the shipment came from, e.g. "Riverside Clinic — Bay 2".
    /// </summary>
    public string OriginClinic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current workflow status.
    /// </summary>
    public ManifestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the clinic dispatched the shipment (UTC).
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token.
    /// </summary>
    /// <remarks>
    /// Two technicians can work the same receiving desk. This makes closing a manifest
    /// fail loudly rather than silently overwrite a concurrent check-in.
    /// </remarks>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// Gets the specimens the manifest says should have arrived.
    /// </summary>
    public ICollection<Specimen> Specimens { get; private set; } = new List<Specimen>();

    /// <summary>
    /// Gets the mismatches raised against this manifest.
    /// </summary>
    public ICollection<Discrepancy> Discrepancies { get; private set; } = new List<Discrepancy>();

    /// <summary>
    /// Gets a value indicating whether the manifest has been closed.
    /// </summary>
    public bool IsClosed => this.Status is ManifestStatus.Closed or ManifestStatus.ClosedWithDiscrepancy;

    /// <summary>
    /// Tallies the manifest against what has actually been checked in.
    /// </summary>
    /// <returns>The running counts.</returns>
    /// <remarks>
    /// Requires <see cref="Specimens"/> to be loaded. Counted in one pass here rather than
    /// recomputed by each caller, so the API, the close rule and the UI cannot disagree
    /// about what "received" means.
    /// </remarks>
    public ManifestCounts Count()
    {
        int received = 0;
        int pending = 0;
        int flagged = 0;

        foreach (Specimen specimen in this.Specimens)
        {
            switch (specimen.Status)
            {
                case SpecimenStatus.Received:
                    received++;
                    break;
                case SpecimenStatus.Flagged:
                    flagged++;
                    break;
                default:
                    pending++;
                    break;
            }
        }

        return new ManifestCounts(this.Specimens.Count, received, pending, flagged);
    }

    /// <summary>
    /// Closes the manifest once every bottle has been accounted for.
    /// </summary>
    /// <exception cref="RuleViolationException">
    /// Thrown if the manifest is already closed, or if bottles are still pending.
    /// </exception>
    /// <remarks>
    /// A manifest with flagged bottles still closes — but as
    /// <see cref="ManifestStatus.ClosedWithDiscrepancy"/>, so "we finished" and "we
    /// finished, and something was missing" stay distinguishable afterwards.
    /// </remarks>
    public void Close()
    {
        if (this.IsClosed)
        {
            throw new RuleViolationException(
                "manifest_already_closed",
                $"Manifest {this.Code} is already closed.");
        }

        ManifestCounts counts = this.Count();

        if (!counts.IsReconciled)
        {
            throw new RuleViolationException(
                "manifest_not_reconciled",
                $"Manifest {this.Code} still has {counts.Pending} specimen(s) pending. "
                + "Receive them or flag them missing before closing.");
        }

        this.Status = counts.Flagged > 0
            ? ManifestStatus.ClosedWithDiscrepancy
            : ManifestStatus.Closed;
    }

    /// <summary>
    /// Guards against checking bottles in or out of a manifest that is already closed.
    /// </summary>
    /// <exception cref="RuleViolationException">Thrown if the manifest is closed.</exception>
    public void GuardIsOpen()
    {
        if (this.IsClosed)
        {
            throw new RuleViolationException(
                "manifest_closed",
                $"Manifest {this.Code} is closed and can no longer be changed.");
        }
    }
}
