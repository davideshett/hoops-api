import { ApiError, get, post } from './api';
import type { EventAccepted, GameEvent, LiveState, SubmitEvent } from './types';

/**
 * The submit queue (guide §8). Events are queued in tap order and sent ONE AT A TIME, each carrying
 * the sequence from the previous response. The queue persists in localStorage so a closed tab or a
 * lost connection loses nothing; a retry after a lost response is safe because eventId is the
 * idempotency key (a 200 instead of a 201 means "already there").
 */

export type Outcome =
  | { kind: 'accepted'; event: SubmitEvent; result: EventAccepted; replay: boolean }
  | { kind: 'rejected'; event: SubmitEvent; problem: ApiError['problem']; overridable: boolean }
  | { kind: 'offline'; event: SubmitEvent };

const OVERRIDABLE = new Set([
  'PLAYER_NOT_ON_COURT', 'PLAYER_FOULED_OUT', 'INVALID_LINEUP_SIZE', 'SUBSTITUTION_WHILE_LIVE',
  'CLOCK_NOT_MONOTONIC', 'CLOCK_OUT_OF_RANGE', 'REBOUND_WITHOUT_MISS', 'FREE_THROW_WITHOUT_SOURCE',
  'SHOT_ZONE_MISMATCH', 'TIMEOUT_LIMIT_EXCEEDED', 'PERIOD_NOT_COMPLETE',
]);

export class SubmitQueue {
  private pending: SubmitEvent[];
  private sending = false;
  private lastSequence: number | undefined;
  private readonly orgId: string;
  private readonly gameId: string;
  private readonly onOutcome: (outcome: Outcome) => void;
  private readonly onChange: (pending: number) => void;

  constructor(orgId: string, gameId: string, lastSequence: number | undefined,
    onOutcome: (outcome: Outcome) => void, onChange: (pending: number) => void) {
    this.orgId = orgId; this.gameId = gameId; this.onOutcome = onOutcome; this.onChange = onChange;
    this.pending = JSON.parse(localStorage.getItem(this.key) ?? '[]');
    this.lastSequence = lastSequence;
    this.onChange(this.pending.length);
  }

  private get key() { return `hoops.queue.${this.gameId}`; }

  get size() { return this.pending.length; }

  enqueue(event: SubmitEvent) {
    this.pending.push(event);
    this.persist();
    void this.flush();
  }

  /** Replace a rejected event with its override and send again. */
  retryWithOverride(event: SubmitEvent, reason: string) {
    this.pending.unshift({ ...event, override: { reason } });
    this.persist();
    void this.flush();
  }

  drop(eventId: string) {
    this.pending = this.pending.filter((e) => e.eventId !== eventId);
    this.persist();
    void this.flush();
  }

  async flush(): Promise<void> {
    if (this.sending) return;
    this.sending = true;
    try {
      while (this.pending.length > 0) {
        const event = this.pending[0];
        const outcome = await this.send(event);
        if (outcome.kind === 'offline') { this.onOutcome(outcome); return; }
        this.pending.shift();
        this.persist();
        this.onOutcome(outcome);
        if (outcome.kind === 'rejected') return; // the official decides; nothing behind it goes yet
      }
    } finally {
      this.sending = false;
    }
  }

  private async send(event: SubmitEvent): Promise<Outcome> {
    const { override, ...body } = event;
    const url = `/api/v1/organisations/${this.orgId}/games/${this.gameId}/events${override ? '?override=true' : ''}`;
    try {
      const result = await post<EventAccepted>(url, { ...body, lastKnownSequence: this.lastSequence },
        override ? { headers: { 'X-Override-Reason': override.reason } } : undefined);
      const replay = result.sequence <= (this.lastSequence ?? -1);
      this.lastSequence = Math.max(this.lastSequence ?? 0, result.state.lastSequence);
      return { kind: 'accepted', event, result, replay };
    } catch (error) {
      if (!(error instanceof ApiError)) return { kind: 'offline', event };
      const { problem } = error;
      if (problem.code === 'STALE_SEQUENCE') {
        // Someone recorded ahead of us. Resync, then try this event again from the new tip.
        const missing = await get<GameEvent[]>(
          `/api/v1/organisations/${this.orgId}/games/${this.gameId}/events?sinceSequence=${this.lastSequence ?? 0}`);
        this.lastSequence = Math.max(this.lastSequence ?? 0, ...missing.map((m) => m.sequence));
        return this.send(event);
      }
      if (problem.status === 429) {
        await new Promise((r) => setTimeout(r, 1500));
        return this.send(event);
      }
      return { kind: 'rejected', event, problem, overridable: OVERRIDABLE.has(problem.code) };
    }
  }

  private persist() {
    localStorage.setItem(this.key, JSON.stringify(this.pending));
    this.onChange(this.pending.length);
  }
}

export async function fetchState(orgId: string, gameId: string): Promise<LiveState> {
  return get<LiveState>(`/api/v1/organisations/${orgId}/games/${gameId}/state`);
}
