using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestChurches.Migrations
{
    /// <inheritdoc />
    public partial class ChangedTimeOnlyToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Time",
                table: "ServiceTimes",
                type: "longtext",
                defaultValue: "00:00:00",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(TimeOnly),
                oldType: "time(6)")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeOnly>(
                name: "Time",
                table: "ServiceTimes",
                type: "time(6)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldRowVersion: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
