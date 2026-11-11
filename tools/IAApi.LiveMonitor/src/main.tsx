import { createRoot } from 'react-dom/client';
import { App } from './App';
import { api } from './api';
import './style.css';

const root = createRoot(document.getElementById('root')!);
root.render(<div className="boot">Carregando IAApi.LiveMonitor…</div>);
api.config().then(config => root.render(<App config={config}/>)).catch(() => root.render(<div className="boot"><h1>Servidor local indisponível</h1><p>Inicie com <code>npm run dev</code> ou <code>npm run preview</code>. O proxy local é necessário.</p><button onClick={() => location.reload()}>Tentar novamente</button></div>));
