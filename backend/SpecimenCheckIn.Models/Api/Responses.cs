namespace SpecimenCheckIn.Models.Api;

/// <summary>
/// Who the current request is acting as.
/// </summary>
/// <param name="LabId">The lab.</param>
/// <param name="LabName">The lab's name, as shown in the header.</param>
/// <param name="LabTech">The technician at the desk.</param>
public record SessionResponse(int LabId, string LabName, string LabTech);

/// <summary>
/// The running tally shown above the specimen table.
/// </summary>
/// <param name="Expected">Bottles the manifest lists.</param>
/// <param name="Received">Bottles physically checked in.</param>
/// <param name="Pending">Bottles still unaccounted for.</param>
/// <param name="Flagged">Bottles reported missing.</param>
/// <param name="ReadyToClose">Whether nothing is left pending.</param>
public record CountsResponse(int Expected, int Received, int Pending, int Flagged, bool ReadyToClose)
{
    /// <summary>
    /// Projects the domain tally onto the wire.
    /// </summary>
    /// <param name="counts">The counts to project.</param>
    /// <returns>The response shape.</returns>
    public static CountsResponse From(ManifestCounts counts) =>
        new(counts.Expected, counts.Received, counts.Pending, counts.Flagged, counts.IsReconciled);
}

/// <summary>
/// A manifest as it appears in the left-hand worklist.
/// </summary>
/// <param name="Id">The manifest id.</param>
/// <param name="Code">The code shown to technicians.</param>
/// <param name="OriginClinic">Where the shipment came from.</param>
/// <param name="Status">Where it is in the workflow.</param>
/// <param name="SentAt">When the clinic dispatched it (UTC).</param>
/// <param name="Counts">The running tally.</param>
/// <param name="OpenDiscrepancies">Unresolved mismatches raised against it.</param>
public record ManifestSummaryResponse(
    Guid Id,
    string Code,
    string OriginClinic,
    string Status,
    DateTime SentAt,
    CountsResponse Counts,
    int OpenDiscrepancies);

/// <summary>
/// A bottle as it appears in the specimen table.
/// </summary>
/// <param name="Id">The specimen id.</param>
/// <param name="Code">The barcode on the bottle.</param>
/// <param name="Patient">The patient it was taken from.</param>
/// <param name="Site">The body site sampled.</param>
/// <param name="Provider">The ordering provider.</param>
/// <param name="Status">Whether it has arrived.</param>
/// <param name="ReceivedBy">Who checked it in, if anyone.</param>
/// <param name="ReceivedAt">When it was checked in (UTC), if it was.</param>
public record SpecimenResponse(
    Guid Id,
    string Code,
    string Patient,
    string Site,
    string Provider,
    string Status,
    string? ReceivedBy,
    DateTime? ReceivedAt);

/// <summary>
/// A manifest with everything the check-in screen needs to render it.
/// </summary>
/// <param name="Id">The manifest id.</param>
/// <param name="Code">The code shown to technicians.</param>
/// <param name="OriginClinic">Where the shipment came from.</param>
/// <param name="Status">Where it is in the workflow.</param>
/// <param name="SentAt">When the clinic dispatched it (UTC).</param>
/// <param name="Counts">The running tally.</param>
/// <param name="Specimens">The bottles the manifest lists.</param>
public record ManifestDetailResponse(
    Guid Id,
    string Code,
    string OriginClinic,
    string Status,
    DateTime SentAt,
    CountsResponse Counts,
    IReadOnlyList<SpecimenResponse> Specimens);
