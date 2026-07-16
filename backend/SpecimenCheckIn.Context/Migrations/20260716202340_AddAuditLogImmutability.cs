using Microsoft.EntityFrameworkCore.Migrations;
using SpecimenCheckIn.Context.Sql;

#nullable disable

namespace SpecimenCheckIn.Context.Migrations
{
    /// <summary>
    /// Makes the audit log append-only in the database, not just in the application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The DbContext already refuses to modify or delete an audit event, which catches the
    /// mistake early and with a clear message. This is the same argument as row-level
    /// security one layer down: a log that can be rewritten by anything holding a
    /// connection — a script, a migration, a future service — is not evidence of anything.
    /// The claim that the log is immutable should be enforced by the thing that stores it.
    /// </para>
    /// <para>
    /// An AFTER trigger that rolls back, rather than INSTEAD OF: SQL Server does not allow
    /// INSTEAD OF triggers on a table carrying a block predicate, and the audit table has
    /// one from the tenant policy. Rolling back refuses the write just as firmly.
    /// </para>
    /// </remarks>
    public partial class AddAuditLogImmutability : Migration
    {
        private const string Trigger = "trg_AuditEvents_AppendOnly";
        private const string Table = "AuditEvents";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql($"""
                CREATE TRIGGER [{RowLevelSecurity.Schema}].[{Trigger}]
                ON [{RowLevelSecurity.Schema}].[{Table}]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    ROLLBACK TRANSACTION;
                    THROW 50000, 'The audit log is append-only: audit events cannot be modified or deleted.', 1;
                END
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql($"DROP TRIGGER [{RowLevelSecurity.Schema}].[{Trigger}]");
    }
}
