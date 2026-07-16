namespace SpecimenCheckIn.Context.Tenancy;

/// <summary>
/// The per-request tenant, resolved once by the tenant middleware.
/// </summary>
/// <remarks>
/// Write-once on purpose: once a request is acting as a lab, nothing downstream can
/// switch it. That keeps "which tenant am I?" from being a question with more than one
/// answer during a single request.
/// </remarks>
public sealed class TenantContext : ITenantContext
{
    private int? labId;

    /// <inheritdoc/>
    public bool IsResolved => this.labId.HasValue;

    /// <inheritdoc/>
    public int LabId => this.labId
        ?? throw new InvalidOperationException(
            "No lab has been resolved for this request. Tenant-scoped data cannot be read or written without a tenant.");

    /// <summary>
    /// Binds this request to a lab.
    /// </summary>
    /// <param name="labId">The lab the request acts as.</param>
    /// <exception cref="InvalidOperationException">Thrown if the request is already bound to a lab.</exception>
    public void Resolve(int labId)
    {
        if (this.labId.HasValue)
        {
            throw new InvalidOperationException(
                $"This request is already acting as lab {this.labId}; the tenant cannot be reassigned.");
        }

        this.labId = labId;
    }
}
