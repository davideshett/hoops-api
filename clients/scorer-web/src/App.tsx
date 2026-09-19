import { useState } from 'react';
import { session } from './api';
import { Login } from './Login';
import { PickGame, type Picked } from './PickGame';
import { Record } from './Record';
import { Setup } from './Setup';

type Screen = { name: 'login' } | { name: 'pick' } | { name: 'setup'; picked: Picked } | { name: 'record'; picked: Picked };

export default function App() {
  const [screen, setScreen] = useState<Screen>(session.current ? { name: 'pick' } : { name: 'login' });

  switch (screen.name) {
    case 'login': return <Login onDone={() => setScreen({ name: 'pick' })} />;
    case 'pick': return <PickGame onPick={(picked) => setScreen(picked.game.status === 'Scheduled' ? { name: 'setup', picked } : { name: 'record', picked })}
      onSignOut={() => { session.clear(); setScreen({ name: 'login' }); }} />;
    case 'setup': return <Setup orgId={screen.picked.orgId} game={screen.picked.game} onLocked={() => setScreen({ name: 'record', picked: screen.picked })} onBack={() => setScreen({ name: 'pick' })} />;
    case 'record': return <Record orgId={screen.picked.orgId} role={screen.picked.role} game={screen.picked.game} onBack={() => setScreen({ name: 'pick' })} />;
  }
}
