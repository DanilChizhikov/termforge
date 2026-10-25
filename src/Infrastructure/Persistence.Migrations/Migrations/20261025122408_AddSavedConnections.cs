using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    public partial class AddSavedConnections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    group_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    port = table.Column<int>(type: "INTEGER", nullable: false),
                    user_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    auth_method = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    private_key_path = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    jump_host_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    ssh_alias = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    has_password = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_connected_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_connections", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_connections_saved_connections_jump_host_id",
                        column: x => x.jump_host_id,
                        principalTable: "saved_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_saved_connections_jump_host_id",
                table: "saved_connections",
                column: "jump_host_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_connections_name",
                table: "saved_connections",
                column: "name",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_connections");
        }
    }
}
