using Microsoft.EntityFrameworkCore.Migrations;
using SpecimenCheckIn.Context.Sql;

#nullable disable

namespace SpecimenCheckIn.Context.Migrations
{
    /// <summary>
    /// Turns on SQL Server row-level security for every tenant-owned table.
    /// </summary>
    /// <remarks>
    /// The application already filters by lab through a global query filter. This is the
    /// backstop underneath it: a query that forgets the filter, a hand-written statement,
    /// or a future bug still cannot cross the tenant boundary, because the database
    /// applies the predicate itself.
    /// </remarks>
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Inline table-valued, so SQL Server folds it into the query plan and the
            // predicate costs a comparison rather than a function call per row.
            // SESSION_CONTEXT is read directly rather than via fn_CurrentLabId, which
            // as a scalar function would defeat that inlining.
            migrationBuilder.Sql($"""
                CREATE FUNCTION [{RowLevelSecurity.Schema}].[{RowLevelSecurity.AccessPredicateFunction}](@LabId int)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN
                    SELECT 1 AS accessResult
                    WHERE @LabId = TRY_CAST(SESSION_CONTEXT(N'{RowLevelSecurity.SessionContextKey}') AS int)
                """);

            string predicates = string.Join(
                "," + Environment.NewLine,
                RowLevelSecurity.ProtectedTables.Select(BuildPredicatesFor));

            migrationBuilder.Sql($"""
                CREATE SECURITY POLICY [{RowLevelSecurity.Schema}].[{RowLevelSecurity.SecurityPolicy}]
                {predicates}
                WITH (STATE = ON, SCHEMABINDING = ON)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP SECURITY POLICY [{RowLevelSecurity.Schema}].[{RowLevelSecurity.SecurityPolicy}]");
            migrationBuilder.Sql($"DROP FUNCTION [{RowLevelSecurity.Schema}].[{RowLevelSecurity.AccessPredicateFunction}]");
        }

        /// <summary>
        /// Builds the filter and block predicates that bind one table to the policy.
        /// </summary>
        private static string BuildPredicatesFor(string table)
        {
            string predicate = $"[{RowLevelSecurity.Schema}].[{RowLevelSecurity.AccessPredicateFunction}](LabId)";
            string target = $"[{RowLevelSecurity.Schema}].[{table}]";

            return string.Join(
                "," + Environment.NewLine,

                // Hides other labs' rows from every read, update and delete.
                $"    ADD FILTER PREDICATE {predicate} ON {target}",

                // Rejects a write that would place a row in another lab, rather than
                // silently discarding it — the attempt surfaces as an error.
                $"    ADD BLOCK PREDICATE {predicate} ON {target} AFTER INSERT",
                $"    ADD BLOCK PREDICATE {predicate} ON {target} AFTER UPDATE");
        }
    }
}
