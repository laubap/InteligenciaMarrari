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
    historyMs: integer(env.MONITOR_HISTORY_MS, 15000, 5000),
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
    case '/monitor-api/decisions': return '/api/anomalias';
    case '/monitor-api/history': return tag ? `/api/tags/historico/${encodeURIComponent(client)}/${encodeURIComponent(tag)}?horas=2` : null;
    case '/monitor-api/diagnostics': return tag ? `/api/intelligence-lab/diagnostics?limit=100&clienteId=${encodeURIComponent(client)}&tagName=${encodeURIComponent(tag)}` : null;
    case '/monitor-api/shadow': return `/api/intelligence-lab/diagnostics/multivariate-shadow?limit=100&clienteId=${encodeURIComponent(client)}`;
    default: {
      const match = url.pathname.match(/^\/monitor-api\/(diagnostic|shadow)\/([a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12})$/i);
      return match ? `/api/intelligence-lab/diagnostics/${match[1] === 'shadow' ? 'multivariate-shadow/' : ''}${match[2]}` : null;
    }
  }
}

export function createMiddleware(env: Env) {
  const config = settings(env);
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
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 12000);
    const abort = () => { if (!res.writableEnded) controller.abort(); };
    res.on('close', abort);
    try {
      const response = await fetch(new URL(path, config.target), {
        method: 'GET', redirect: 'error', signal: controller.signal,
        headers: protectedRoute ? { 'X-Intelligence-Lab-Key': config.key } : {},
      });
      if (!response.ok) return reply(response.status, { error: `IAApi respondeu HTTP ${response.status}.` });
      let body: unknown = await response.json();
      // Reduzir transferência ao navegador; o endpoint original não tem paginação.
      if (Array.isArray(body) && ['tags', 'decisions'].some(s => url.pathname.endsWith('/' + s))) {
        const client = url.searchParams.get('client') || config.public.client;
        body = body.filter(row => row.clienteId === client);
      }
      reply(200, body);
    } catch {
      reply(502, { error: 'IAApi inacessível ou consulta excedeu 12 segundos.' });
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
