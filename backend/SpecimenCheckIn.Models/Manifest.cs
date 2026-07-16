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
}
