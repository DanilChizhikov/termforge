using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServerAiAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ai_access",
                table: "saved_connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_access",
                table: "saved_connections");
        }
    }
}
