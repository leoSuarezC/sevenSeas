namespace SpecimenCheckIn.Context.Tenancy;

/// <summary>
/// The lab the current request acts as.
/// </summary>
/// <remarks>
/// Resolved once per request from the incoming credentials — never from a value the
/// caller can choose per query. Everything below this point (query filters, session
/// context, row-level security) derives the tenant from here.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// Gets a value indicating whether a lab has been resolved for this request.
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// Gets the current lab id.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no lab has been resolved. Tenant-scoped work without a tenant is a
    /// bug, so it fails loudly rather than quietly reading someone's data.
    /// </exception>
    int LabId { get; }
}
