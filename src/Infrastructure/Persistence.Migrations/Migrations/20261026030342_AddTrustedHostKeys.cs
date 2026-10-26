using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustedHostKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trusted_host_keys",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    port = table.Column<int>(type: "INTEGER", nullable: false),
                    algorithm = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    fingerprint = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    trusted_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trusted_host_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trusted_host_keys_host_port_algorithm_fingerprint",
                table: "trusted_host_keys",
                columns: new[] { "host", "port", "algorithm", "fingerprint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trusted_host_keys");
        }
    }
}
