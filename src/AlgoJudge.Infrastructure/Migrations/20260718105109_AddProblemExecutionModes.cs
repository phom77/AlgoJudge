using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemExecutionModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExecutionMode",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FunctionAdapterTemplate",
                table: "Problems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionSignatureJson",
                table: "Problems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_ExecutionMode",
                table: "Problems",
                sql: "\"ExecutionMode\" IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_FunctionConfiguration",
                table: "Problems",
                sql: "(\"ExecutionMode\" = 0 AND \"FunctionSignatureJson\" IS NULL AND \"FunctionAdapterTemplate\" IS NULL) OR (\"ExecutionMode\" = 1 AND \"FunctionSignatureJson\" IS NOT NULL AND \"FunctionAdapterTemplate\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_ExecutionMode",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_FunctionConfiguration",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "ExecutionMode",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "FunctionAdapterTemplate",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "FunctionSignatureJson",
                table: "Problems");
        }
    }
}
