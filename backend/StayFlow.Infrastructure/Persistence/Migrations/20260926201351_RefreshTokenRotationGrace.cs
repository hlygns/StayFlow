using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenRotationGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousRefreshToken",
                table: "Users",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenRotatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PreviousRefreshToken",
                table: "Users",
                column: "PreviousRefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RefreshToken",
                table: "Users",
                column: "RefreshToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PreviousRefreshToken",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreviousRefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenRotatedAt",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
