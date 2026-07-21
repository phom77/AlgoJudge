using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowGenericFunctionHarness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_FunctionConfiguration",
                table: "Problems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_FunctionConfiguration",
                table: "Problems",
                sql: "(\"ExecutionMode\" = 0 AND \"FunctionSignatureJson\" IS NULL AND \"FunctionAdapterTemplate\" IS NULL) OR (\"ExecutionMode\" = 1 AND \"FunctionSignatureJson\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_FunctionConfiguration",
                table: "Problems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_FunctionConfiguration",
                table: "Problems",
                sql: "(\"ExecutionMode\" = 0 AND \"FunctionSignatureJson\" IS NULL AND \"FunctionAdapterTemplate\" IS NULL) OR (\"ExecutionMode\" = 1 AND \"FunctionSignatureJson\" IS NOT NULL AND \"FunctionAdapterTemplate\" IS NOT NULL)");
        }
    }
}
