using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    server = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    finished_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    details = table.Column<string>(type: "TEXT", nullable: false),
                    error = table.Column<string>(type: "TEXT", nullable: true),
                    retryable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operations_created_at",
                table: "operations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_operations_kind",
                table: "operations",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "ix_operations_server",
                table: "operations",
                column: "server");

            migrationBuilder.CreateIndex(
                name: "ix_operations_status",
                table: "operations",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operations");
        }
    }
}
