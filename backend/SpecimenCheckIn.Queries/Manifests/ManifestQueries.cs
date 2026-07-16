using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Models;
using SpecimenCheckIn.Models.Api;
using SpecimenCheckIn.Models.Errors;

namespace SpecimenCheckIn.Queries.Manifests;

/// <summary>
/// The read side of check-in.
/// </summary>
/// <remarks>
/// Every query here is scoped to the current lab without saying so: the global query
/// filter adds the lab, and row-level security enforces it regardless. There is
/// deliberately no "for lab X" parameter anywhere — a caller cannot ask for another
/// tenant's data, because there is no way to express the question.
/// </remarks>
/// <param name="database">The database.</param>
public class ManifestQueries(SpecimenCheckInContext database)
{
    /// <summary>
    /// Lists the manifests for the current lab, newest shipment first.
    /// </summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The worklist.</returns>
    public async Task<IReadOnlyList<ManifestSummaryResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<Manifest> manifests = await database.Manifests
            .AsNoTracking()
            .Include(manifest => manifest.Specimens)
            .Include(manifest => manifest.Discrepancies)
            .OrderByDescending(manifest => manifest.SentAt)
            .ToListAsync(cancellationToken);

        return manifests.Select(Summarise).ToList();
    }

    /// <summary>
    /// Gets one manifest and the bottles it lists.
    /// </summary>
    /// <param name="manifestId">The manifest to load.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The manifest detail.</returns>
    /// <exception cref="NotFoundException">
    /// Thrown when no such manifest exists in this lab — including when it exists in
    /// another one.
    /// </exception>
    public async Task<ManifestDetailResponse> GetAsync(Guid manifestId, CancellationToken cancellationToken = default)
    {
        Manifest manifest = await database.Manifests
            .AsNoTracking()
            .Include(manifest => manifest.Specimens)
            .FirstOrDefaultAsync(manifest => manifest.Id == manifestId, cancellationToken)
            ?? throw new NotFoundException("manifest", manifestId);

        IReadOnlyList<SpecimenResponse> specimens = manifest.Specimens
            .OrderBy(specimen => specimen.Code)
            .Select(specimen => new SpecimenResponse(
                specimen.Id,
                specimen.Code,
                specimen.Patient,
                specimen.Site,
                specimen.Provider,
                specimen.Status.ToString(),
                specimen.ReceivedBy,
                specimen.ReceivedAt))
            .ToList();

        return new ManifestDetailResponse(
            manifest.Id,
            manifest.Code,
            manifest.OriginClinic,
            manifest.Status.ToString(),
            manifest.SentAt,
            CountsResponse.From(manifest.Count()),
            specimens);
    }

    private static ManifestSummaryResponse Summarise(Manifest manifest) => new(
        manifest.Id,
        manifest.Code,
        manifest.OriginClinic,
        manifest.Status.ToString(),
        manifest.SentAt,
        CountsResponse.From(manifest.Count()),
        manifest.Discrepancies.Count(discrepancy => discrepancy.Status == DiscrepancyStatus.Open));
}
