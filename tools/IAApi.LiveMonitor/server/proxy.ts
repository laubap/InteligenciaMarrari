import type { IncomingMessage, ServerResponse } from 'node:http';
import type { Plugin } from 'vite';

type Env = Record<string, string | undefined>;
const integer = (v: string | undefined, fallback: number, min: number) =>
  v && Number.isFinite(Number(v)) ? Math.max(min, Math.round(Number(v))) : fallback;
export function settings(env: Env) {
  const target = new URL(env.IAAPI_URL || 'http://localhost:5145');
  if (!['http:', 'https:'].includes(target.protocol) || target.username || target.password)
    throw new Error('IAAPI_URL deve ser HTTP(S), sem credenciais.');
  const key = env.IAAPI_DIAGNOSTICS_KEY || '';
  if (key && !['localhost', '127.0.0.1', '[::1]'].includes(target.hostname))
    throw new Error('Diagnósticos protegidos exigem IAApi em loopback local.');
  const sourceOffset = env.MONITOR_SOURCE_OFFSET || '-03:00';
  if (!/^[+-](0\d|1[0-4]):[0-5]\d$/.test(sourceOffset)) throw new Error('Offset inválido.');
  return { target, key, public: {
    client: env.MONITOR_CLIENT || 'PK2', pollMs: integer(env.MONITOR_POLL_MS, 2000, 1000),
    // 34.4 (P42): padrão de 60 s (era 15 s); o gráfico tem 1 ponto por minuto.
    historyMs: integer(env.MONITOR_HISTORY_MS, 60000, 5000),
    staleMs: integer(env.MONITOR_STALE_MS, 180000, 10000), sourceOffset,
    diagnosticsConfigured: Boolean(key), apiOrigin: target.origin,
  } };
}

// Lista fechada: até mesmo GETs da API fora destas rotas são recusados.
export function upstreamPath(url: URL): string | null {
  const client = url.searchParams.get('client') || 'PK2';
  const tag = url.searchParams.get('tag');
  if (client.length > 128 || (tag && tag.length > 512)) return null;
  switch (url.pathname) {
    case '/monitor-api/tags': return '/api/tags';
    case '/monitor-api/metadata': return `/api/tags/bridge/${encodeURIComponent(client)}`;
    case '/monitor-api/decisions': {
      // 34.2 (P42): o filtro por cliente e o `desde` vão para a IAApi.
      const since = url.searchParams.get('since');
      if (since !== null && !/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d{1,7})?$/.test(since)) return null;
      return `/api/anomalias?clienteId=${encodeURIComponent(client)}${since ? `&desde=${encodeURIComponent(since)}` : ''}`;
    }
    case '/monitor-api/history': return tag ? `/api/tags/historico/${encodeURIComponent(client)}/${encodeURIComponent(tag)}?horas=2` : null;
    case '/monitor-api/diagnostics': return tag ? `/api/intelligence-lab/diagnostics?limit=100&clienteId=${encodeURIComponent(client)}&tagName=${encodeURIComponent(tag)}` : null;
    case '/monitor-api/shadow': return `/api/intelligence-lab/diagnostics/multivariate-shadow?limit=100&clienteId=${encodeURIComponent(client)}`;
    default: {
      const match = url.pathname.match(/^\/monitor-api\/(diagnostic|shadow)\/([a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12})$/i);
      return match ? `/api/intelligence-lab/diagnostics/${match[1] === 'shadow' ? 'multivariate-shadow/' : ''}${match[2]}` : null;
    }
  }
}

export const TIMEOUT_MS = 12000;
// 34.4 (P42): o histórico não é abandonado enquanto a IAApi ainda busca no
// Bridge. A IAApi espera até 60 s por chamada ao Bridge (BridgeHistoricoService)
// e, com período vazio ou erro do Bridge, faz uma segunda (24 h; 34.3): até
// 120 s. O proxy só desiste depois disso, e o fechamento da página não cancela
// a chamada à IAApi (cancelar aqui não cancela o Bridge).
export const HISTORY_TIMEOUT_MS = 125000;
type Upstream = { status: number; body?: unknown };

