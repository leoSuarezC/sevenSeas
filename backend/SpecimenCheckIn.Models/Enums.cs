namespace SpecimenCheckIn.Models;

/// <summary>
/// Where a manifest is in the check-in workflow.
/// </summary>
public enum ManifestStatus
{
    /// <summary>Still being checked in; specimens may be pending.</summary>
    Open,

    /// <summary>Every specimen was received and accounted for.</summary>
    Closed,

    /// <summary>Reconciled, but closed with at least one unresolved discrepancy.</summary>
    ClosedWithDiscrepancy,
}

/// <summary>
/// Whether a bottle listed on the manifest has physically turned up.
/// </summary>
public enum SpecimenStatus
{
    /// <summary>On the manifest, not yet checked in.</summary>
    Pending,

    /// <summary>Physically received at the desk.</summary>
    Received,

    /// <summary>Reported missing; carries an open discrepancy.</summary>
    Flagged,
}

/// <summary>
/// What kind of mismatch a discrepancy records.
/// </summary>
public enum DiscrepancyType
{
    /// <summary>The manifest listed a bottle that did not arrive.</summary>
    Missing,

    /// <summary>A bottle arrived that the manifest does not list.</summary>
    OffManifest,
}

/// <summary>
/// Whether a discrepancy still needs attention.
/// </summary>
public enum DiscrepancyStatus
{
    /// <summary>Raised and unresolved.</summary>
    Open,

    /// <summary>Investigated and settled.</summary>
    Resolved,
}

/// <summary>
/// The check-in actions recorded in the audit log.
/// </summary>
public enum AuditAction
{
    /// <summary>A specimen was marked received.</summary>
    SpecimenReceived,

    /// <summary>A specimen was flagged missing.</summary>
    SpecimenFlagged,

    /// <summary>A manifest was closed.</summary>
    ManifestClosed,
}
