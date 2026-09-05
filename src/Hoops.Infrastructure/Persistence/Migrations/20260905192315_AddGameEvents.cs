using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hoops.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    event_subtype = table.Column<string>(type: "text", nullable: true),
                    period = table.Column<int>(type: "integer", nullable: false),
                    game_clock_ms = table.Column<int>(type: "integer", nullable: false),
                    shot_clock_ms = table.Column<int>(type: "integer", nullable: true),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    game_roster_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    secondary_roster_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    points = table.Column<int>(type: "integer", nullable: true),
                    shot_x_cm = table.Column<int>(type: "integer", nullable: true),
                    shot_y_cm = table.Column<int>(type: "integer", nullable: true),
                    shot_zone = table.Column<string>(type: "text", nullable: true),
                    shot_distance_cm = table.Column<int>(type: "integer", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    is_voided = table.Column<bool>(type: "boolean", nullable: false),
                    client_recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_events_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_events_game_id_event_type",
                table: "game_events",
                columns: new[] { "game_id", "event_type" });

            migrationBuilder.CreateIndex(
                name: "ix_game_events_game_id_sequence",
                table: "game_events",
                columns: new[] { "game_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_events_secondary_roster_entry_id",
                table: "game_events",
                column: "secondary_roster_entry_id",
                filter: "secondary_roster_entry_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_events");
        }
    }
}
