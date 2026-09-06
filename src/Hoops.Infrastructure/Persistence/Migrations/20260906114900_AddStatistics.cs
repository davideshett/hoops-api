using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoops.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "amendment_reason",
                table: "games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "finalized_at",
                table: "games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "forfeit_winner_competition_team_id",
                table: "games",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "competition_player_aggregates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    games_played = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    field_goals_made = table.Column<int>(type: "integer", nullable: false),
                    field_goals_attempted = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_made = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_attempted = table.Column<int>(type: "integer", nullable: false),
                    free_throws_made = table.Column<int>(type: "integer", nullable: false),
                    free_throws_attempted = table.Column<int>(type: "integer", nullable: false),
                    offensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    defensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    assists = table.Column<int>(type: "integer", nullable: false),
                    steals = table.Column<int>(type: "integer", nullable: false),
                    blocks = table.Column<int>(type: "integer", nullable: false),
                    turnovers = table.Column<int>(type: "integer", nullable: false),
                    fouls_committed = table.Column<int>(type: "integer", nullable: false),
                    seconds_played = table.Column<int>(type: "integer", nullable: false),
                    plus_minus = table.Column<int>(type: "integer", nullable: false),
                    is_qualified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_player_aggregates", x => x.id);
                    table.ForeignKey(
                        name: "fk_competition_player_aggregates_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_competition_player_aggregates_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competition_standings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    played = table.Column<int>(type: "integer", nullable: false),
                    won = table.Column<int>(type: "integer", nullable: false),
                    lost = table.Column<int>(type: "integer", nullable: false),
                    drawn = table.Column<int>(type: "integer", nullable: false),
                    points_for = table.Column<int>(type: "integer", nullable: false),
                    points_against = table.Column<int>(type: "integer", nullable: false),
                    league_points = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_standings", x => x.id);
                    table.ForeignKey(
                        name: "fk_competition_standings_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_period_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    team_fouls = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_period_states", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_period_states_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lineup_stints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<int>(type: "integer", nullable: false),
                    player_ids = table.Column<string>(type: "text", nullable: false),
                    seconds_played = table.Column<int>(type: "integer", nullable: false),
                    points_for = table.Column<int>(type: "integer", nullable: false),
                    points_against = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lineup_stints", x => x.id);
                    table.ForeignKey(
                        name: "fk_lineup_stints_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "player_career_aggregates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    games_played = table.Column<int>(type: "integer", nullable: false),
                    competitions_played = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    field_goals_made = table.Column<int>(type: "integer", nullable: false),
                    field_goals_attempted = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_made = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_attempted = table.Column<int>(type: "integer", nullable: false),
                    free_throws_made = table.Column<int>(type: "integer", nullable: false),
                    free_throws_attempted = table.Column<int>(type: "integer", nullable: false),
                    offensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    defensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    assists = table.Column<int>(type: "integer", nullable: false),
                    steals = table.Column<int>(type: "integer", nullable: false),
                    blocks = table.Column<int>(type: "integer", nullable: false),
                    turnovers = table.Column<int>(type: "integer", nullable: false),
                    fouls_committed = table.Column<int>(type: "integer", nullable: false),
                    seconds_played = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_career_aggregates", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_career_aggregates_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_game_statlines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_roster_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    field_goals_made = table.Column<int>(type: "integer", nullable: false),
                    field_goals_attempted = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_made = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_attempted = table.Column<int>(type: "integer", nullable: false),
                    free_throws_made = table.Column<int>(type: "integer", nullable: false),
                    free_throws_attempted = table.Column<int>(type: "integer", nullable: false),
                    offensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    defensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    assists = table.Column<int>(type: "integer", nullable: false),
                    steals = table.Column<int>(type: "integer", nullable: false),
                    blocks = table.Column<int>(type: "integer", nullable: false),
                    blocks_against = table.Column<int>(type: "integer", nullable: false),
                    turnovers = table.Column<int>(type: "integer", nullable: false),
                    fouls_committed = table.Column<int>(type: "integer", nullable: false),
                    fouls_drawn = table.Column<int>(type: "integer", nullable: false),
                    fouled_out = table.Column<bool>(type: "boolean", nullable: false),
                    seconds_played = table.Column<int>(type: "integer", nullable: false),
                    plus_minus = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_game_statlines", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_game_statlines_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_game_statlines_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "team_game_statlines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opponent_competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_home = table.Column<bool>(type: "boolean", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    points_against = table.Column<int>(type: "integer", nullable: false),
                    field_goals_made = table.Column<int>(type: "integer", nullable: false),
                    field_goals_attempted = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_made = table.Column<int>(type: "integer", nullable: false),
                    three_pointers_attempted = table.Column<int>(type: "integer", nullable: false),
                    free_throws_made = table.Column<int>(type: "integer", nullable: false),
                    free_throws_attempted = table.Column<int>(type: "integer", nullable: false),
                    offensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    defensive_rebounds = table.Column<int>(type: "integer", nullable: false),
                    assists = table.Column<int>(type: "integer", nullable: false),
                    steals = table.Column<int>(type: "integer", nullable: false),
                    blocks = table.Column<int>(type: "integer", nullable: false),
                    turnovers = table.Column<int>(type: "integer", nullable: false),
                    fouls_committed = table.Column<int>(type: "integer", nullable: false),
                    won = table.Column<bool>(type: "boolean", nullable: false),
                    drawn = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_game_statlines", x => x.id);
                    table.ForeignKey(
                        name: "fk_team_game_statlines_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_competition_player_aggregates_competition_id_player_id",
                table: "competition_player_aggregates",
                columns: new[] { "competition_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competition_player_aggregates_competition_id_points",
                table: "competition_player_aggregates",
                columns: new[] { "competition_id", "points" },
                filter: "is_qualified");

            migrationBuilder.CreateIndex(
                name: "ix_competition_player_aggregates_player_id",
                table: "competition_player_aggregates",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_competition_standings_competition_id_competition_team_id",
                table: "competition_standings",
                columns: new[] { "competition_id", "competition_team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competition_standings_competition_id_position",
                table: "competition_standings",
                columns: new[] { "competition_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_game_period_states_game_id_competition_team_id_period",
                table: "game_period_states",
                columns: new[] { "game_id", "competition_team_id", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lineup_stints_competition_team_id_player_ids",
                table: "lineup_stints",
                columns: new[] { "competition_team_id", "player_ids" });

            migrationBuilder.CreateIndex(
                name: "ix_lineup_stints_game_id",
                table: "lineup_stints",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_player_career_aggregates_player_id",
                table: "player_career_aggregates",
                column: "player_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_game_statlines_competition_id_player_id",
                table: "player_game_statlines",
                columns: new[] { "competition_id", "player_id" });

            migrationBuilder.CreateIndex(
                name: "ix_player_game_statlines_game_id_game_roster_entry_id",
                table: "player_game_statlines",
                columns: new[] { "game_id", "game_roster_entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_game_statlines_player_id",
                table: "player_game_statlines",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_game_statlines_competition_id",
                table: "team_game_statlines",
                column: "competition_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_game_statlines_game_id_competition_team_id",
                table: "team_game_statlines",
                columns: new[] { "game_id", "competition_team_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competition_player_aggregates");

            migrationBuilder.DropTable(
                name: "competition_standings");

            migrationBuilder.DropTable(
                name: "game_period_states");

            migrationBuilder.DropTable(
                name: "lineup_stints");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "player_career_aggregates");

            migrationBuilder.DropTable(
                name: "player_game_statlines");

            migrationBuilder.DropTable(
                name: "team_game_statlines");

            migrationBuilder.DropColumn(
                name: "amendment_reason",
                table: "games");

            migrationBuilder.DropColumn(
                name: "finalized_at",
                table: "games");

            migrationBuilder.DropColumn(
                name: "forfeit_winner_competition_team_id",
                table: "games");
        }
    }
}
