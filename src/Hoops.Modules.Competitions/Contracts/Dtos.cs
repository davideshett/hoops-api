using Hoops.SharedKernel;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Competitions.Contracts;

// ── Seasons ──────────────────────────────────────────────────────────────────

/// <summary>A season.</summary>
public sealed record SeasonDto(
    SeasonId Id, string Name, DateOnly StartsOn, DateOnly EndsOn, bool IsActive, DateTimeOffset CreatedAt);

/// <summary>Create a season.</summary>
public sealed record CreateSeasonRequest(string Name, DateOnly StartsOn, DateOnly EndsOn);

/// <summary>Update a season; omitted fields are unchanged.</summary>
public sealed record UpdateSeasonRequest(string? Name, DateOnly? StartsOn, DateOnly? EndsOn, bool? IsActive);

// ── Competitions ─────────────────────────────────────────────────────────────

/// <summary>A competition, including its typed rule set.</summary>
public sealed record CompetitionDto(
    CompetitionId Id,
    SeasonId SeasonId,
    string Name,
    string Slug,
    string Format,
    string? Category,
    RuleSet RuleSet,
    string Status,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string Timezone,
    DateTimeOffset CreatedAt);

/// <summary>Create a competition. A null <paramref name="RuleSet"/> defaults to FIBA rules.</summary>
public sealed record CreateCompetitionRequest(
    SeasonId SeasonId,
    string Name,
    string Slug,
    string Format,
    string? Category,
    RuleSet? RuleSet,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string Timezone);

/// <summary>Update a competition's profile; omitted fields are unchanged.</summary>
public sealed record UpdateCompetitionRequest(
    string? Name,
    string? Category,
    string? Format,
    RuleSet? RuleSet,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string? Timezone);

/// <summary>Move a competition to a new lifecycle status.</summary>
public sealed record ChangeCompetitionStatusRequest(string Status);

// ── Stages &amp; groups ────────────────────────────────────────────────────────

/// <summary>A stage within a competition.</summary>
public sealed record StageDto(StageId Id, CompetitionId CompetitionId, string Name, string StageType, int Sequence);

/// <summary>Create a stage.</summary>
public sealed record CreateStageRequest(string Name, string StageType, int Sequence);

/// <summary>A group (pool) within a stage.</summary>
public sealed record GroupDto(GroupId Id, StageId StageId, string Name);

/// <summary>Create a group.</summary>
public sealed record CreateGroupRequest(string Name);

// ── Teams ────────────────────────────────────────────────────────────────────

/// <summary>A canonical team (club).</summary>
public sealed record TeamDto(
    TeamId Id,
    string Name,
    string ShortName,
    string? Abbreviation,
    string? LogoUrl,
    string? PrimaryColour,
    string? SecondaryColour,
    VenueId? HomeVenueId,
    DateTimeOffset CreatedAt);

/// <summary>Create a canonical team.</summary>
public sealed record CreateTeamRequest(
    string Name,
    string ShortName,
    string? Abbreviation,
    string? PrimaryColour,
    string? SecondaryColour,
    VenueId? HomeVenueId);

/// <summary>Update a team; omitted fields are unchanged.</summary>
public sealed record UpdateTeamRequest(
    string? Name,
    string? ShortName,
    string? Abbreviation,
    string? PrimaryColour,
    string? SecondaryColour,
    VenueId? HomeVenueId);

// ── Competition entries &amp; staff ──────────────────────────────────────────────

/// <summary>A team's entry into a competition.</summary>
public sealed record CompetitionTeamDto(
    CompetitionTeamId Id,
    CompetitionId CompetitionId,
    TeamId TeamId,
    GroupId? GroupId,
    string? DisplayName,
    int? Seed,
    string Status);

/// <summary>Enter an existing canonical team into a competition.</summary>
public sealed record EnterTeamRequest(TeamId TeamId);

/// <summary>Update a competition entry; omitted fields are unchanged.</summary>
public sealed record UpdateCompetitionTeamRequest(GroupId? GroupId, int? Seed, string? DisplayName, string? Status);

/// <summary>A staff member on a competition entry.</summary>
public sealed record TeamStaffDto(TeamStaffId Id, CompetitionTeamId CompetitionTeamId, string FullName, string Role);

/// <summary>Add a staff member.</summary>
public sealed record AddStaffRequest(string FullName, string Role);

// ── Venues ───────────────────────────────────────────────────────────────────

/// <summary>A venue.</summary>
public sealed record VenueDto(
    VenueId Id, string Name, string? Address, string? City, int CourtCount, string Timezone, DateTimeOffset CreatedAt);

/// <summary>Create a venue.</summary>
public sealed record CreateVenueRequest(string Name, string? Address, string? City, int? CourtCount, string Timezone);

/// <summary>Update a venue; omitted fields are unchanged.</summary>
public sealed record UpdateVenueRequest(string? Name, string? Address, string? City, int? CourtCount, string? Timezone);
