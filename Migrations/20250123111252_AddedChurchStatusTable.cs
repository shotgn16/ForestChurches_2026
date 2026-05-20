using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestChurches.Migrations
{
    /// <inheritdoc />
    public partial class AddedChurchStatusTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ChurchInformation");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ChurchInformation",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ChurchInformation",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ChurchInformation",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ChurchInformation",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ChurchInformation",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
