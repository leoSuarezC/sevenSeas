using Microsoft.AspNetCore.Mvc;
using SpecimenCheckIn.Models.Api;
using SpecimenCheckIn.Queries.Manifests;

namespace SpecimenCheckIn.WebAPI.Controllers;

/// <summary>
/// Reads manifests for the lab the request is acting as.
/// </summary>
/// <param name="queries">The read side.</param>
[ApiController]
[Route("manifests")]
[Produces("application/json")]
public class ManifestsQueryController(ManifestQueries queries) : ControllerBase
{
    /// <summary>
    /// Lists this lab's manifests, newest shipment first.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The worklist.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ManifestSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ManifestSummaryResponse>> ListAsync(CancellationToken cancellationToken) =>
        await queries.ListAsync(cancellationToken);

    /// <summary>
    /// Gets one manifest and the bottles it lists.
    /// </summary>
    /// <param name="manifestId">The manifest to load.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The manifest detail.</returns>
    /// <response code="404">No such manifest in this lab.</response>
    [HttpGet("{manifestId:guid}")]
    [ProducesResponseType<ManifestDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ManifestDetailResponse> GetAsync(Guid manifestId, CancellationToken cancellationToken) =>
        await queries.GetAsync(manifestId, cancellationToken);
}
