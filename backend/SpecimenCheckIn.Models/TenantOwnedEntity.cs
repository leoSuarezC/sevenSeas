namespace SpecimenCheckIn.Models;

/// <summary>
/// Base for every row that belongs to a lab. Carrying <see cref="LabId"/> on each
/// tenant-owned table (rather than reaching it through a join) is what lets row-level
/// security police each table directly.
/// </summary>
public abstract class TenantOwnedEntity
{
    /// <summary>
    /// Gets the clustering key.
    /// </summary>
    /// <remarks>
    /// The primary key is a Guid, which would scatter inserts across the table if it
    /// also drove physical order. This identity column carries the clustered index
    /// instead, so rows always append to the end.
    /// </remarks>
    public int ClusterId { get; private set; }

    /// <summary>
    /// Gets the owning lab (the tenant).
    /// </summary>
    /// <remarks>
    /// Deliberately not settable from application code: the column defaults to the lab
    /// in the current session context, so the database stamps it on insert. A caller
    /// cannot write a row into another tenant even by mistake, and row-level security's
    /// block predicate rejects the attempt outright.
    /// </remarks>
    public int LabId { get; private set; }
}
