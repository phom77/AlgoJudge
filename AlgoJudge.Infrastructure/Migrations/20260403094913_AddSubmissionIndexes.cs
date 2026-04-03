using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Status",
                table: "Submissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId_ProblemId",
                table: "Submissions",
                columns: new[] { "UserId", "ProblemId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_Score",
                table: "Problems",
                sql: "\"Score\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_Status",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_UserId_ProblemId",
                table: "Submissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_Score",
                table: "Problems");
        }
    }
}
