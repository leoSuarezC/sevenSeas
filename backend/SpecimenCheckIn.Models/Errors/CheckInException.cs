namespace SpecimenCheckIn.Models.Errors;

/// <summary>
/// Base for the failures a caller can act on, as opposed to bugs.
/// </summary>
/// <remarks>
/// Carries a stable <see cref="Code"/> so a client can branch on the reason without
/// parsing prose. The message is for humans; the code is the contract.
/// </remarks>
/// <param name="code">A stable, machine-readable reason.</param>
/// <param name="message">A human-readable explanation.</param>
public abstract class CheckInException(string code, string message) : Exception(message)
{
    /// <summary>
    /// Gets the stable, machine-readable reason for the failure.
    /// </summary>
    public string Code { get; } = code;
}

/// <summary>
/// Thrown when the requested thing does not exist for the current lab.
/// </summary>
/// <remarks>
/// Deliberately does not distinguish "no such manifest" from "a manifest that belongs to
/// another lab". Answering differently would confirm that a record exists somewhere,
/// which is exactly the leak the isolation exists to prevent.
/// </remarks>
/// <param name="resource">The kind of thing that was not found.</param>
/// <param name="id">The id that was asked for.</param>
public sealed class NotFoundException(string resource, Guid id)
    : CheckInException("not_found", $"No {resource} with id {id} exists in this lab.");

/// <summary>
/// Thrown when an action is understood but not allowed in the current state.
/// </summary>
/// <param name="code">A stable, machine-readable reason.</param>
/// <param name="message">A human-readable explanation.</param>
public sealed class RuleViolationException(string code, string message)
    : CheckInException(code, message);