export function createMiddleware(env: Env) {
  const config = settings(env);
  // 34.4 (P42): no máximo uma consulta de histórico por cliente/tag em curso na
  // IAApi. Um pedido que chega enquanto a anterior roda espera a mesma resposta.
  const historyInFlight = new Map<string, Promise<Upstream>>();
  const callUpstream = async (path: string, signal: AbortSignal, headers: Record<string, string>): Promise<Upstream> => {
    const response = await fetch(new URL(path, config.target), { method: 'GET', redirect: 'error', signal, headers });
    return response.ok ? { status: 200, body: await response.json() } : { status: response.status };
  };
  const sharedHistory = (path: string) => {
    let pending = historyInFlight.get(path);
    if (!pending) {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), HISTORY_TIMEOUT_MS);
      pending = callUpstream(path, controller.signal, {})
        .finally(() => { clearTimeout(timeout); historyInFlight.delete(path); });
      historyInFlight.set(path, pending);
    }
    return pending;
  };
  return async (req: IncomingMessage, res: ServerResponse, next: () => void) => {
    if (!req.url?.startsWith('/monitor-api')) return next();
    const reply = (status: number, body: unknown) => {
      res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
      res.end(JSON.stringify(body));
    };
    // Não permitir publicação acidental via --host, proxy externo ou acesso cross-site.
    const remote = req.socket.remoteAddress;
    const origin = req.headers.origin;
    const host = req.headers.host || '';
    if (!['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(remote || '') ||
        !/^(127\.0\.0\.1|localhost|\[::1\]):\d+$/.test(host) ||
        (origin && origin !== `http://${host}`) || req.headers['sec-fetch-site'] === 'cross-site')
      return reply(403, { error: 'Monitor restrito ao computador local.' });
    if (req.method !== 'GET') return reply(405, { error: 'Somente leitura.' });
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname === '/monitor-api/config') return reply(200, config.public);
    const path = upstreamPath(url);
    if (!path) return reply(404, { error: 'Rota não permitida.' });
    const protectedRoute = path.startsWith('/api/intelligence-lab/');
    if (protectedRoute && !config.key) return reply(404, { error: 'Diagnósticos não configurados no monitor.' });
    const history = url.pathname === '/monitor-api/history';
    const controller = new AbortController();
    const timeout = history ? undefined : setTimeout(() => controller.abort(), TIMEOUT_MS);
    const abort = () => { if (!res.writableEnded) controller.abort(); };
    if (!history) res.on('close', abort);
    try {
      const response = history ? await sharedHistory(path)
        : await callUpstream(path, controller.signal, protectedRoute ? { 'X-Intelligence-Lab-Key': config.key } : {});
      if (response.status !== 200) return reply(response.status, { error: `IAApi respondeu HTTP ${response.status}.` });
      let body: unknown = response.body;
      // Reduzir transferência ao navegador. Em decisions a IAApi já filtra por
      // cliente (34.2); o filtro fica como defesa, para IAApi sem o parâmetro.
      if (Array.isArray(body) && ['tags', 'decisions'].some(s => url.pathname.endsWith('/' + s))) {
        const client = url.searchParams.get('client') || config.public.client;
        body = body.filter(row => row.clienteId === client);
      }
      reply(200, body);
    } catch {
      reply(502, { error: `IAApi inacessível ou consulta excedeu ${(history ? HISTORY_TIMEOUT_MS : TIMEOUT_MS) / 1000} segundos.` });
    } finally {
      clearTimeout(timeout); res.off('close', abort);
    }
  };
}
export function monitorPlugin(env: Env): Plugin {
  const middleware = createMiddleware(env);
  return { name: 'iaapi-read-only-local',
    configureServer(server) { server.middlewares.use(middleware); },
    configurePreviewServer(server) { server.middlewares.use(middleware); },
  };
}
