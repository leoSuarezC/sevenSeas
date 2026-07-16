using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Models.Api;

namespace SpecimenCheckIn.Queries.Session;

/// <summary>
/// Who the current request is acting as.
/// </summary>
/// <remarks>
/// The screen shows the lab it is working in, and that label has to come from the server.
/// If the client rendered whichever lab it believed it had asked to be, the header could
/// disagree with the data underneath it — which is exactly the confusion a technician
/// handling someone's biopsy should never have to resolve.
/// </remarks>
/// <param name="database">The database.</param>
/// <param name="tenant">The lab the request acts as.</param>
/// <param name="user">The technician at the desk.</param>
public class SessionQueries(SpecimenCheckInContext database, ITenantContext tenant, IUserContext user)
{
    /// <summary>
    /// Describes the current session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The lab and technician this request is acting as.</returns>
    public async Task<SessionResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        string labName = await database.Labs
            .Where(lab => lab.Id == tenant.LabId)
            .Select(lab => lab.Name)
            .SingleAsync(cancellationToken);

        return new SessionResponse(tenant.LabId, labName, user.LabTech);
    }
}
