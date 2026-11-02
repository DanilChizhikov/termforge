using Microsoft.EntityFrameworkCore.Migrations;

namespace DTech.Persistence.Migrations;

public partial class AddOperationRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "recovery_data", table: "operations", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "has_pending_cleanup", table: "operations", type: "INTEGER", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "recovery_data", table: "operations");
        migrationBuilder.DropColumn(name: "has_pending_cleanup", table: "operations");
    }
}
