namespace SpecimenCheckIn.Context.Tenancy;

/// <summary>
/// The lab tech working the receiving desk on this request.
/// </summary>
/// <remarks>
/// Stubbed, as the assignment allows — but kept as a service rather than a string passed
/// around, because the audit log has to be able to answer "who did this" and that answer
/// should come from the request context, not from whatever a caller chose to send in a body.
/// </remarks>
public interface IUserContext
{
    /// <summary>
    /// Gets the technician's display name.
    /// </summary>
    string LabTech { get; }
}

/// <summary>
/// The per-request technician, resolved by the tenant middleware.
/// </summary>
public sealed class UserContext : IUserContext
{
    /// <summary>The technician assumed when the request does not name one.</summary>
    public const string DefaultLabTech = "Lab Tech 1";

    /// <inheritdoc/>
    public string LabTech { get; private set; } = DefaultLabTech;

    /// <summary>
    /// Binds this request to a technician.
    /// </summary>
    /// <param name="labTech">The technician's display name.</param>
    public void Resolve(string labTech)
    {
        if (!string.IsNullOrWhiteSpace(labTech))
        {
            this.LabTech = labTech.Trim();
        }
    }
}
