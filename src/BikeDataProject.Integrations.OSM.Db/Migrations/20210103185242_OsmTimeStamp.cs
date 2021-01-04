using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BikeDataProject.Integrations.OSM.Db.Migrations
{
    public partial class OsmTimeStamp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OsmTimeStamp",
                table: "Tracks",
                type: "timestamp without time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsmTimeStamp",
                table: "Tracks");
        }
    }
}
