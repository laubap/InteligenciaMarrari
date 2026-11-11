import { afterEach, describe, expect, it, vi } from 'vitest';
import { EventEmitter } from 'node:events';
import type { IncomingMessage, ServerResponse } from 'node:http';
import { createMiddleware, settings, upstreamPath } from '../server/proxy';

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
