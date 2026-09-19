import { useEffect, useMemo, useRef, useState } from 'react';
import { ApiError, get, post } from './api';
import { formatClock, parseClock, uuidv7 } from './clock';
import { Court } from './Court';
import { CsvImport } from './CsvImport';
import { SubmitQueue, fetchState, type Outcome } from './queue';
import type { Game, GameRosterEntry, GameSetup, LiveState, SubmitEvent } from './types';

interface Props { orgId: string; role: string; game: Game; onBack: () => void }

// Finalisation is a review step, not a recording step (§9.3): the statistician ends the game, a
// competition manager finalises it.
const CAN_FINALISE = new Set(['Owner', 'Admin', 'CompetitionManager']);

type Pending =
  | { kind: 'shot'; made: boolean; three: boolean; x?: number; y?: number }
  | { kind: 'ft'; made: boolean }
  | { kind: 'turnover' }
  | { kind: 'foul' }
  | { kind: 'sub' };

const TURNOVERS = ['LostBall', 'BadPass', 'Travelling', 'DoubleDribble', 'OffensiveFoul', 'ShotClockViolation', 'OutOfBounds', 'BackCourt', 'Other'];
const FOULS = ['Personal', 'Shooting', 'Offensive', 'Technical', 'Unsportsmanlike', 'Disqualifying'];

