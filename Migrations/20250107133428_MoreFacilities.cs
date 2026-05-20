using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestChurches.Migrations
{
    /// <inheritdoc />
    public partial class MoreFacilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Childcare",
                table: "ChurchInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HearingAidSupport",
                table: "ChurchInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Kitchen",
                table: "ChurchInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MeetingHall",
                table: "ChurchInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Offices",
                table: "ChurchInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OutdoorSpaces",
                table: "ChurchInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WorshipHall",
                table: "ChurchInformation",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Childcare",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "HearingAidSupport",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "Kitchen",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "MeetingHall",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "Offices",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "OutdoorSpaces",
                table: "ChurchInformation");

            migrationBuilder.DropColumn(
                name: "WorshipHall",
                table: "ChurchInformation");
        }
    }
}
