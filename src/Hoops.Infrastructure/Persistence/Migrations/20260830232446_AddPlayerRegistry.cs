using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hoops.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "merge_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    keep_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merge_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_by_organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merge_proposals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: true),
                    known_as = table.Column<string>(type: "text", nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: false),
                    nationality = table.Column<string>(type: "char(2)", nullable: false),
                    state_of_origin = table.Column<string>(type: "text", nullable: true),
                    height_cm = table.Column<int>(type: "integer", nullable: true),
                    dominant_hand = table.Column<string>(type: "text", nullable: true),
                    photo_object_key = table.Column<string>(type: "text", nullable: true),
                    nin_hmac = table.Column<byte[]>(type: "bytea", nullable: true),
                    nin_verified = table.Column<bool>(type: "boolean", nullable: false),
                    nin_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nin_verification_ref = table.Column<string>(type: "text", nullable: true),
                    nin_verification_provider = table.Column<string>(type: "text", nullable: true),
                    identity_tier = table.Column<string>(type: "text", nullable: false),
                    dob_evidence_type = table.Column<string>(type: "text", nullable: false),
                    dob_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    guardian_name = table.Column<string>(type: "text", nullable: true),
                    guardian_phone = table.Column<string>(type: "text", nullable: true),
                    guardian_consent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_by_organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    merged_into_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anonymised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                    table.ForeignKey(
                        name: "fk_players_players_merged_into_id",
                        column: x => x.merged_into_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registry_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    query_terms = table.Column<string>(type: "jsonb", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result_count = table.Column<int>(type: "integer", nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registry_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registry_ledger",
                columns: table => new
                {
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_type = table.Column<string>(type: "text", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    previous_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    entry_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registry_ledger", x => x.sequence);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_type = table.Column<string>(type: "text", nullable: false),
                    scope_version = table.Column<string>(type: "text", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_object_key = table.Column<string>(type: "text", nullable: true),
                    captured_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_consent_records_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_eligibility_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flag_type = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    raised_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_eligibility_flags", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_eligibility_flags_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_org_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_org_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_player_org_links_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roster_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_tier = table.Column<int>(type: "integer", nullable: false),
                    jersey_number = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<string>(type: "text", nullable: true),
                    is_captain = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roster_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_roster_entries_competition_teams_competition_team_id",
                        column: x => x.competition_team_id,
                        principalTable: "competition_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_roster_entries_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_player_id",
                table: "consent_records",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_merge_proposals_status",
                table: "merge_proposals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_player_eligibility_flags_player_id",
                table: "player_eligibility_flags",
                column: "player_id",
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_player_org_links_player_id_organisation_id",
                table: "player_org_links",
                columns: new[] { "player_id", "organisation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_players_last_name_date_of_birth",
                table: "players",
                columns: new[] { "last_name", "date_of_birth" });

            migrationBuilder.CreateIndex(
                name: "ix_players_merged_into_id",
                table: "players",
                column: "merged_into_id");

            migrationBuilder.CreateIndex(
                name: "ix_players_nin_hmac",
                table: "players",
                column: "nin_hmac",
                unique: true,
                filter: "nin_hmac IS NOT NULL AND merged_into_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_players_registered_by_organisation_id",
                table: "players",
                column: "registered_by_organisation_id");

            migrationBuilder.CreateIndex(
                name: "ix_registry_audit_occurred_at",
                table: "registry_audit",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_registry_audit_player_id",
                table: "registry_audit",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_registry_ledger_entry_hash",
                table: "registry_ledger",
                column: "entry_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roster_entries_competition_team_id_jersey_number",
                table: "roster_entries",
                columns: new[] { "competition_team_id", "jersey_number" },
                unique: true,
                filter: "status <> 'Removed'");

            migrationBuilder.CreateIndex(
                name: "ix_roster_entries_competition_team_id_player_id",
                table: "roster_entries",
                columns: new[] { "competition_team_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roster_entries_player_id",
                table: "roster_entries",
                column: "player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "merge_proposals");

            migrationBuilder.DropTable(
                name: "player_eligibility_flags");

            migrationBuilder.DropTable(
                name: "player_org_links");

            migrationBuilder.DropTable(
                name: "registry_audit");

            migrationBuilder.DropTable(
                name: "registry_ledger");

            migrationBuilder.DropTable(
                name: "roster_entries");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
