using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionQueueLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_Status",
                table: "Submissions");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimToken",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkerId",
                table: "Submissions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Status value 2 represented the old non-leased Compiling state.
            // Requeue any in-flight rows before enforcing Running ownership.
            migrationBuilder.Sql(
                "UPDATE \"Submissions\" SET \"Status\" = 1 WHERE \"Status\" = 2;");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Status_CreatedAt_Id",
                table: "Submissions",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Status_LeaseExpiresAt",
                table: "Submissions",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submission_AttemptCount",
                table: "Submissions",
                sql: "\"AttemptCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submission_RunningClaim",
                table: "Submissions",
                sql: "\"Status\" <> 2 OR (\"WorkerId\" IS NOT NULL AND \"ClaimToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_Status_CreatedAt_Id",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_Status_LeaseExpiresAt",
                table: "Submissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Submission_AttemptCount",
                table: "Submissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Submission_RunningClaim",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ClaimToken",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "WorkerId",
                table: "Submissions");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Status",
                table: "Submissions",
                column: "Status");
        }
    }
}
