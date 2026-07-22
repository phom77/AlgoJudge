using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWrongSolutionCandidateProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KilledWrongSolutionsJson",
                table: "AuthoringTestCases",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KilledWrongSolutionsJson",
                table: "AuthoringTestCases");
        }
    }
}