export function Record({ orgId, role, game, onBack }: Props) {
  const [setup, setSetup] = useState<GameSetup | null>(null);
  const [roster, setRoster] = useState<GameRosterEntry[]>([]);
  const [live, setLive] = useState<LiveState | null>(null);
  const [period, setPeriod] = useState(1);
  const [clockText, setClockText] = useState('10:00');
  const [actor, setActor] = useState<string | null>(null);
  const [pending, setPending] = useState<Pending | null>(null);
  const [subtype, setSubtype] = useState('');
  const [ftAwarded, setFtAwarded] = useState('2');
  const [ftAttempt, setFtAttempt] = useState<[number, number]>([1, 2]);
  const [feed, setFeed] = useState<Outcome[]>([]);
  const [rejected, setRejected] = useState<Extract<Outcome, { kind: 'rejected' }> | null>(null);
  const [reason, setReason] = useState('');
  const [queued, setQueued] = useState(0);
  const [offline, setOffline] = useState(false);
  const [showCsv, setShowCsv] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const queue = useRef<SubmitQueue | null>(null);

  const url = `/api/v1/organisations/${orgId}/games/${game.id}`;

  useEffect(() => {
    (async () => {
      const [s, r, state] = await Promise.all([get<GameSetup>(`${url}/setup`), get<GameRosterEntry[]>(`${url}/roster`), fetchState(orgId, game.id)]);
      setSetup(s); setRoster(r); setLive(state);
      setPeriod(Math.max(1, state.currentPeriod));
      setClockText(formatClock(state.gameStarted ? state.gameClockMs : s.ruleSet.periodDurationSeconds * 1000));
      queue.current = new SubmitQueue(orgId, game.id, state.lastSequence, (outcome) => {
        setFeed((f) => [outcome, ...f].slice(0, 12));
        if (outcome.kind === 'accepted') {
          setLive(outcome.result.state); setOffline(false);
          // The typed field follows the log, so a CSV import or a replay leaves it where the game is.
          setPeriod(Math.max(1, outcome.result.state.currentPeriod));
          if (!outcome.result.state.periodEnded) setClockText(formatClock(outcome.result.state.gameClockMs));
        }
        if (outcome.kind === 'rejected') setRejected(outcome);
        if (outcome.kind === 'offline') setOffline(true);
      }, setQueued);
      void queue.current.flush();
    })().catch((e) => setError(String(e)));
  }, [orgId, game.id, url]);

  const home = setup?.teams[0], away = setup?.teams[1];
  const byId = useMemo(() => new Map(roster.map((e) => [e.id, e])), [roster]);
  const teamOf = (id: string) => byId.get(id)?.competitionTeamId ?? null;
  const onCourt = (id: string) => live?.onCourt[teamOf(id) ?? '']?.includes(id) ?? false;
  const label = (id: string | null | undefined) => id && byId.get(id) ? `#${byId.get(id)!.jerseyNumber} ${byId.get(id)!.fullName.split(' ').pop()}` : '—';

  function clockMs(): number | null {
    const ms = parseClock(clockText);
    if (ms === null) setError('Clock must be mm:ss');
    return ms;
  }

  function submit(partial: Omit<SubmitEvent, 'eventId' | 'period' | 'gameClockMs' | 'clientRecordedAt'>) {
    const ms = clockMs(); if (ms === null) return;
    queue.current?.enqueue({ ...partial, eventId: uuidv7(), period, gameClockMs: ms, clientRecordedAt: new Date().toISOString() });
    setPending(null); setActor(null); setError(null);
  }

  // ── flow events ─────────────────────────────────────────────────────────
  function periodStart() {
    const isOt = period > (setup?.ruleSet.numberOfPeriods ?? 4);
    const full = (isOt ? setup!.ruleSet.overtimeDurationSeconds : setup!.ruleSet.periodDurationSeconds) * 1000;
    setClockText(formatClock(full));
    queue.current?.enqueue({ eventId: uuidv7(), eventType: 'PERIOD_START', eventSubtype: isOt ? 'Overtime' : 'Regulation', period, gameClockMs: full, clientRecordedAt: new Date().toISOString() });
  }
  function periodEnd() {
    setClockText('0:00');
    queue.current?.enqueue({ eventId: uuidv7(), eventType: 'PERIOD_END', period, gameClockMs: 0, clientRecordedAt: new Date().toISOString() });
    setPeriod((p) => p + 1);
  }
  const clock = (type: 'CLOCK_START' | 'CLOCK_STOP') => submit({ eventType: type });
  const timeout = (teamId: string) => submit({ eventType: 'TIMEOUT', competitionTeamId: teamId, payload: { timeoutType: 'Team' } });
  const teamRebound = (teamId: string, sub: 'Offensive' | 'Defensive') => submit({ eventType: 'TEAM_REBOUND', eventSubtype: sub, competitionTeamId: teamId });

  async function control(action: 'start' | 'end' | 'finalize' | 'undo') {
    try {
      if (action === 'undo') { const r = await post<{ state: LiveState }>(`${url}/events/undo-last`); setLive(r.state); return; }
      const r = await post<LiveState>(`${url}/${action}`);
      if (action !== 'finalize') setLive(r);
      else setError('Finalised. Standings and leaderboards rebuild in a few seconds.');
    } catch (e) { setError(e instanceof ApiError ? `${e.problem.code}: ${e.problem.detail}` : String(e)); }
  }

  // ── player events ───────────────────────────────────────────────────────
  function shot(made: boolean, three: boolean) { if (actor) setPending({ kind: 'shot', made, three }); }

  function finishShot(secondary: string | null) {
    if (!actor || !pending || pending.kind !== 'shot' || pending.x === undefined) return;
    submit({
      eventType: pending.made ? 'FIELD_GOAL_MADE' : 'FIELD_GOAL_MISSED', eventSubtype: pending.three ? 'ThreePoint' : 'TwoPoint',
      competitionTeamId: teamOf(actor), gameRosterEntryId: actor, secondaryRosterEntryId: secondary, shotXCm: pending.x, shotYCm: pending.y,
    });
  }

  function freeThrow(made: boolean) {
    if (!actor) return;
    submit({ eventType: made ? 'FREE_THROW_MADE' : 'FREE_THROW_MISSED', competitionTeamId: teamOf(actor), gameRosterEntryId: actor,
      payload: { attemptNumber: String(ftAttempt[0]), totalAttempts: String(ftAttempt[1]) } });
    // Advance to the next attempt of the sequence, so consecutive taps need no re-selection.
    if (ftAttempt[0] < ftAttempt[1]) { setFtAttempt([ftAttempt[0] + 1, ftAttempt[1]]); setActor(actor); }
  }

  const rebound = (sub: 'Offensive' | 'Defensive') => actor && submit({ eventType: 'REBOUND', eventSubtype: sub, competitionTeamId: teamOf(actor), gameRosterEntryId: actor });

  function finishTurnover(stealer: string | null) {
    if (!actor) return;
    submit({ eventType: 'TURNOVER', eventSubtype: subtype || 'LostBall', competitionTeamId: teamOf(actor), gameRosterEntryId: actor, secondaryRosterEntryId: stealer });
  }

  function finishFoul(fouled: string | null) {
    if (!actor) return;
    submit({ eventType: 'FOUL', eventSubtype: subtype || 'Personal', competitionTeamId: teamOf(actor), gameRosterEntryId: actor, secondaryRosterEntryId: fouled,
      payload: { freeThrowsAwarded: ftAwarded } });
    if (ftAwarded !== '0' && fouled) { setFtAttempt([1, Number(ftAwarded)]); setActor(fouled); }
  }

  function finishSub(incoming: string) {
    if (!actor) return;
    submit({ eventType: 'SUBSTITUTION', competitionTeamId: teamOf(actor), gameRosterEntryId: actor, secondaryRosterEntryId: incoming });
  }

  /** A tap on a player while an action is waiting for its second participant. */
  function tapPlayer(id: string) {
    if (!pending) { setActor(id === actor ? null : id); return; }
    if (pending.kind === 'shot' && pending.x !== undefined) return finishShot(id);
    if (pending.kind === 'turnover') return finishTurnover(id);
    if (pending.kind === 'foul') return finishFoul(id);
    if (pending.kind === 'sub') return finishSub(id);
  }

  if (!setup || !live || !home || !away) return <div className="page">{error ?? 'Loading game…'}</div>;

  const prompt = !pending ? null
    : pending.kind === 'shot' && pending.x === undefined ? 'Tap where the shot was taken'
    : pending.kind === 'shot' ? (pending.made ? 'Tap the assister, or No assist' : 'Tap the blocker, or None')
    : pending.kind === 'turnover' ? 'Tap the stealer, or None'
    : pending.kind === 'foul' ? 'Tap the player fouled, or None'
    : pending.kind === 'sub' ? `${label(actor)} out — tap who comes in` : null;

  const fouledOut = (id: string) => false && id; // per-player fouls are not in LiveState; the server enforces the limit.

  const teamPanel = (team: typeof home, side: 'home' | 'away') => (
    <section className={`team ${side}`}>
      <h2>{team.name} <span className="score">{live.score[team.competitionTeamId] ?? 0}</span></h2>
      <div className="muted small">Fouls {live.teamFouls[team.competitionTeamId] ?? 0} · TO left {live.timeoutsRemaining[team.competitionTeamId] ?? '–'}</div>
      <div className="team-actions">
        <button onClick={() => timeout(team.competitionTeamId)}>Timeout</button>
        <button onClick={() => teamRebound(team.competitionTeamId, 'Defensive')}>Team reb D</button>
        <button onClick={() => teamRebound(team.competitionTeamId, 'Offensive')}>Team reb O</button>
      </div>
      <div className="players">
        {roster.filter((e) => e.competitionTeamId === team.competitionTeamId)
          .sort((a, b) => Number(onCourt(b.id)) - Number(onCourt(a.id)) || a.jerseyNumber.localeCompare(b.jerseyNumber, undefined, { numeric: true }))
          .map((e) => (
            <button key={e.id} className={`player ${onCourt(e.id) ? 'on' : 'bench'} ${actor === e.id ? 'selected' : ''} ${fouledOut(e.id) ? 'out' : ''}`}
              onClick={() => tapPlayer(e.id)}>
              <span className="jersey">{e.jerseyNumber}</span>
              <span className="name">{e.fullName}</span>
            </button>
          ))}
      </div>
    </section>
  );

  return (
    <div className="record">
      <header className="bar">
        <button onClick={onBack}>← Games</button>
        <div className="clock">
          <label>Period <input type="number" min={1} max={9} value={period} onChange={(e) => setPeriod(Number(e.target.value))} /></label>
          <label>Clock <input className="clock-input" value={clockText} onChange={(e) => setClockText(e.target.value)} placeholder="mm:ss" /></label>
          <button onClick={() => { const ms = parseClock(clockText); if (ms !== null) setClockText(formatClock(Math.max(0, ms - 10_000))); }}>−10s</button>
          <button onClick={() => { const ms = parseClock(clockText); if (ms !== null) setClockText(formatClock(Math.max(0, ms - 1_000))); }}>−1s</button>
          <span className={`pill ${live.clockRunning ? 'running' : ''}`}>{live.clockRunning ? 'clock running' : 'clock stopped'}</span>
        </div>
        <div className="status">
          <span className="muted">seq {live.lastSequence}</span>
          {queued > 0 && <span className="pill warn">{queued} queued{offline ? ' · offline' : ''}</span>}
          <button onClick={() => setShowCsv(true)}>Import CSV</button>
        </div>
      </header>

      <div className="flow">
        {!live.gameStarted && <button className="primary" onClick={() => control('start')}>Start game</button>}
        <button onClick={periodStart}>Period start</button>
        <button onClick={() => clock('CLOCK_START')} disabled={live.clockRunning}>Clock start</button>
        <button onClick={() => clock('CLOCK_STOP')} disabled={!live.clockRunning}>Clock stop</button>
        <button onClick={periodEnd}>Period end</button>
        <button className="danger" onClick={() => control('undo')}>Undo last</button>
        {live.gameStarted && !live.gameEnded && <button className="primary" onClick={() => control('end')}>End game</button>}
        {live.gameEnded && (CAN_FINALISE.has(role)
          ? <button className="primary" onClick={() => control('finalize')}>Finalise</button>
          : <span className="pill">Game ended — awaiting finalisation by a competition manager</span>)}
      </div>

      <div className="board">
        {teamPanel(home, 'home')}

        <section className="actions">
          {prompt && <div className="prompt">{prompt}
            {pending?.kind === 'shot' && pending.x !== undefined && <button onClick={() => finishShot(null)}>{pending.made ? 'No assist' : 'None'}</button>}
            {pending?.kind === 'turnover' && <button onClick={() => finishTurnover(null)}>None</button>}
            {pending?.kind === 'foul' && <button onClick={() => finishFoul(null)}>None</button>}
            <button onClick={() => setPending(null)}>Cancel</button>
          </div>}

          {pending?.kind === 'shot' && pending.x === undefined
            ? <Court onTap={(x, y) => setPending({ ...pending, x, y })} />
            : (
              <>
                <div className="selected">{actor ? <>Selected: <b>{label(actor)}</b> {onCourt(actor) ? '' : '(bench)'}</> : 'Tap a player, then an action'}</div>
                <div className="grid" aria-disabled={!actor}>
                  <button className="made" disabled={!actor} onClick={() => shot(true, false)}>2PT ✓</button>
                  <button className="missed" disabled={!actor} onClick={() => shot(false, false)}>2PT ✗</button>
                  <button className="made" disabled={!actor} onClick={() => shot(true, true)}>3PT ✓</button>
                  <button className="missed" disabled={!actor} onClick={() => shot(false, true)}>3PT ✗</button>
                  <button className="made" disabled={!actor} onClick={() => freeThrow(true)}>FT ✓ {ftAttempt[0]}/{ftAttempt[1]}</button>
                  <button className="missed" disabled={!actor} onClick={() => freeThrow(false)}>FT ✗ {ftAttempt[0]}/{ftAttempt[1]}</button>
                  <button disabled={!actor} onClick={() => rebound('Defensive')}>Reb D</button>
                  <button disabled={!actor} onClick={() => rebound('Offensive')}>Reb O</button>
                  <button disabled={!actor} onClick={() => { setSubtype('LostBall'); setPending({ kind: 'turnover' }); }}>Turnover</button>
                  <button disabled={!actor} onClick={() => { setSubtype('Personal'); setPending({ kind: 'foul' }); }}>Foul</button>
                  <button disabled={!actor} onClick={() => setPending({ kind: 'sub' })}>Sub out</button>
                </div>
                <div className="row small">
                  <label>FT sequence
                    <select value={ftAttempt.join('/')} onChange={(e) => { const [a, t] = e.target.value.split('/').map(Number); setFtAttempt([a, t]); }}>
                      {['1/1', '1/2', '2/2', '1/3', '2/3', '3/3'].map((v) => <option key={v}>{v}</option>)}
                    </select>
                  </label>
                  {pending?.kind === 'turnover' && <label>Type <select value={subtype} onChange={(e) => setSubtype(e.target.value)}>{TURNOVERS.map((t) => <option key={t}>{t}</option>)}</select></label>}
                  {pending?.kind === 'foul' && <>
                    <label>Type <select value={subtype} onChange={(e) => setSubtype(e.target.value)}>{FOULS.map((t) => <option key={t}>{t}</option>)}</select></label>
                    <label>FTs <select value={ftAwarded} onChange={(e) => setFtAwarded(e.target.value)}>{['0', '1', '2', '3'].map((n) => <option key={n}>{n}</option>)}</select></label>
                  </>}
                </div>
              </>
            )}

          <div className="feed">
            {feed.map((o, i) => (
              <div key={i} className={`feed-row ${o.kind}`}>
                {o.kind === 'accepted' && <>#{o.result.sequence} {o.event.eventType}{o.event.eventSubtype ? `/${o.event.eventSubtype}` : ''} {label(o.event.gameRosterEntryId)} {o.result.event.points ? `+${o.result.event.points}` : ''} {o.result.event.shotZone ?? ''} {o.replay ? '(replay)' : ''}</>}
                {o.kind === 'rejected' && <>✗ {o.event.eventType} — {o.problem.code}</>}
                {o.kind === 'offline' && <>⏸ {o.event.eventType} queued (offline)</>}
              </div>
            ))}
          </div>
          {error && <p className="error">{error}</p>}
        </section>

        {teamPanel(away, 'away')}
      </div>

      {rejected && (
        <div className="modal-backdrop">
          <div className="modal">
            <h2>{rejected.overridable ? 'Rule check' : 'Rejected'}</h2>
            <p><code>{rejected.problem.code}</code></p>
            <p>{rejected.problem.detail}</p>
            {rejected.overridable ? (
              <>
                <label>Reason for recording it anyway<input value={reason} onChange={(e) => setReason(e.target.value)} autoFocus /></label>
                <div className="row">
                  <button className="primary" disabled={!reason.trim()} onClick={() => { queue.current?.retryWithOverride(rejected.event, reason.trim()); setRejected(null); setReason(''); }}>Override and record</button>
                  <button onClick={() => { queue.current?.flush(); setRejected(null); }}>Discard this event</button>
                </div>
              </>
            ) : (
              <button className="primary" onClick={() => { queue.current?.flush(); setRejected(null); }}>OK</button>
            )}
          </div>
        </div>
      )}

      {showCsv && <CsvImport roster={roster} home={home} away={away} onSubmit={(events) => { events.forEach((e) => queue.current?.enqueue(e)); setShowCsv(false); }} onClose={() => setShowCsv(false)} />}
    </div>
  );
}
