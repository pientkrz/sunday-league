using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SundayLeague.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeasonRolloverSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leagues_SeasonId",
                table: "Leagues");

            migrationBuilder.DropIndex(
                name: "IX_Leagues_Slug",
                table: "Leagues");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_SeasonId_Slug",
                table: "Leagues",
                columns: new[] { "SeasonId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leagues_SeasonId_Slug",
                table: "Leagues");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_SeasonId",
                table: "Leagues",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_Slug",
                table: "Leagues",
                column: "Slug",
                unique: true);
        }
    }
}
