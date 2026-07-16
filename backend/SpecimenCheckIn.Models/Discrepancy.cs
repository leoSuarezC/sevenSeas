namespace SpecimenCheckIn.Models;

/// <summary>
/// A tracked mismatch between what the manifest listed and what physically arrived.
/// Must be resolved or acknowledged before the manifest can be closed.
/// </summary>
public class Discrepancy : TenantOwnedEntity
{
    /// <summary>
    /// Gets or sets the discrepancy id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the manifest the mismatch was found on.
    /// </summary>
    public Guid ManifestId { get; set; }

    /// <summary>
    /// Gets or sets the specimen involved.
    /// </summary>
    /// <remarks>
    /// Null for an off-manifest bottle, which by definition has no listed specimen.
    /// </remarks>
    public Guid? SpecimenId { get; set; }

    /// <summary>
    /// Gets or sets the kind of mismatch.
    /// </summary>
    public DiscrepancyType Type { get; set; }

    /// <summary>
    /// Gets or sets whether the mismatch is still outstanding.
    /// </summary>
    public DiscrepancyStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the mismatch was raised (UTC).
    /// </summary>
    public DateTime RaisedAt { get; set; }

    /// <summary>
    /// Gets or sets free-text context recorded by the technician.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the manifest the mismatch was found on.
    /// </summary>
    public Manifest? Manifest { get; set; }

    /// <summary>
    /// Gets or sets the specimen involved, if any.
    /// </summary>
    public Specimen? Specimen { get; set; }
}
