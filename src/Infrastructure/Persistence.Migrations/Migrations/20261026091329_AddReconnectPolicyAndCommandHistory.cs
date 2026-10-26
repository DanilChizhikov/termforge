using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconnectPolicyAndCommandHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "reconnect_initial_delay_seconds",
                table: "saved_connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "reconnect_max_attempts",
                table: "saved_connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "reconnect_max_delay_seconds",
                table: "saved_connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "reconnect_mode",
                table: "saved_connections",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.CreateTable(
                name: "command_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    connection_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    command = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    executed_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    exit_code = table.Column<int>(type: "INTEGER", nullable: true),
                    is_pinned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_command_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_command_history_saved_connection_entity_connection_id",
                        column: x => x.connection_id,
                        principalTable: "saved_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_command_history_connection_id_executed_at",
                table: "command_history",
                columns: new[] { "connection_id", "executed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "command_history");

            migrationBuilder.DropColumn(
                name: "reconnect_initial_delay_seconds",
                table: "saved_connections");

            migrationBuilder.DropColumn(
                name: "reconnect_max_attempts",
                table: "saved_connections");

            migrationBuilder.DropColumn(
                name: "reconnect_max_delay_seconds",
                table: "saved_connections");

            migrationBuilder.DropColumn(
                name: "reconnect_mode",
                table: "saved_connections");
        }
    }
}
