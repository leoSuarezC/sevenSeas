using Microsoft.AspNetCore.Mvc;
using SpecimenCheckIn.Commands.Manifests;
using SpecimenCheckIn.Models.Api;
using SpecimenCheckIn.Queries.Manifests;

namespace SpecimenCheckIn.WebAPI.Controllers;

/// <summary>
/// The actions a technician takes at the receiving desk.
/// </summary>
/// <remarks>
/// Each action answers with the manifest as it now stands, so the screen's counts and
/// "ready to close" state come from the server rather than being re-derived in the client
/// and drifting from it.
/// </remarks>
/// <param name="commands">The write side.</param>
/// <param name="queries">The read side, used to answer with the new state.</param>
[ApiController]
[Route("manifests")]
[Produces("application/json")]
public class ManifestsCommandController(CheckInCommands commands, ManifestQueries queries) : ControllerBase
{
    /// <summary>
    /// Marks a bottle as physically received. Receiving the same bottle twice is safe.
    /// </summary>
    /// <param name="manifestId">The manifest the bottle is listed on.</param>
    /// <param name="specimenId">The bottle.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The manifest as it now stands.</returns>
    /// <response code="404">No such manifest or bottle in this lab.</response>
    /// <response code="409">The manifest is closed.</response>
    [HttpPost("{manifestId:guid}/specimens/{specimenId:guid}/receive")]
    [ProducesResponseType<ManifestDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ManifestDetailResponse> ReceiveAsync(
        Guid manifestId,
        Guid specimenId,
        CancellationToken cancellationToken)
    {
        await commands.ReceiveSpecimenAsync(manifestId, specimenId, cancellationToken);
        return await queries.GetAsync(manifestId, cancellationToken);
    }

    /// <summary>
    /// Reports a listed bottle as missing, raising a discrepancy.
    /// </summary>
    /// <param name="manifestId">The manifest the bottle is listed on.</param>
    /// <param name="specimenId">The bottle.</param>
    /// <param name="request">Optional context from the technician.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The manifest as it now stands.</returns>
    /// <response code="404">No such manifest or bottle in this lab.</response>
    /// <response code="409">The manifest is closed.</response>
    [HttpPost("{manifestId:guid}/specimens/{specimenId:guid}/flag")]
    [ProducesResponseType<ManifestDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ManifestDetailResponse> FlagAsync(
        Guid manifestId,
        Guid specimenId,
        [FromBody] FlagSpecimenRequest? request,
        CancellationToken cancellationToken)
    {
        await commands.FlagSpecimenAsync(manifestId, specimenId, request?.Notes, cancellationToken);
        return await queries.GetAsync(manifestId, cancellationToken);
    }

    /// <summary>
    /// Closes a manifest, once every bottle has been received or flagged missing.
    /// </summary>
    /// <param name="manifestId">The manifest to close.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The manifest as it now stands.</returns>
    /// <response code="404">No such manifest in this lab.</response>
    /// <response code="409">Bottles are still pending, it is already closed, or someone else changed it.</response>
    [HttpPost("{manifestId:guid}/close")]
    [ProducesResponseType<ManifestDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ManifestDetailResponse> CloseAsync(Guid manifestId, CancellationToken cancellationToken)
    {
        await commands.CloseManifestAsync(manifestId, cancellationToken);
        return await queries.GetAsync(manifestId, cancellationToken);
    }
}

/// <summary>
/// Optional context recorded with a flagged bottle.
/// </summary>
/// <param name="Notes">What the technician wants on the record.</param>
public record FlagSpecimenRequest(string? Notes);
