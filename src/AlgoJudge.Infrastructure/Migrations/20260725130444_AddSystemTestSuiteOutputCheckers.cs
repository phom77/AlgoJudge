using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemTestSuiteOutputCheckers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemTestSuites",
                columns: table => new
                {
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    OutputCheckerKind = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AbsoluteTolerance = table.Column<double>(type: "double precision", nullable: true),
                    RelativeTolerance = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemTestSuites", x => new { x.ProblemId, x.Version });
                    table.CheckConstraint("CK_SystemTestSuite_OutputChecker", "\"OutputCheckerKind\" IN (0, 1, 2)");
                    table.CheckConstraint("CK_SystemTestSuite_OutputCheckerTolerance", "(\"OutputCheckerKind\" IN (0, 1) AND \"AbsoluteTolerance\" IS NULL AND \"RelativeTolerance\" IS NULL) OR (\"OutputCheckerKind\" = 2 AND \"AbsoluteTolerance\" IS NOT NULL AND \"RelativeTolerance\" IS NOT NULL AND \"AbsoluteTolerance\" NOT IN ('Infinity'::double precision, '-Infinity'::double precision, 'NaN'::double precision) AND \"RelativeTolerance\" NOT IN ('Infinity'::double precision, '-Infinity'::double precision, 'NaN'::double precision) AND \"AbsoluteTolerance\" >= 0 AND \"RelativeTolerance\" >= 0 AND (\"AbsoluteTolerance\" > 0 OR \"RelativeTolerance\" > 0))");
                    table.CheckConstraint("CK_SystemTestSuite_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_SystemTestSuites_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "SystemTestSuites" ("ProblemId", "Version", "OutputCheckerKind", "CreatedAt")
                SELECT "ProblemId", "SystemTestSuiteVersion", 0, CURRENT_TIMESTAMP
                FROM "JudgeTestCases"
                GROUP BY "ProblemId", "SystemTestSuiteVersion";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_JudgeTestCases_SystemTestSuites_ProblemId_SystemTestSuiteVe~",
                table: "JudgeTestCases",
                columns: new[] { "ProblemId", "SystemTestSuiteVersion" },
                principalTable: "SystemTestSuites",
                principalColumns: new[] { "ProblemId", "Version" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JudgeTestCases_SystemTestSuites_ProblemId_SystemTestSuiteVe~",
                table: "JudgeTestCases");

            migrationBuilder.DropTable(
                name: "SystemTestSuites");
        }
    }
}
