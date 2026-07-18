using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VersionSystemTestSuites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JudgeTestCases_ProblemId_Ordinal",
                table: "JudgeTestCases");

            migrationBuilder.AddColumn<int>(
                name: "SystemTestSuiteVersion",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SystemTestSuiteVersion",
                table: "JudgeTestCases",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE "JudgeTestCases" AS test_case
                SET "SystemTestSuiteVersion" = problem."JudgeVersion"
                FROM "Problems" AS problem
                WHERE test_case."ProblemId" = problem."Id";

                UPDATE "Submissions" AS submission
                SET "SystemTestSuiteVersion" = problem."JudgeVersion"
                FROM "Problems" AS problem
                WHERE submission."ProblemId" = problem."Id";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Submission_SystemTestSuiteVersion",
                table: "Submissions",
                sql: "\"SystemTestSuiteVersion\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeTestCases_ProblemId_SystemTestSuiteVersion_Ordinal",
                table: "JudgeTestCases",
                columns: new[] { "ProblemId", "SystemTestSuiteVersion", "Ordinal" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_JudgeTestCase_SystemTestSuiteVersion",
                table: "JudgeTestCases",
                sql: "\"SystemTestSuiteVersion\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Submission_SystemTestSuiteVersion",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_JudgeTestCases_ProblemId_SystemTestSuiteVersion_Ordinal",
                table: "JudgeTestCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JudgeTestCase_SystemTestSuiteVersion",
                table: "JudgeTestCases");

            migrationBuilder.DropColumn(
                name: "SystemTestSuiteVersion",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SystemTestSuiteVersion",
                table: "JudgeTestCases");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeTestCases_ProblemId_Ordinal",
                table: "JudgeTestCases",
                columns: new[] { "ProblemId", "Ordinal" },
                unique: true);
        }
    }
}
