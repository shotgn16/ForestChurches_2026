using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestChurches.Migrations
{
    /// <inheritdoc />
    public partial class ReversedChurchStatusTosTRING : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChurchInformation_ChurchStatus_ChurchStatusId",
                table: "ChurchInformation");

            migrationBuilder.DropForeignKey(
                name: "FK_ChurchInformation_ChurchStatus_StatusID",
                table: "ChurchInformation");

            migrationBuilder.DropTable(
                name: "ChurchStatus");

            migrationBuilder.DropIndex(
                name: "IX_ChurchInformation_ChurchStatusId",
                table: "ChurchInformation");

            migrationBuilder.DropIndex(
                name: "IX_ChurchInformation_StatusID",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "ChurchStatusId",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "StatusID",
                table: "ChurchInformation");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ChurchInformation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ChurchInformation");

            migrationBuilder.AddColumn<string>(
                name: "ChurchStatusId",
                table: "ChurchInformation",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StatusID",
                table: "ChurchInformation",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ChurchStatus",
                columns: table => new
                {
                    ID = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChurchStatus", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ChurchInformation_ChurchStatusId",
                table: "ChurchInformation",
                column: "ChurchStatusId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChurchInformation_StatusID",
                table: "ChurchInformation",
                column: "StatusID");

            migrationBuilder.AddForeignKey(
                name: "FK_ChurchInformation_ChurchStatus_ChurchStatusId",
                table: "ChurchInformation",
                column: "ChurchStatusId",
                principalTable: "ChurchStatus",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChurchInformation_ChurchStatus_StatusID",
                table: "ChurchInformation",
                column: "StatusID",
                principalTable: "ChurchStatus",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
