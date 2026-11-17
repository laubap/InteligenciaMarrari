import { afterEach, describe, expect, it, vi } from 'vitest';
import { EventEmitter } from 'node:events';
import type { IncomingMessage, ServerResponse } from 'node:http';
import { createMiddleware, HISTORY_TIMEOUT_MS, settings, TIMEOUT_MS, upstreamPath } from '../server/proxy';

afterEach(() => vi.unstubAllGlobals());
const key = 'test-only-secret-not-a-real-api-key';
async function request(path: string, method = 'GET', headers = {}) {
  const req = { url: path, method, headers: { host: '127.0.0.1:5173', ...headers }, socket: { remoteAddress: '127.0.0.1' } } as unknown as IncomingMessage;
  let status = 0, body = '';
  const res = Object.assign(new EventEmitter(), { writableEnded: false, writeHead: (s: number) => { status = s; }, end: (s: string) => { body = s; } }) as unknown as ServerResponse;
  await createMiddleware({ IAAPI_DIAGNOSTICS_KEY: key })(req, res, () => {});
  return { status, body };
}
describe('proxy local somente leitura', () => {
  it('não expõe chave na configuração pública', async () => {
    const response = await request('/monitor-api/config');
    expect(response.status).toBe(200); expect(response.body).not.toContain(key);
    expect(JSON.parse(response.body).diagnosticsConfigured).toBe(true);
  });
  it('recusa rotas arbitrárias, escritas e origem externa antes de chamar API', async () => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    expect((await request('/monitor-api/decisions', 'POST')).status).toBe(405);
    expect((await request('/monitor-api/anomalias/tag')).status).toBe(404);
    expect((await request('/monitor-api/decisions', 'GET', { origin: 'https://example.com' })).status).toBe(403);
    expect((await request('/monitor-api/decisions', 'GET', { host: 'evil.example:5173' })).status).toBe(403);
    expect(fetch).not.toHaveBeenCalled();
  });
  it('permite somente destinos locais para a chave de diagnóstico', () => {
    expect(() => settings({ IAAPI_URL: 'https://example.com', IAAPI_DIAGNOSTICS_KEY: key })).toThrow();
  });
  it('encoda nomes de tag sem permitir troca de rota', () => {
    expect(upstreamPath(new URL('http://local/monitor-api/history?client=PK2&tag=A%2FB%20%26'))).toBe('/api/tags/historico/PK2/A%2FB%20%26?horas=2');
  });
  it('decisões: filtra cliente na IAApi e repassa só um desde bem formado (34.2)', () => {
    const path = (q: string) => upstreamPath(new URL('http://local/monitor-api/decisions' + q));
    expect(path('?client=PK2')).toBe('/api/anomalias?clienteId=PK2');
    expect(path('')).toBe('/api/anomalias?clienteId=PK2');
    expect(path('?client=A%26B')).toBe('/api/anomalias?clienteId=A%26B');
    expect(path('?client=PK2&since=2026-11-16T16%3A27%3A17.123')).toBe('/api/anomalias?clienteId=PK2&desde=2026-11-16T16%3A27%3A17.123');
    expect(path('?client=PK2&since=2026-11-16T16:27:17')).toBe('/api/anomalias?clienteId=PK2&desde=2026-11-16T16%3A27%3A17');
    for (const bad of ['2026-11-16', '2026-11-16T16:27:17Z', '2026-11-16T16:27:17&limite=1', 'x'])
      expect(path('?client=PK2&since=' + encodeURIComponent(bad))).toBeNull();
  });
  it('decisões: chama a IAApi com clienteId e desde', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => [] }); vi.stubGlobal('fetch', fetch);
    await request('/monitor-api/decisions?client=PK2&since=2026-11-16T16%3A22%3A17.000');
    const called = new URL(fetch.mock.calls[0][0]);
    expect(called.pathname).toBe('/api/anomalias');
    expect(called.searchParams.get('clienteId')).toBe('PK2');
    expect(called.searchParams.get('desde')).toBe('2026-11-16T16:22:17.000');
    expect(called.searchParams.has('limite')).toBe(false);
  });
  it('transmite apenas GET, filtra cliente e não transmite chave em rota pública', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => [{ clienteId: 'PK2', id: 1 }, { clienteId: 'OTHER', id: 2 }] }); vi.stubGlobal('fetch', fetch);
    const result = await request('/monitor-api/decisions?client=PK2');
    expect(JSON.parse(result.body)).toEqual([{ clienteId: 'PK2', id: 1 }]);
    expect(fetch.mock.calls[0][1].method).toBe('GET'); expect(fetch.mock.calls[0][1].headers).toEqual({});
  });
  it('insere chave só no servidor e não replica erros sensíveis', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: false, status: 401 }); vi.stubGlobal('fetch', fetch);
    const result = await request('/monitor-api/diagnostics?tag=TEST');
    expect(fetch.mock.calls[0][1].headers['X-Intelligence-Lab-Key']).toBe(key);
    expect(result.status).toBe(401); expect(result.body).not.toContain(key);
  });
  it('transforma indisponibilidade em erro explícito', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('private details')));
    const result = await request('/monitor-api/decisions');
    expect(result.status).toBe(502); expect(result.body).not.toContain('private details');
  });
});

