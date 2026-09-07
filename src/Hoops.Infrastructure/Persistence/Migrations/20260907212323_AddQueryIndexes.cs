using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoops.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_player_game_statlines_organisation_id",
                table: "player_game_statlines",
                column: "organisation_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_events_game_id_game_roster_entry_id",
                table: "game_events",
                columns: new[] { "game_id", "game_roster_entry_id" },
                filter: "shot_x_cm IS NOT NULL AND is_voided = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_player_game_statlines_organisation_id",
                table: "player_game_statlines");

            migrationBuilder.DropIndex(
                name: "ix_game_events_game_id_game_roster_entry_id",
                table: "game_events");
        }
    }
}
