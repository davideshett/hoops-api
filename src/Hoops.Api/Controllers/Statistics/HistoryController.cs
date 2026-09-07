using Hoops.Api.Auth;
using Hoops.Modules.Statistics.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Statistics;

/// <summary>
/// The historical query surface — leaderboards, careers, shot charts, play-by-play, lineups, and
/// all-time records. This is the data the product is sold on (§11).
/// </summary>
[Route("api/v1/organisations/{orgId:guid}")]
public sealed class HistoryController : ApiControllerBase
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;

    private readonly IHistoryQueryService _history;

    /// <summary>Creates the controller.</summary>
    public HistoryController(IHistoryQueryService history) => _history = history;

    /// <summary>
    /// One leaderboard. <c>per=total</c> ranks everyone; <c>per=game</c> ranks per-game averages and
    /// includes only players who have met the qualification threshold.
    /// </summary>
    /// <response code="200">The ranked leaderboard.</response>
    /// <response code="400">Unknown stat, or <c>per</c> was neither total nor game.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("competitions/{competitionId:guid}/leaders")]
    [ProducesResponseType(typeof(LeaderboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeaderboardDto>> Leaders(
        Guid orgId, Guid competitionId, [FromQuery] string stat = "points",
        [FromQuery] string per = "total", [FromQuery] int limit = DefaultLimit, CancellationToken ct = default)
        => Ok(await _history.GetLeadersAsync(
            CompetitionId.FromGuid(competitionId), stat, per, Clamp(limit), ct));

    /// <summary>Every headline leaderboard in one round trip — the competition's home screen.</summary>
    /// <response code="200">All headline leaderboards.</response>
    /// <response code="400"><c>per</c> was neither total nor game.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("competitions/{competitionId:guid}/leaders/all")]
    [ProducesResponseType(typeof(AllLeadersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AllLeadersDto>> AllLeaders(
        Guid orgId, Guid competitionId, [FromQuery] string per = "total",
        [FromQuery] int limit = DefaultLimit, CancellationToken ct = default)
        => Ok(await _history.GetAllLeadersAsync(CompetitionId.FromGuid(competitionId), per, Clamp(limit), ct));

    /// <summary>A player's career page: totals plus the per-competition breakdown.</summary>
    /// <response code="200">The career page.</response>
    /// <response code="404">The player has no finalised games.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}/career-page")]
    [ProducesResponseType(typeof(CareerPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CareerPageDto>> CareerPage(Guid orgId, Guid playerId, CancellationToken ct)
        => Ok(await _history.GetCareerPageAsync(PlayerId.FromGuid(playerId), ct));

    /// <summary>A game's play-by-play, with the running score after each entry.</summary>
    /// <response code="200">The feed, in game order.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("games/{gameId:guid}/play-by-play")]
    [ProducesResponseType(typeof(IReadOnlyList<PlayByPlayEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PlayByPlayEntryDto>>> PlayByPlay(
        Guid orgId, Guid gameId, [FromQuery] int? period, CancellationToken ct)
        => Ok(await _history.GetPlayByPlayAsync(GameId.FromGuid(gameId), period, ct));

    /// <summary>A game's shot chart, optionally filtered to one team or player.</summary>
    /// <response code="200">The shots and their per-zone aggregation.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("games/{gameId:guid}/shot-chart")]
    [ProducesResponseType(typeof(ShotChartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShotChartDto>> GameShotChart(
        Guid orgId, Guid gameId, [FromQuery] Guid? teamId, [FromQuery] Guid? playerId, CancellationToken ct)
        => Ok(await _history.GetGameShotChartAsync(
            GameId.FromGuid(gameId),
            teamId.HasValue ? CompetitionTeamId.FromGuid(teamId.Value) : null,
            playerId.HasValue ? PlayerId.FromGuid(playerId.Value) : null, ct));

    /// <summary>A player's shot chart across a competition, or their whole career when omitted.</summary>
    /// <response code="200">The shots and their per-zone aggregation.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}/shot-chart")]
    [ProducesResponseType(typeof(ShotChartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShotChartDto>> PlayerShotChart(
        Guid orgId, Guid playerId, [FromQuery] Guid? competitionId, CancellationToken ct)
        => Ok(await _history.GetPlayerShotChartAsync(
            PlayerId.FromGuid(playerId),
            competitionId.HasValue ? CompetitionId.FromGuid(competitionId.Value) : null, ct));

    /// <summary>A game's lineups, aggregated from its stints.</summary>
    /// <response code="200">The lineups, longest-serving first.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("games/{gameId:guid}/lineups")]
    [ProducesResponseType(typeof(IReadOnlyList<LineupSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LineupSummaryDto>>> Lineups(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _history.GetLineupsAsync(GameId.FromGuid(gameId), ct));

    /// <summary>The organisation's all-time single-game and career records.</summary>
    /// <response code="200">The records.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("records")]
    [ProducesResponseType(typeof(OrganisationRecordsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganisationRecordsDto>> Records(
        Guid orgId, [FromQuery] int limit = 5, CancellationToken ct = default)
        => Ok(await _history.GetRecordsAsync(OrganisationId.FromGuid(orgId), Clamp(limit), ct));

    private static int Clamp(int limit) => Math.Clamp(limit, 1, MaxLimit);
}
