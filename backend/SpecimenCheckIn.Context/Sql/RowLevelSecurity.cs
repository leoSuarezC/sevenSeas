using SpecimenCheckIn.Models;

namespace SpecimenCheckIn.Context.Sql;

/// <summary>
/// The single source of truth for the row-level security objects, shared by the
/// DbContext and the migration that creates them so the two cannot drift apart.
/// </summary>
/// <remarks>
/// <para>
/// The scheme: every tenant-owned table carries a <c>LabId</c> column that defaults to
/// <see cref="CurrentLabIdFunction"/>, which reads the lab out of the connection's
/// <c>SESSION_CONTEXT</c>. A security policy then applies two predicates to those tables:
/// </para>
/// <list type="bullet">
/// <item><description><b>Filter</b> — silently hides rows belonging to other labs from
/// every read, update and delete.</description></item>
/// <item><description><b>Block</b> — hard-fails any write that would place a row in, or
/// move a row to, another lab.</description></item>
/// </list>
/// <para>
/// This is the backstop, not the only guard: the DbContext also applies a global query
/// filter. The filter catches mistakes early and keeps queries honest; the policy means
/// that even a hand-written query, a missed filter, or a future bug still cannot cross
/// the tenant boundary, because the database itself refuses.
/// </para>
/// </remarks>
public static class RowLevelSecurity
{
    /// <summary>The schema owning the tables and the security objects.</summary>
    public const string Schema = "SpecimenCheckIn";

    /// <summary>Scalar function returning the lab for the current session.</summary>
    public const string CurrentLabIdFunction = "fn_CurrentLabId";

    /// <summary>Inline table-valued function used as the security predicate.</summary>
    public const string AccessPredicateFunction = "fn_LabAccessPredicate";

    /// <summary>The security policy binding the predicate to each protected table.</summary>
    public const string SecurityPolicy = "LabAccessPolicy";

    /// <summary>
    /// The <c>SESSION_CONTEXT</c> key holding the current lab. Set once per connection
    /// by the tenant interceptor; never trusted from client input.
    /// </summary>
    public const string SessionContextKey = "LabId";

    /// <summary>
    /// The tables the policy protects — every tenant-owned table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Lab"/> is absent on purpose: resolving the tenant means reading it,
    /// which has to happen before a tenant context exists.
    /// </para>
    /// <para>
    /// This list is the one thing a new tenant-owned table could be left out of, so it
    /// is not left to reviewer diligence: a test asserts it matches every
    /// <see cref="TenantOwnedEntity"/> mapped by the model, and fails if a table is
    /// added without protecting it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> ProtectedTables { get; } =
    [
        "Manifests",
        "Specimens",
        "Discrepancies",
        "AuditEvents",
    ];

    /// <summary>
    /// Gets the SQL a tenant-owned <c>LabId</c> column defaults to.
    /// </summary>
    public static string CurrentLabIdDefaultSql => $"[{Schema}].[{CurrentLabIdFunction}]()";
}
