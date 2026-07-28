using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentBatchOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "ProblemAuthoringRevisions",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "ProblemAuthoringRevisions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchItemId",
                table: "ContentGenerationJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBatches", x => x.Id);
                    table.CheckConstraint("CK_ContentBatch_Status", "\"Status\" IN (0, 1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_ContentBatches_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    CatalogPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StatementMarkdown = table.Column<string>(type: "text", nullable: false),
                    ConstraintsMarkdown = table.Column<string>(type: "text", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    TimeLimitMs = table.Column<int>(type: "integer", nullable: false),
                    MemoryLimitKb = table.Column<int>(type: "integer", nullable: false),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SamplesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    GeneratorParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProblemId = table.Column<int>(type: "integer", nullable: true),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SafeFailureCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SafeFailureMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBatchItems", x => x.Id);
                    table.CheckConstraint("CK_ContentBatchItem_Action", "\"Action\" IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_ContentBatchItem_Ordinal", "\"Ordinal\" > 0");
                    table.CheckConstraint("CK_ContentBatchItem_Resolution", "(\"Status\" IN (1, 2, 3, 5) AND \"ProblemId\" IS NOT NULL AND \"RevisionId\" IS NOT NULL) OR \"Status\" IN (0, 4, 6)");
                    table.CheckConstraint("CK_ContentBatchItem_Status", "\"Status\" IN (0, 1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_ContentBatchItems_ContentBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ContentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentBatchItems_ProblemAuthoringRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "ProblemAuthoringRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentBatchItems_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentBatchAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<int>(type: "integer", nullable: true),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SafeFailureCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBatchAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentBatchAuditEntries_ContentBatchItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ContentBatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentBatchAuditEntries_ContentBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ContentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentBatchAuditEntries_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_BatchItemId",
                table: "ContentGenerationJobs",
                column: "BatchItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchAuditEntries_AdminUserId_CreatedAt",
                table: "ContentBatchAuditEntries",
                columns: new[] { "AdminUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchAuditEntries_BatchId_CreatedAt",
                table: "ContentBatchAuditEntries",
                columns: new[] { "BatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchAuditEntries_ItemId",
                table: "ContentBatchAuditEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatches_CreatedByUserId_CreatedAt",
                table: "ContentBatches",
                columns: new[] { "CreatedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatches_Status_UpdatedAt",
                table: "ContentBatches",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchItems_BatchId_Ordinal",
                table: "ContentBatchItems",
                columns: new[] { "BatchId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchItems_BatchId_Status",
                table: "ContentBatchItems",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchItems_ProblemId",
                table: "ContentBatchItems",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchItems_RevisionId",
                table: "ContentBatchItems",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBatchItems_Slug_ContentHash",
                table: "ContentBatchItems",
                columns: new[] { "Slug", "ContentHash" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContentGenerationJobs_ContentBatchItems_BatchItemId",
                table: "ContentGenerationJobs",
                column: "BatchItemId",
                principalTable: "ContentBatchItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentGenerationJobs_ContentBatchItems_BatchItemId",
                table: "ContentGenerationJobs");

            migrationBuilder.DropTable(
                name: "ContentBatchAuditEntries");

            migrationBuilder.DropTable(
                name: "ContentBatchItems");

            migrationBuilder.DropTable(
                name: "ContentBatches");

            migrationBuilder.DropIndex(
                name: "IX_ContentGenerationJobs_BatchItemId",
                table: "ContentGenerationJobs");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "ProblemAuthoringRevisions");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "ProblemAuthoringRevisions");

            migrationBuilder.DropColumn(
                name: "BatchItemId",
                table: "ContentGenerationJobs");
        }
    }
}
