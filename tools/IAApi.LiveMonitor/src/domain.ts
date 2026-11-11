import type { Decision, Diagnostic, Reading, State } from './types';

export function timestamp(value: unknown, offset: string): number | undefined {
  if (typeof value !== 'string' || !/^\d{4}-\d\d-\d\dT/.test(value)) return undefined;
  const time = Date.parse(/[zZ]$|[+-]\d\d:\d\d$/.test(value) ? value : value + offset);
  return Number.isFinite(time) ? time : undefined;
}
const finite = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v);
export function parseDecisions(input: unknown, client: string, offset: string, receivedAt: number): Decision[] {
  if (!Array.isArray(input)) throw new Error('Contrato inválido de decisões.');
  return input.filter(x => x.clienteId === client).map(x => {
    const at = timestamp(x.dataDeteccao, offset);
    if (!Number.isSafeInteger(x.id) || typeof x.tagName !== 'string' || typeof x.ehAnomalia !== 'boolean' || at === undefined || !finite(x.valor))
      throw new Error('Decisão sem identificação, timestamp, valor ou classificação válida.');
    return { id: `db:${x.id}`, tag: x.tagName, anomaly: x.ehAnomalia, value: x.valor,
      analyzedAt: at, receivedAt, source: 'persisted' as const, score: finite(x.score) ? x.score : undefined };
  });
}
export function parseHistory(input: unknown, offset: string): Reading[] {
  const data = input as { historico?: unknown };
  if (!Array.isArray(data?.historico)) throw new Error('Contrato inválido de histórico.');
  const readings = new Map<number, Reading>();
  for (const item of data.historico) {
    const at = timestamp(item.dataHora, offset);
    if (at === undefined || !finite(item.valor)) throw new Error('Leitura inválida no histórico.');
    readings.set(at, { at, value: item.valor });
  }
  return [...readings.values()].sort((a, b) => a.at - b.at);
}
export function fromDiagnostic(d: Diagnostic, client: string, tag: string, receivedAt: number, sourceOffset = '-03:00'): Decision | undefined {
  const analyzedAt = timestamp(d.completedAtUtc, '+00:00');
  const knownUtc = timestamp(d.readingTimestampUtc, '+00:00');
  // Sem fuso original não há UTC conhecido. O offset é apenas uma hipótese de
  // apresentação explicitamente configurada, nunca gravada como UTC de origem.
  const assumedLocal = knownUtc === undefined && d.readingTimestampKind === 'Unspecified'
    ? timestamp(d.readingTimestampOriginal, sourceOffset) : undefined;
  const readingAt = knownUtc ?? assumedLocal;
  // Partial significa instrumentação parcial, não decisão pendente. FinalAnomaly deve existir.
  if (d.clienteId !== client || d.tagName !== tag || !d.context?.diagnosticId ||
      typeof d.runtimeDecision?.finalAnomaly !== 'boolean' || analyzedAt === undefined || readingAt === undefined || !finite(d.readingValue)) return;
  return { id: `lab:${d.context.diagnosticId}`, tag, anomaly: d.runtimeDecision.finalAnomaly,
    value: d.readingValue, analyzedAt, readingAt,
    readingAtBasis: knownUtc !== undefined ? 'known-utc' : 'configured-offset',
    receivedAt, source: 'diagnostic',
    score: finite(d.ensemble?.normalizedScore) ? d.ensemble.normalizedScore : undefined,
    threshold: finite(d.ensemble?.threshold) ? d.ensemble.threshold : undefined,
    method: d.runtimeDecision.source, detail: d };
}
export function mergeDecisions(previous: Decision[], incoming: Decision[], max = 1000): Decision[] {
  const result = new Map(previous.map(d => [d.id, d]));
  for (const d of incoming) if (!result.has(d.id)) result.set(d.id, d);
  return [...result.values()].sort((a, b) => b.analyzedAt - a.analyzedAt || b.id.localeCompare(a.id, undefined, { numeric: true })).slice(0, max);
}
export function latestDecision(db: Decision[], diagnostics: Decision[], tag: string): Decision | undefined {
  return [...db, ...diagnostics].filter(d => d.tag === tag).sort((a, b) => b.analyzedAt - a.analyzedAt || (a.source === 'diagnostic' ? -1 : 1))[0];
}
export function readingState(decision: Decision | undefined, reading: Reading | undefined, unavailable: boolean, now: number, staleMs: number): State {
  if (unavailable) return 'INDISPONÍVEL';
  if ((reading && now - reading.at > staleMs) || (decision && now - decision.analyzedAt > staleMs)) return 'DESATUALIZADO';
  if (!decision || !reading || decision.readingAt === undefined) return 'AGUARDANDO';
  // Igualdade de timestamp E valor. Não presumir correlação por proximidade temporal.
  if (decision.readingAt !== reading.at || decision.value !== reading.value) return 'AGUARDANDO';
  return decision.anomaly ? 'ANOMALIA' : 'NORMAL';
}
export function latestReading(history: Reading[], diagnostics: Decision[]): Reading | undefined {
  const readings = [...history, ...diagnostics.filter(d => d.readingAt !== undefined).map(d => ({ at: d.readingAt!, value: d.value }))];
  return readings.sort((a, b) => b.at - a.at)[0];
}
export function newAnomalies(previous: Decision[], incoming: Decision[], initialized: boolean): Decision[] {
  if (!initialized) return [];
  const seen = new Set(previous.map(d => d.id));
  const newest = previous.reduce((max, d) => Math.max(max, d.analyzedAt), 0);
  return incoming.filter(d => d.anomaly && !seen.has(d.id) && d.analyzedAt >= newest);
}

/** Tempo decorrido desde um carimbo, sem converter ausência de dados em "zero". */
export function ageLabel(at: number | undefined, now: number): string {
  if (at === undefined) return 'sem horário';
  const elapsed = now - at;
  if (elapsed < -5000) return 'relógios divergentes';
  if (elapsed < 60000) return `${Math.max(0, Math.round(elapsed / 1000))} s atrás`;
  if (elapsed < 3600000) return `${Math.floor(elapsed / 60000)} min atrás`;
  return `${Math.floor(elapsed / 3600000)} h ${Math.floor((elapsed % 3600000) / 60000)} min atrás`;
}
