using Hoops.Api.Auth;
using Hoops.Modules.GameRecording.Contracts;
using Hoops.Modules.Statistics.Contracts;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Statistics;

/// <summary>Finalisation and the persisted statistics it produces.</summary>
[Route("api/v1/organisations/{orgId:guid}")]
public sealed class StatisticsController : ApiControllerBase
{
    private readonly IGameFinalizationService _finalization;
    private readonly IStatisticsQueryService _query;
    private readonly IStatisticsRecomputeService _recompute;

    /// <summary>Creates the controller.</summary>
    public StatisticsController(
        IGameFinalizationService finalization, IStatisticsQueryService query, IStatisticsRecomputeService recompute)
    {
        _finalization = finalization;
        _query = query;
        _recompute = recompute;
    }

    /// <summary>Finalises a reviewed game, so it starts contributing to leaderboards and careers.</summary>
    /// <response code="200">The finalised game.</response>
    /// <response code="409">The game is not in PendingReview.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("games/{gameId:guid}/finalize")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDto>> Finalize(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _finalization.FinalizeAsync(GameId.FromGuid(gameId), ct));

    /// <summary>Reopens a finalised game for correction. Admin-only; a reason is required and audited.</summary>
    /// <response code="200">The reopened game.</response>
    /// <response code="400">No reason was supplied.</response>
    /// <response code="409">The game is not Finalized.</response>
    [Authorize(Policy = AuthPolicies.OrgAdmin)]
    [HttpPost("games/{gameId:guid}/reopen")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDto>> Reopen(
        Guid orgId, Guid gameId, [FromBody] ReopenGameRequest request, CancellationToken ct)
        => Ok(await _finalization.ReopenAsync(GameId.FromGuid(gameId), request, ct));

    /// <summary>Awards a forfeit to one of the two teams.</summary>
    /// <response code="200">The forfeited game.</response>
    /// <response code="400">No reason was supplied.</response>
    /// <response code="409">The winner is not one of the teams, or the game is already final.</response>
    [Authorize(Policy = AuthPolicies.OrgManager)]
    [HttpPost("games/{gameId:guid}/forfeit")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GameDto>> Forfeit(
        Guid orgId, Guid gameId, [FromBody] ForfeitGameRequest request, CancellationToken ct)
        => Ok(await _finalization.ForfeitAsync(GameId.FromGuid(gameId), request, ct));

    /// <summary>A competition's standings, in table order after tiebreakers.</summary>
    /// <response code="200">The standings.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("competitions/{competitionId:guid}/standings")]
    [ProducesResponseType(typeof(IReadOnlyList<StandingsRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<StandingsRowDto>>> Standings(
        Guid orgId, Guid competitionId, CancellationToken ct)
        => Ok(await _query.GetStandingsAsync(CompetitionId.FromGuid(competitionId), ct));

    /// <summary>A competition's player aggregates. Pass <c>qualifiedOnly</c> for per-game leaderboards.</summary>
    /// <response code="200">The aggregates, highest scoring first.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("competitions/{competitionId:guid}/player-stats")]
    [ProducesResponseType(typeof(IReadOnlyList<CompetitionPlayerAggregateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CompetitionPlayerAggregateDto>>> PlayerStats(
        Guid orgId, Guid competitionId, [FromQuery] bool qualifiedOnly, CancellationToken ct)
        => Ok(await _query.GetCompetitionPlayersAsync(CompetitionId.FromGuid(competitionId), qualifiedOnly, ct));

    /// <summary>A player's career totals, across every organisation they have played for.</summary>
    /// <response code="200">The career totals.</response>
    /// <response code="404">The player has no finalised games.</response>
    [Authorize(Policy = AuthPolicies.OrgMember)]
    [HttpGet("players/{playerId:guid}/career")]
    [ProducesResponseType(typeof(PlayerCareerAggregateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerCareerAggregateDto>> Career(Guid orgId, Guid playerId, CancellationToken ct)
        => Ok(await _query.GetCareerAsync(PlayerId.FromGuid(playerId), ct));

    /// <summary>Rebuilds one game's statistics from its event log, then its competition's aggregates.</summary>
    /// <response code="200">What was rebuilt.</response>
    [Authorize(Policy = AuthPolicies.OrgAdmin)]
    [HttpPost("admin/recompute/games/{gameId:guid}")]
    [ProducesResponseType(typeof(RecomputeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecomputeSummaryDto>> RecomputeGame(Guid orgId, Guid gameId, CancellationToken ct)
        => Ok(await _recompute.RecomputeGameAsync(GameId.FromGuid(gameId), ct));

    /// <summary>
    /// Rebuilds an entire competition from its event logs. Safe to run at any time — it is the
    /// disaster-recovery path and the correctness audit in one (§9.3).
    /// </summary>
    /// <response code="200">What was rebuilt.</response>
    [Authorize(Policy = AuthPolicies.OrgAdmin)]
    [HttpPost("admin/recompute/competitions/{competitionId:guid}")]
    [ProducesResponseType(typeof(RecomputeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RecomputeSummaryDto>> RecomputeCompetition(
        Guid orgId, Guid competitionId, CancellationToken ct)
        => Ok(await _recompute.RecomputeCompetitionAsync(CompetitionId.FromGuid(competitionId), ct));
}
