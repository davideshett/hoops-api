import { useState } from 'react';
import { ApiError, login } from './api';

export function Login({ onDone }: { onDone: () => void }) {
  const [email, setEmail] = useState('anambra.stats@hoops.local');
  const [password, setPassword] = useState('Seed-Password-1');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try { await login(email, password); onDone(); }
    catch (err) { setError(err instanceof ApiError ? err.problem.detail : 'Cannot reach the API. Is it running on :5290?'); }
    finally { setBusy(false); }
  }

  return (
    <div className="centered">
      <form className="card" onSubmit={submit}>
        <h1>Hoops Scorer</h1>
        <p className="muted">Sign in with a statistician or admin account.</p>
        <label>Email<input value={email} onChange={(e) => setEmail(e.target.value)} autoFocus /></label>
        <label>Password<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} /></label>
        {error && <p className="error">{error}</p>}
        <button className="primary" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </div>
  );
}
