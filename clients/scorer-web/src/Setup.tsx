import { useEffect, useState } from 'react';
import { ApiError, get, post } from './api';
import type { Game, GameSetup } from './types';

/** Dress the squad and mark the starters, then lock — after which the roster is frozen for this game. */
export function Setup({ orgId, game, onLocked, onBack }: { orgId: string; game: Game; onLocked: () => void; onBack: () => void }) {
  const [setup, setSetup] = useState<GameSetup | null>(null);
  const [dressed, setDressed] = useState<Set<string>>(new Set());
  const [starters, setStarters] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    get<GameSetup>(`/api/v1/organisations/${orgId}/games/${game.id}/setup`).then((s) => {
      setSetup(s);
      // Default: everyone dresses, the first five per team start. Adjust before locking.
      setDressed(new Set(s.teams.flatMap((t) => t.players.map((p) => p.rosterEntryId))));
      setStarters(new Set(s.teams.flatMap((t) => t.players.slice(0, s.ruleSet.playersOnCourt).map((p) => p.rosterEntryId))));
    }).catch((e) => setError(String(e)));
  }, [orgId, game.id]);

  function toggle(set: Set<string>, setter: (s: Set<string>) => void, id: string) {
    const next = new Set(set); next.has(id) ? next.delete(id) : next.add(id); setter(next);
  }

  async function lock() {
    if (!setup) return;
    setBusy(true); setError(null);
    try {
      const selections = [...dressed].map((rosterEntryId) => ({ rosterEntryId, isStarter: starters.has(rosterEntryId) }));
      await post(`/api/v1/organisations/${orgId}/games/${game.id}/lock-roster`, { selections });
      onLocked();
    } catch (e) { setError(e instanceof ApiError ? `${e.problem.code}: ${e.problem.detail}` : String(e)); }
    finally { setBusy(false); }
  }

  if (!setup) return <div className="page">{error ?? 'Loading setup…'}</div>;
  const n = setup.ruleSet.playersOnCourt;

  return (
    <div className="page">
      <header className="bar">
        <button onClick={onBack}>← Games</button>
        <h1>{setup.teams[0].name} vs {setup.teams[1].name}</h1>
        <span className="muted">{setup.ruleSet.numberOfPeriods} × {setup.ruleSet.periodDurationSeconds / 60} min · foul out at {setup.ruleSet.personalFoulLimit}</span>
      </header>
      <p className="muted">Tick who dresses; star exactly {n} starters per team. Locking freezes this roster for the game — a jersey change afterwards will not affect it.</p>
      <div className="columns">
        {setup.teams.map((team) => {
          const teamStarters = team.players.filter((p) => starters.has(p.rosterEntryId)).length;
          return (
            <section key={team.competitionTeamId} className="card">
              <h2>{team.name} <span className={teamStarters === n ? 'ok' : 'error'}>{teamStarters}/{n} starters</span></h2>
              {team.players.map((p) => (
                <div key={p.rosterEntryId} className="player-row">
                  <input type="checkbox" checked={dressed.has(p.rosterEntryId)} onChange={() => toggle(dressed, setDressed, p.rosterEntryId)} />
                  <button className={starters.has(p.rosterEntryId) ? 'star on' : 'star'} disabled={!dressed.has(p.rosterEntryId)}
                    onClick={() => toggle(starters, setStarters, p.rosterEntryId)} title="Starter">★</button>
                  <span className="jersey">#{p.jerseyNumber}</span>
                  <span>{p.fullName}</span>
                  <span className="muted">{p.position}{p.isCaptain ? ' · C' : ''}</span>
                </div>
              ))}
            </section>
          );
        })}
      </div>
      {error && <p className="error">{error}</p>}
      <button className="primary big" disabled={busy} onClick={lock}>{busy ? 'Locking…' : 'Lock roster'}</button>
    </div>
  );
}
