using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.key);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");
        }
    }
}
