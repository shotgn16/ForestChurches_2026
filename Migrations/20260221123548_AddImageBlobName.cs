using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForestChurches.Migrations
{
    /// <inheritdoc />
    public partial class AddImageBlobName_V2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL to add columns only if they do not already exist. This makes the migration
            // safe to run against MySQL 8.x where the column may already be present or when adding
            // a NOT NULL column to a table with existing rows.
            migrationBuilder.Sql("ALTER TABLE `Events` ADD COLUMN IF NOT EXISTS `ImageBlobName` longtext CHARACTER SET utf8mb4 NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `AspNetUsers` ADD COLUMN IF NOT EXISTS `ImageBlobName` longtext CHARACTER SET utf8mb4 NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Use DROP COLUMN IF EXISTS to avoid errors when rolling back if the column isn't present.
            migrationBuilder.Sql("ALTER TABLE `Events` DROP COLUMN IF EXISTS `ImageBlobName`;");
            migrationBuilder.Sql("ALTER TABLE `AspNetUsers` DROP COLUMN IF EXISTS `ImageBlobName`;");
        }
    }
}
