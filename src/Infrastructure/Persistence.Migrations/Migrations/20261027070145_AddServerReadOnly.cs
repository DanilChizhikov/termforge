using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DTech.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServerReadOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "read_only",
                table: "saved_connections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "read_only",
                table: "saved_connections");
        }
    }
}
