using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SundayLeague.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableStandingsRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DrawPoints",
                table: "Leagues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LossPoints",
                table: "Leagues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Tiebreaker",
                table: "Leagues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WinPoints",
                table: "Leagues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DrawPoints",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "LossPoints",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Tiebreaker",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "WinPoints",
                table: "Leagues");
        }
    }
}
