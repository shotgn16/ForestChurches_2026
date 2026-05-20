using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestChurches.Migrations
{
    /// <inheritdoc />
    public partial class RenamedSystem_Logsto_Logs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_system_logs",
                table: "system_logs");

            migrationBuilder.RenameTable(
                name: "system_logs",
                newName: "logs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_logs",
                table: "logs",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_logs",
                table: "logs");

            migrationBuilder.RenameTable(
                name: "logs",
                newName: "system_logs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_system_logs",
                table: "system_logs",
                column: "Id");
        }
    }
}
