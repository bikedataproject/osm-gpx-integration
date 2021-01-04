using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BikeDataProject.Integrations.OSM.Db.Migrations
{
    public partial class GpxFileStorage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "GpxFile",
                table: "Tracks",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "GpxFileName",
                table: "Tracks",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GpxFile",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "GpxFileName",
                table: "Tracks");
        }
    }
}
