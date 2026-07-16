namespace SpecimenCheckIn.Models;

/// <summary>
/// An immutable record of a check-in action.
/// </summary>
/// <remarks>
/// Append-only by design: there is no endpoint that updates or deletes an audit event,
/// and the DbContext rejects any attempt to modify one. Handling patient specimens is
/// regulated work — the log has to be able to answer "who touched this bottle, and when"
/// long after the fact, which it cannot do if entries can be rewritten.
/// </remarks>
public class AuditEvent : TenantOwnedEntity
{
    /// <summary>
    /// Gets or sets the audit event id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the manifest the action was performed against.
    /// </summary>
    public Guid ManifestId { get; set; }

    /// <summary>
    /// Gets or sets the specimen the action was performed against, if the action was
    /// specimen-level rather than manifest-level.
    /// </summary>
    public Guid? SpecimenId { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// Gets or sets the lab tech who performed the action.
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the action happened (UTC).
    /// </summary>
    public DateTime At { get; set; }

    /// <summary>
    /// Gets or sets a short human-readable summary of the action.
    /// </summary>
    /// <remarks>
    /// Kept free of patient identifiers: the ids above are enough to reconstruct the
    /// full picture from the tenant's own data, so the log itself needs no PHI.
    /// </remarks>
    public string? Details { get; set; }
}
