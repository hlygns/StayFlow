using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationGuestCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestCount",
                table: "Reservations");
        }
    }
}
