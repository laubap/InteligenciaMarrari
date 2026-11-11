import type { Config, Diagnostic, DiagnosticSummary, Shadow, Tag } from './types';

export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch('/monitor-api/' + path, { signal, cache: 'no-store' });
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || `HTTP ${response.status}`);
  return data as T;
}
const query = (client: string, tag?: string) => new URLSearchParams({ client, ...(tag ? { tag } : {}) }).toString();
export const api = {
  config: () => getJson<Config>('config'),
  tags: (client: string, signal?: AbortSignal) => getJson<Tag[]>('tags?' + query(client), signal),
  metadata: (client: string, signal?: AbortSignal) => getJson<{ fullName: string; name: string; descricao: string; unidade: string }[]>('metadata?' + query(client), signal),
  decisions: (client: string, signal: AbortSignal) => getJson<unknown>('decisions?' + query(client), signal),
  history: (client: string, tag: string, signal: AbortSignal) => getJson<unknown>('history?' + query(client, tag), signal),
  diagnostics: (client: string, tag: string, signal: AbortSignal) => getJson<DiagnosticSummary[]>('diagnostics?' + query(client, tag), signal),
  diagnostic: (id: string, signal: AbortSignal) => getJson<Diagnostic>('diagnostic/' + encodeURIComponent(id), signal),
  shadow: (client: string, signal: AbortSignal) => getJson<Shadow[]>('shadow?' + query(client), signal),
  shadowDetail: (id: string, signal: AbortSignal) => getJson<Shadow>('shadow/' + encodeURIComponent(id), signal),
};
