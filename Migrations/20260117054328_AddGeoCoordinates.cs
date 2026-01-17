using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicService.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ServiceRequests",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ServiceRequests",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ServiceRequests");
        }
    }
}
