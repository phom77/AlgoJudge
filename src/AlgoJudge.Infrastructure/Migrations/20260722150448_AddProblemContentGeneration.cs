using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemContentGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProblemAuthoringRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StatementMarkdown = table.Column<string>(type: "text", nullable: false),
                    ConstraintsMarkdown = table.Column<string>(type: "text", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    TimeLimitMs = table.Column<int>(type: "integer", nullable: false),
                    MemoryLimitKb = table.Column<int>(type: "integer", nullable: false),
                    SamplesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    DefinitionSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CandidateSuiteSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    CandidateToolchain = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CandidateStatisticsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CandidateCaseCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemAuthoringRevisions", x => x.Id);
                    table.CheckConstraint("CK_AuthoringRevision_Candidate", "(\"Status\" IN (0, 1) AND \"CandidateSuiteSha256\" IS NULL AND \"CandidateCaseCount\" IS NULL) OR (\"Status\" IN (2, 3) AND \"CandidateSuiteSha256\" IS NOT NULL AND \"CandidateCaseCount\" > 0)");
                    table.CheckConstraint("CK_AuthoringRevision_MemoryLimit", "\"MemoryLimitKb\" > 0");
                    table.CheckConstraint("CK_AuthoringRevision_Number", "\"RevisionNumber\" > 0");
                    table.CheckConstraint("CK_AuthoringRevision_Status", "\"Status\" IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_AuthoringRevision_TimeLimit", "\"TimeLimitMs\" > 0");
                    table.ForeignKey(
                        name: "FK_ProblemAuthoringRevisions_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProblemAuthoringRevisions_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthoringTestCases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthoringTestCases", x => x.Id);
                    table.CheckConstraint("CK_AuthoringTestCase_Group", "\"Group\" IN ('handwritten', 'edge', 'random', 'adversarial', 'stress')");
                    table.CheckConstraint("CK_AuthoringTestCase_Ordinal", "\"Ordinal\" > 0");
                    table.ForeignKey(
                        name: "FK_AuthoringTestCases_ProblemAuthoringRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "ProblemAuthoringRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentGenerationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DefinitionSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    DefinitionSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    TimeLimitMs = table.Column<int>(type: "integer", nullable: false),
                    MemoryLimitKb = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentGenerationJobs", x => x.Id);
                    table.CheckConstraint("CK_ContentGenerationJob_Attempts", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_ContentGenerationJob_Claim", "(\"Status\" = 1 AND \"WorkerId\" IS NOT NULL AND \"ClaimToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL) OR (\"Status\" <> 1 AND \"WorkerId\" IS NULL AND \"ClaimToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL)");
                    table.CheckConstraint("CK_ContentGenerationJob_Status", "\"Status\" IN (0, 1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ContentGenerationJobs_ProblemAuthoringRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "ProblemAuthoringRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthoringTestCases_RevisionId_Name",
                table: "AuthoringTestCases",
                columns: new[] { "RevisionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthoringTestCases_RevisionId_Ordinal",
                table: "AuthoringTestCases",
                columns: new[] { "RevisionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_LeaseExpiresAt",
                table: "ContentGenerationJobs",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_RevisionId",
                table: "ContentGenerationJobs",
                column: "RevisionId",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_Status_CreatedAt",
                table: "ContentGenerationJobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemAuthoringRevisions_OwnerUserId_Status_UpdatedAt",
                table: "ProblemAuthoringRevisions",
                columns: new[] { "OwnerUserId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemAuthoringRevisions_ProblemId_RevisionNumber",
                table: "ProblemAuthoringRevisions",
                columns: new[] { "ProblemId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthoringTestCases");

            migrationBuilder.DropTable(
                name: "ContentGenerationJobs");

            migrationBuilder.DropTable(
                name: "ProblemAuthoringRevisions");
        }
    }
}
