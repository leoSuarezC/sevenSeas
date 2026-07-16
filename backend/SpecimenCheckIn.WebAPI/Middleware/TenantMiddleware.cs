using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;

namespace SpecimenCheckIn.WebAPI.Middleware;

/// <summary>
/// Resolves the lab each request acts as, before any handler can touch tenant data.
/// </summary>
/// <remarks>
/// <para>
/// Authentication is deliberately stubbed — the assignment does not ask for login — so the
/// lab arrives in a header. In production this would come from a verified token claim, and
/// the only line that would change is where <see cref="ResolveLabId"/> reads it from:
/// everything downstream already treats the tenant as server-side state, never as
/// something a caller can pick per query.
/// </para>
/// <para>
/// The header is not trusted on sight: it must name a lab that exists. A request that
/// names no lab, or a lab that does not exist, is refused here rather than being allowed
/// to run untenanted.
/// </para>
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class TenantMiddleware(RequestDelegate next)
{
    /// <summary>The header carrying the current lab.</summary>
    public const string HeaderName = "X-Lab-Id";

    private static readonly string[] UntenantedPaths = ["/swagger", "/health"];

    /// <summary>
    /// Resolves the tenant and passes the request on, or refuses it.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="tenant">The tenant to bind for this request.</param>
    /// <param name="database">Used to confirm the lab exists.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context, TenantContext tenant, SpecimenCheckInContext database)
    {
        if (IsUntenanted(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!TryReadLabId(context, out int labId))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Missing tenant",
                $"Every request must identify its lab through the {HeaderName} header, as a lab id.");
            return;
        }

        // Labs sit outside row-level security precisely so this check can run before a
        // tenant exists.
        bool labExists = await database.Labs.AnyAsync(lab => lab.Id == labId, context.RequestAborted);

        if (!labExists)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Unknown lab",
                $"{HeaderName} does not identify a lab this deployment serves.");
            return;
        }

        tenant.Resolve(labId);

        await next(context);
    }

    private static bool IsUntenanted(PathString path) =>
        UntenantedPaths.Any(untenanted => path.StartsWithSegments(untenanted, StringComparison.OrdinalIgnoreCase));

    private static bool TryReadLabId(HttpContext context, out int labId)
    {
        labId = 0;

        return context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues header)
            && int.TryParse(header.ToString(), out labId);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail) =>
        Results.Problem(statusCode: statusCode, title: title, detail: detail).ExecuteAsync(context);
}
