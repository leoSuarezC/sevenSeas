using Microsoft.AspNetCore.Mvc;
using SpecimenCheckIn.Models.Api;
using SpecimenCheckIn.Queries.Session;

namespace SpecimenCheckIn.WebAPI.Controllers;

/// <summary>
/// Reports who the request is acting as.
/// </summary>
/// <param name="queries">The read side.</param>
[ApiController]
[Route("session")]
[Produces("application/json")]
public class SessionQueryController(SessionQueries queries) : ControllerBase
{
    /// <summary>
    /// Gets the lab and technician for the current session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current session.</returns>
    [HttpGet]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    public async Task<SessionResponse> GetAsync(CancellationToken cancellationToken) =>
        await queries.GetAsync(cancellationToken);
}