// 34.4 (P42): o histórico não é abandonado enquanto a IAApi ainda busca no Bridge.
describe('histórico sem buscas abandonadas (34.4)', () => {
  afterEach(() => vi.useRealTimers());
  const open = (middleware: ReturnType<typeof createMiddleware>, path: string) => {
    const req = { url: path, method: 'GET', headers: { host: '127.0.0.1:5173' }, socket: { remoteAddress: '127.0.0.1' } } as unknown as IncomingMessage;
    const state = { status: 0, body: '' };
    const res = Object.assign(new EventEmitter(), { writableEnded: false,
      writeHead: (s: number) => { state.status = s; }, end: (s: string) => { state.body = s; res.writableEnded = true; } });
    const done = middleware(req, res as unknown as ServerResponse, () => {});
    return { state, done, close: () => res.emit('close') };
  };
  const pendingFetch = () => {
    const calls: { url: string; signal: AbortSignal; resolve: (body: unknown) => void }[] = [];
    const fetch = vi.fn((url: URL, init: RequestInit) => new Promise((resolve, reject) => {
      init.signal!.addEventListener('abort', () => reject(new Error('aborted')));
      calls.push({ url: String(url), signal: init.signal!, resolve: body => resolve({ ok: true, json: async () => body }) });
    }));
    vi.stubGlobal('fetch', fetch);
    return calls;
  };
  it('padrão do intervalo de histórico é 60 s, e o mínimo continua 5 s', () => {
    expect(settings({}).public.historyMs).toBe(60000);
    expect(settings({ MONITOR_HISTORY_MS: '15000' }).public.historyMs).toBe(15000);
    expect(settings({ MONITOR_HISTORY_MS: '1000' }).public.historyMs).toBe(5000);
  });
  it('espera a IAApi além dos 12 s e só desiste depois dela (2 × 60 s + margem)', async () => {
    vi.useFakeTimers();
    const calls = pendingFetch();
    const history = open(createMiddleware({}), '/monitor-api/history?client=PK2&tag=PK2.TAG31');
    await vi.advanceTimersByTimeAsync(120000);
    expect(calls[0].signal.aborted).toBe(false);
    expect(HISTORY_TIMEOUT_MS).toBeGreaterThanOrEqual(2 * 60000);
    await vi.advanceTimersByTimeAsync(HISTORY_TIMEOUT_MS - 120000);
    await history.done;
    expect(calls[0].signal.aborted).toBe(true);
    expect(history.state.status).toBe(502);
    expect(JSON.parse(history.state.body).error).toContain('125 segundos');
  });
  it('as outras rotas continuam desistindo em 12 s', async () => {
    vi.useFakeTimers();
    const calls = pendingFetch();
    const decisions = open(createMiddleware({}), '/monitor-api/decisions?client=PK2');
    await vi.advanceTimersByTimeAsync(TIMEOUT_MS);
    await decisions.done;
    expect(calls[0].signal.aborted).toBe(true);
    expect(JSON.parse(decisions.state.body).error).toContain('12 segundos');
  });
  it('uma busca por tag: pedido repetido espera a mesma resposta, sem nova chamada à IAApi', async () => {
    const calls = pendingFetch();
    const middleware = createMiddleware({});
    const first = open(middleware, '/monitor-api/history?client=PK2&tag=PK2.TAG31');
    const second = open(middleware, '/monitor-api/history?client=PK2&tag=PK2.TAG31');
    const other = open(middleware, '/monitor-api/history?client=PK2&tag=PK2.TAG33');
    await Promise.resolve();
    expect(calls.map(c => new URL(c.url).pathname)).toEqual(['/api/tags/historico/PK2/PK2.TAG31', '/api/tags/historico/PK2/PK2.TAG33']);
    calls[0].resolve({ tagName: 'PK2.TAG31', historico: [] }); calls[1].resolve({ tagName: 'PK2.TAG33', historico: [] });
    await Promise.all([first.done, second.done, other.done]);
    expect([first.state.status, second.state.status, other.state.status]).toEqual([200, 200, 200]);
    expect(first.state.body).toBe(second.state.body);
    // Terminada a busca, o próximo ciclo chama a IAApi de novo.
    const next = open(middleware, '/monitor-api/history?client=PK2&tag=PK2.TAG31');
    await Promise.resolve();
    expect(calls).toHaveLength(3);
    calls[2].resolve({ historico: [] }); await next.done;
  });
  it('fechar a página não cancela a busca na IAApi; o pedido seguinte da mesma tag se junta a ela', async () => {
    const calls = pendingFetch();
    const middleware = createMiddleware({});
    const abandoned = open(middleware, '/monitor-api/history?client=PK2&tag=PK2.TAG31');
    await Promise.resolve();
    abandoned.close();
    expect(calls[0].signal.aborted).toBe(false);
    const again = open(middleware, '/monitor-api/history?client=PK2&tag=PK2.TAG31');
    await Promise.resolve();
    expect(calls).toHaveLength(1);
    calls[0].resolve({ historico: [] });
    await Promise.all([abandoned.done, again.done]);
    expect(again.state.status).toBe(200);
  });
  it('decisões continuam canceladas quando a página fecha', async () => {
    const calls = pendingFetch();
    const decisions = open(createMiddleware({}), '/monitor-api/decisions?client=PK2');
    await Promise.resolve();
    decisions.close();
    await decisions.done;
    expect(calls[0].signal.aborted).toBe(true);
  });
});
