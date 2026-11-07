using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    public partial class AddNamedAiBridgeTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_bridge_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "TEXT", nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_bridge_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_bridge_tokens_normalized_name",
                table: "ai_bridge_tokens",
                column: "normalized_name",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO ai_bridge_tokens (id, name, normalized_name, token_hash, created_at)
                SELECT '35C13333-93D6-4D3A-BF84-DCCCE2B3BD0E', 'Default', 'DEFAULT',
                       json_extract(value, '$.TokenHash'), updated_at
                FROM app_settings
                WHERE key = 'ai_bridge.settings'
                  AND CASE WHEN json_valid(value)
                      THEN json_type(value, '$.TokenHash') = 'text'
                           AND length(json_extract(value, '$.TokenHash')) > 0
                      ELSE 0 END;

                UPDATE app_settings
                SET value = json_remove(value, '$.TokenHash')
                WHERE key = 'ai_bridge.settings' AND json_valid(value);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_bridge_tokens");
        }
    }
}
