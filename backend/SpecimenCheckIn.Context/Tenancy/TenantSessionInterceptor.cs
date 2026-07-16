using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpecimenCheckIn.Context.Sql;

namespace SpecimenCheckIn.Context.Tenancy;

/// <summary>
/// Publishes the current lab into the connection's <c>SESSION_CONTEXT</c>, which is
/// where row-level security reads it from.
/// </summary>
/// <remarks>
/// <para>
/// This runs on every connection open, not once per context: connections are pooled, and
/// returning one to the pool resets its session context. Setting it on open is what keeps
/// a recycled connection from carrying one request's lab into another's.
/// </para>
/// <para>
/// When no lab has been resolved, nothing is set — and that is the safe outcome. Without a
/// lab in session context the security predicate matches no rows and the LabId default
/// resolves to NULL, so untenanted code reads nothing and writes nothing.
/// </para>
/// </remarks>
/// <param name="tenant">The lab the current request acts as.</param>
public sealed class TenantSessionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    /// <inheritdoc/>
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (tenant.IsResolved)
        {
            using DbCommand command = CreateSetSessionContextCommand(connection, tenant.LabId);
            command.ExecuteNonQuery();
        }

        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (tenant.IsResolved)
        {
            await using DbCommand command = CreateSetSessionContextCommand(connection, tenant.LabId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static DbCommand CreateSetSessionContextCommand(DbConnection connection, int labId)
    {
        DbCommand command = connection.CreateCommand();

        // Parameterised, and @read_only locks the value for the life of the connection:
        // once this request's lab is set, nothing further down can re-point the session
        // at a different lab.
        command.CommandText = "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1";

        DbParameter key = command.CreateParameter();
        key.ParameterName = "@key";
        key.Value = RowLevelSecurity.SessionContextKey;
        command.Parameters.Add(key);

        DbParameter value = command.CreateParameter();
        value.ParameterName = "@value";
        value.Value = labId;
        command.Parameters.Add(value);

        return command;
    }
}
