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
}
