using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicService.Migrations
{
    /// <inheritdoc />
    public partial class AddNeighborhoodField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Neighborhood",
                table: "ServiceRequests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Neighborhood",
                table: "ServiceRequests");
        }
    }
}
