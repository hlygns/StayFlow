using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuestPushTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestPushTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestPushTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestPushTokens_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestPushTokens_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestPushTokens_HotelId",
                table: "GuestPushTokens",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestPushTokens_ReservationId_Token",
                table: "GuestPushTokens",
                columns: new[] { "ReservationId", "Token" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestPushTokens");
        }
    }
}
