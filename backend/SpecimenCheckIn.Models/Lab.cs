namespace SpecimenCheckIn.Models;

/// <summary>
/// A pathology lab receiving specimens. This is the tenant — the isolation boundary
/// every other entity is scoped to.
/// </summary>
/// <remarks>
/// Labs are the one table deliberately left outside row-level security: the tenant
/// middleware has to resolve and validate the incoming lab before a tenant context
/// exists, so it cannot itself be tenant-scoped.
/// </remarks>
public class Lab
{
    /// <summary>
    /// Gets or sets the lab id. Also the tenant id used by row-level security.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the lab's display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
