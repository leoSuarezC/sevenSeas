namespace SpecimenCheckIn.Models;

/// <summary>
/// A single bottle listed on a manifest.
/// </summary>
public class Specimen : TenantOwnedEntity
{
    /// <summary>
    /// Gets or sets the specimen id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the manifest this specimen belongs to.
    /// </summary>
    public Guid ManifestId { get; set; }

    /// <summary>
    /// Gets or sets the barcode shown on the bottle, e.g. "SP-2026-A0041".
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient the specimen was taken from (synthetic data only).
    /// </summary>
    public string Patient { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body site sampled, e.g. "Left forearm".
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordering provider, e.g. "Dr. Patel".
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the bottle has arrived.
    /// </summary>
    public SpecimenStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the lab tech who checked the bottle in, if it has been received.
    /// </summary>
    public string? ReceivedBy { get; set; }

    /// <summary>
    /// Gets or sets when the bottle was checked in (UTC), if it has been received.
    /// </summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>
    /// Gets or sets the manifest this specimen belongs to.
    /// </summary>
    public Manifest? Manifest { get; set; }

    /// <summary>
    /// Records that the bottle physically arrived.
    /// </summary>
    /// <param name="labTech">The technician checking it in.</param>
    /// <param name="at">When it was checked in (UTC).</param>
    /// <returns>
    /// <see langword="true"/> if this changed anything; <see langword="false"/> if the
    /// bottle was already received.
    /// </returns>
    /// <remarks>
    /// Idempotent, and it has to be: bottles get scanned twice at a busy desk. A repeat
    /// scan keeps the original technician and timestamp rather than rewriting who
    /// received it, and reports that nothing changed so counts cannot drift.
    /// </remarks>
    public bool Receive(string labTech, DateTime at)
    {
        if (this.Status == SpecimenStatus.Received)
        {
            return false;
        }

        this.Status = SpecimenStatus.Received;
        this.ReceivedBy = labTech;
        this.ReceivedAt = at;

        return true;
    }

    /// <summary>
    /// Reports the bottle as missing.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this changed anything; <see langword="false"/> if the
    /// bottle was already flagged.
    /// </returns>
    /// <remarks>
    /// Also idempotent, so flagging twice raises one discrepancy rather than two.
    /// </remarks>
    public bool Flag()
    {
        if (this.Status == SpecimenStatus.Flagged)
        {
            return false;
        }

        this.Status = SpecimenStatus.Flagged;

        // A bottle cannot be both missing and in hand: reporting it missing clears any
        // receipt details, so the record cannot claim someone received what is absent.
        this.ReceivedBy = null;
        this.ReceivedAt = null;

        return true;
    }
}
