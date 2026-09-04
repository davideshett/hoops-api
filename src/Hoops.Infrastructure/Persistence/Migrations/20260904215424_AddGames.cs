using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoops.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    home_competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    away_competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rule_set_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    roster_locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                    table.ForeignKey(
                        name: "fk_games_competition_teams_away_competition_team_id",
                        column: x => x.away_competition_team_id,
                        principalTable: "competition_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_games_competition_teams_home_competition_team_id",
                        column: x => x.home_competition_team_id,
                        principalTable: "competition_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_games_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_games_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "game_officials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_officials", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_officials_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_roster_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    jersey_number = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<string>(type: "text", nullable: true),
                    is_starter = table.Column<bool>(type: "boolean", nullable: false),
                    is_captain = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_roster_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_roster_entries_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_roster_entries_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_officials_game_id",
                table: "game_officials",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_roster_entries_game_id",
                table: "game_roster_entries",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_roster_entries_game_id_competition_team_id_jersey_numb",
                table: "game_roster_entries",
                columns: new[] { "game_id", "competition_team_id", "jersey_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_roster_entries_player_id",
                table: "game_roster_entries",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_games_away_competition_team_id",
                table: "games",
                column: "away_competition_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_games_competition_id_scheduled_at",
                table: "games",
                columns: new[] { "competition_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_games_home_competition_team_id",
                table: "games",
                column: "home_competition_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_games_stage_id",
                table: "games",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_games_venue_id",
                table: "games",
                column: "venue_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_officials");

            migrationBuilder.DropTable(
                name: "game_roster_entries");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
