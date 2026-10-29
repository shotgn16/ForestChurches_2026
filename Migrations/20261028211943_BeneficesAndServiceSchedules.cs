using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestChurches.Migrations
{
    /// <inheritdoc />
    public partial class BeneficesAndServiceSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL/MariaDB won't drop an index a foreign key relies on, so the church -> account key is
            // dropped while the index changes from unique (one church per account) to non-unique, then re-added.
            migrationBuilder.DropForeignKey(
                name: "FK_ChurchInformation_AspNetUsers_ChurchAccountId",
                table: "ChurchInformation");

            migrationBuilder.DropIndex(
                name: "IX_ChurchInformation_ChurchAccountId",
                table: "ChurchInformation");

            migrationBuilder.AddColumn<int>(
                name: "DayOfMonth",
                table: "ServiceTimes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DayOfWeek",
                table: "ServiceTimes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "ServiceTimes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndTime",
                table: "ServiceTimes",
                type: "varchar(5)",
                maxLength: 5,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Interval",
                table: "ServiceTimes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "LocationAddress",
                table: "ServiceTimes",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocationChurchId",
                table: "ServiceTimes",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RepeatType",
                table: "ServiceTimes",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Weekly")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "ServiceTimes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseCustomLocation",
                table: "ServiceTimes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WeeksOfMonth",
                table: "ServiceTimes",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BeneficeId",
                table: "ChurchInformation",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ImageBlobName",
                table: "ChurchInformation",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Benefices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Website = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Benefices", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ServiceDates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ServiceTimesId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDates_ServiceTimes_ServiceTimesId",
                        column: x => x.ServiceTimesId,
                        principalTable: "ServiceTimes",
                        principalColumn: "ServiceID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTimes_LocationChurchId",
                table: "ServiceTimes",
                column: "LocationChurchId");

            migrationBuilder.CreateIndex(
                name: "IX_ChurchInformation_BeneficeId",
                table: "ChurchInformation",
                column: "BeneficeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChurchInformation_ChurchAccountId",
                table: "ChurchInformation",
                column: "ChurchAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChurchInformation_AspNetUsers_ChurchAccountId",
                table: "ChurchInformation",
                column: "ChurchAccountId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_Benefices_Name",
                table: "Benefices",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDates_ServiceTimesId_Date",
                table: "ServiceDates",
                columns: new[] { "ServiceTimesId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChurchInformation_Benefices_BeneficeId",
                table: "ChurchInformation",
                column: "BeneficeId",
                principalTable: "Benefices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTimes_ChurchInformation_LocationChurchId",
                table: "ServiceTimes",
                column: "LocationChurchId",
                principalTable: "ChurchInformation",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChurchInformation_Benefices_BeneficeId",
                table: "ChurchInformation");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTimes_ChurchInformation_LocationChurchId",
                table: "ServiceTimes");

            migrationBuilder.DropTable(
                name: "Benefices");

            migrationBuilder.DropTable(
                name: "ServiceDates");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTimes_LocationChurchId",
                table: "ServiceTimes");

            migrationBuilder.DropIndex(
                name: "IX_ChurchInformation_BeneficeId",
                table: "ChurchInformation");

            // MySQL/MariaDB won't drop an index a foreign key relies on, so the church -> account key is
            // dropped while the index changes back to unique, then re-added.
            migrationBuilder.DropForeignKey(
                name: "FK_ChurchInformation_AspNetUsers_ChurchAccountId",
                table: "ChurchInformation");

            migrationBuilder.DropIndex(
                name: "IX_ChurchInformation_ChurchAccountId",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "DayOfMonth",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "Interval",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "LocationAddress",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "LocationChurchId",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "RepeatType",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "UseCustomLocation",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "WeeksOfMonth",
                table: "ServiceTimes");

            migrationBuilder.DropColumn(
                name: "BeneficeId",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "ImageBlobName",
                table: "ChurchInformation");

            // Note: rolling back fails if any account manages more than one church (the unique index can't be
            // created). Move or remove the extra churches first.
            migrationBuilder.CreateIndex(
                name: "IX_ChurchInformation_ChurchAccountId",
                table: "ChurchInformation",
                column: "ChurchAccountId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChurchInformation_AspNetUsers_ChurchAccountId",
                table: "ChurchInformation",
                column: "ChurchAccountId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
