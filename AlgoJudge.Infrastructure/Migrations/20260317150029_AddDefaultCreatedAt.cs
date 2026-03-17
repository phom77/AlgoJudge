using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlgoJudge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Problems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_CreatedAt",
                table: "Submissions",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Problems_CreatedBy",
                table: "Problems",
                column: "CreatedBy");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_MemoryLimit",
                table: "Problems",
                sql: "\"MemoryLimit\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Problem_TimeLimit",
                table: "Problems",
                sql: "\"TimeLimit\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_Users_CreatedBy",
                table: "Problems",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Problems_Users_CreatedBy",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_CreatedAt",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Problems_CreatedBy",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_MemoryLimit",
                table: "Problems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Problem_TimeLimit",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Problems");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");
        }
    }
}
