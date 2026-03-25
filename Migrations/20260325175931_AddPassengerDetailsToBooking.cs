using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineSeatReservationSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengerDetailsToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PassengerDob",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassengerName",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassengerPassportId",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassengerPhone",
                table: "Bookings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassengerDob",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PassengerName",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PassengerPassportId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PassengerPhone",
                table: "Bookings");
        }
    }
}
