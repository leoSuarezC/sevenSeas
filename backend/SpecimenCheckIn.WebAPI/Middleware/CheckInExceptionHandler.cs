using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SpecimenCheckIn.Models.Errors;

namespace SpecimenCheckIn.WebAPI.Middleware;

/// <summary>
/// Turns the domain's refusals into structured HTTP responses.
/// </summary>
/// <remarks>
/// <para>
/// Handled here rather than in each action so a rule can be enforced where it belongs —
/// in the domain — without every controller having to remember to translate it.
/// </para>
/// <para>
/// Every response carries the exception's stable <c>code</c>, so a client can react to
/// "still pending" differently from "already closed" without matching on prose that a
/// later edit would break.
/// </para>
/// </remarks>
/// <param name="logger">Records why a request was refused.</param>
public sealed class CheckInExceptionHandler(ILogger<CheckInExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not CheckInException checkInException)
        {
            // Not something the caller can act on. Left to the default handler, which does
            // not put internals in the response body.
            return false;
        }

        (int status, string title) = checkInException switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (StatusCodes.Status409Conflict, "Action not allowed"),
        };

        // Logged with the code only: the message can name a manifest, and request logs are
        // the last place patient-adjacent detail should accumulate.
        logger.LogInformation(
            "Refused {Method} {Path}: {Code}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            checkInException.Code);

        ProblemDetails problem = new()
        {
            Status = status,
            Title = title,
            Detail = checkInException.Message,
            Instance = httpContext.Request.Path,
            Extensions = { ["code"] = checkInException.Code },
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
