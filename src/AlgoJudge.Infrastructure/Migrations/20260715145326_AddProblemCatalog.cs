using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_MemoryLimit",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_TimeLimit",
                table: "Problems");

            migrationBuilder.RenameColumn(
                name: "TimeLimit",
                table: "Problems",
                newName: "TimeLimitMs");

            migrationBuilder.RenameColumn(
                name: "MemoryLimit",
                table: "Problems",
                newName: "MemoryLimitKb");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Problems",
                newName: "StatementMarkdown");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Problems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "ConstraintsMarkdown",
                table: "Problems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JudgeVersion",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Problems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Problems",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Problems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.Sql(
                """
                UPDATE "Problems"
                SET
                    "Slug" = 'problem-' || "Id",
                    "ConstraintsMarkdown" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Problems",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConstraintsMarkdown",
                table: "Problems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "JudgeTestCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JudgeTestCases", x => x.Id);
                    table.CheckConstraint("CK_JudgeTestCase_Ordinal", "\"Ordinal\" > 0");
                    table.ForeignKey(
                        name: "FK_JudgeTestCases_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProblemSamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemSamples", x => x.Id);
                    table.CheckConstraint("CK_ProblemSample_Ordinal", "\"Ordinal\" > 0");
                    table.ForeignKey(
                        name: "FK_ProblemSamples_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "ProblemSamples"
                    ("ProblemId", "Input", "ExpectedOutput", "Explanation", "Ordinal")
                SELECT
                    "ProblemId",
                    "Input",
                    "ExpectedOutput",
                    NULL,
                    ROW_NUMBER() OVER (PARTITION BY "ProblemId" ORDER BY "Id")::integer
                FROM "TestCases"
                WHERE NOT "IsHidden";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "JudgeTestCases"
                    ("ProblemId", "Input", "ExpectedOutput", "Ordinal")
                SELECT
                    "ProblemId",
                    "Input",
                    "ExpectedOutput",
                    ROW_NUMBER() OVER (PARTITION BY "ProblemId" ORDER BY "Id")::integer
                FROM "TestCases"
                WHERE "IsHidden";
                """);

            migrationBuilder.DropTable(
                name: "TestCases");

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProblemTags",
                columns: table => new
                {
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemTags", x => new { x.ProblemId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ProblemTags_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProblemTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId_Status_ProblemId",
                table: "Submissions",
                columns: new[] { "UserId", "Status", "ProblemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Problems_Slug",
                table: "Problems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Problems_Status_CreatedAt",
                table: "Problems",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_JudgeVersion",
                table: "Problems",
                sql: "\"JudgeVersion\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_MemoryLimitKb",
                table: "Problems",
                sql: "\"MemoryLimitKb\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_TimeLimitMs",
                table: "Problems",
                sql: "\"TimeLimitMs\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_JudgeTestCases_ProblemId_Ordinal",
                table: "JudgeTestCases",
                columns: new[] { "ProblemId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemSamples_ProblemId_Ordinal",
                table: "ProblemSamples",
                columns: new[] { "ProblemId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemTags_TagId",
                table: "ProblemTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Slug",
                table: "Tags",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "text", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCases_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "TestCases" ("ProblemId", "Input", "ExpectedOutput", "IsHidden")
                SELECT "ProblemId", "Input", "ExpectedOutput", FALSE
                FROM "ProblemSamples"
                ORDER BY "ProblemId", "Ordinal";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "TestCases" ("ProblemId", "Input", "ExpectedOutput", "IsHidden")
                SELECT "ProblemId", "Input", "ExpectedOutput", TRUE
                FROM "JudgeTestCases"
                ORDER BY "ProblemId", "Ordinal";
                """);

            migrationBuilder.DropTable(
                name: "JudgeTestCases");

            migrationBuilder.DropTable(
                name: "ProblemSamples");

            migrationBuilder.DropTable(
                name: "ProblemTags");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_UserId_Status_ProblemId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Problems_Slug",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_Problems_Status_CreatedAt",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_JudgeVersion",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_MemoryLimitKb",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_TimeLimitMs",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ConstraintsMarkdown",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "JudgeVersion",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Problems");

            migrationBuilder.RenameColumn(
                name: "TimeLimitMs",
                table: "Problems",
                newName: "TimeLimit");

            migrationBuilder.RenameColumn(
                name: "StatementMarkdown",
                table: "Problems",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "MemoryLimitKb",
                table: "Problems",
                newName: "MemoryLimit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Problems",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_MemoryLimit",
                table: "Problems",
                sql: "\"MemoryLimit\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_TimeLimit",
                table: "Problems",
                sql: "\"TimeLimit\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_ProblemId",
                table: "TestCases",
                column: "ProblemId");
        }
    }
}
