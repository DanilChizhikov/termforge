using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_editor_drafts",
                columns: table => new
                {
                    connection_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    base_modified_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    base_size = table.Column<long>(type: "INTEGER", nullable: true),
                    base_sha256 = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_editor_drafts", x => new { x.connection_id, x.path });
                    table.ForeignKey(
                        name: "fk_workspace_editor_drafts_saved_connections_connection_id",
                        column: x => x.connection_id,
                        principalTable: "saved_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_states",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                    document = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_states", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_editor_drafts");

            migrationBuilder.DropTable(
                name: "workspace_states");
        }
    }
}
