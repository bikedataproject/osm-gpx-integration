using Microsoft.EntityFrameworkCore.Migrations;

namespace BikeDataProject.Integrations.OSM.Db.Migrations
{
    public partial class UserIsString : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsmUserId",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "OsmUser",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsmUser",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "OsmUserId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
