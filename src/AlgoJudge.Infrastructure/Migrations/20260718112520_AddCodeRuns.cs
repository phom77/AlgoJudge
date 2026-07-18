using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodeRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    SourceCode = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StandardOutput = table.Column<string>(type: "text", nullable: true),
                    ErrorOutput = table.Column<string>(type: "text", nullable: true),
                    ExecutionTimeMs = table.Column<int>(type: "integer", nullable: false),
                    MemoryUsedKb = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeRuns", x => x.Id);
                    table.CheckConstraint("CK_CodeRun_AttemptCount", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_CodeRun_RunningClaim", "\"Status\" <> 2 OR (\"WorkerId\" IS NOT NULL AND \"ClaimToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CodeRuns_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodeRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_CreatedAt",
                table: "CodeRuns",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_ProblemId",
                table: "CodeRuns",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_Status_CreatedAt_Id",
                table: "CodeRuns",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_Status_LeaseExpiresAt",
                table: "CodeRuns",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_UserId",
                table: "CodeRuns",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeRuns");
        }
    }
}
