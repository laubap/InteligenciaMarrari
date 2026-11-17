import { describe, expect, it } from 'vitest';
import { ageLabel, decisionsSince, fromDiagnostic, latestDecision, latestReading, mergeDecisions, newAnomalies, parseDecisions, parseHistory, readingState, timestamp } from '../src/domain';
import type { Decision, Diagnostic } from '../src/types';

const now = Date.parse('2026-11-04T12:00:05Z');
const decision = (overrides: Partial<Decision> = {}): Decision => ({ id: 'db:1', tag: 'TEST', anomaly: false, value: 10, analyzedAt: now - 1000, readingAt: now - 2000, receivedAt: now, source: 'persisted', ...overrides });
describe('contratos e semântica operacional', () => {
  it('interpreta offset explícito e fuso configurado sem inventar timestamp', () => {
    expect(timestamp('2026-11-04T09:00:05', '-03:00')).toBe(now);
    expect(timestamp('2026-11-04T12:00:05Z', '-03:00')).toBe(now);
    expect(timestamp(null, '-03:00')).toBeUndefined();
  });
  it('ausência de decisão nunca vira normal', () => {
    expect(readingState(undefined, { at: now, value: 10 }, false, now, 180000)).toBe('AGUARDANDO');
    expect(parseDecisions([], 'PK2', '-03:00', now)).toEqual([]);
  });
  it('normal e anomalia exigem correlação exata de leitura e decisão', () => {
    const d = decision(), r = { at: d.readingAt!, value: d.value };
    expect(readingState(d, r, false, now, 180000)).toBe('NORMAL');
    expect(readingState({ ...d, anomaly: true }, r, false, now, 180000)).toBe('ANOMALIA');
    expect(readingState(d, { ...r, at: now }, false, now, 180000)).toBe('AGUARDANDO');
    expect(readingState(d, { ...r, value: 11 }, false, now, 180000)).toBe('AGUARDANDO');
  });
  it('decisão persistida sem timestamp da leitura não confirma a leitura atual', () => {
    const [d] = parseDecisions([{ id: 1, clienteId: 'PK2', tagName: 'TEST', ehAnomalia: false, valor: 10, score: 0, dataDeteccao: '2026-11-04T12:00:04Z' }], 'PK2', '-03:00', now);
    expect(d.anomaly).toBe(false); expect(d.score).toBe(0); expect(d.readingAt).toBeUndefined();
    expect(readingState(d, { at: now - 2000, value: 10 }, false, now, 180000)).toBe('AGUARDANDO');
  });
  it('separa indisponibilidade, dados antigos e análise pendente', () => {
    expect(readingState(decision(), undefined, true, now, 180000)).toBe('INDISPONÍVEL');
    expect(readingState(decision({ analyzedAt: now - 200000 }), undefined, false, now, 180000)).toBe('DESATUALIZADO');
    expect(readingState(undefined, { at: now - 200000, value: 1 }, false, now, 180000)).toBe('DESATUALIZADO');
  });
  it('rejeita decisões com classificação ausente ou falsa string', () => {
    for (const value of [null, undefined, 'false']) expect(() => parseDecisions([{ id: 1, clienteId: 'PK2', tagName: 'TEST', ehAnomalia: value, valor: 10, dataDeteccao: '2026-11-04T12:00:04Z' }], 'PK2', '-03:00', now)).toThrow();
  });
  it('filtra cliente e mantém normais reais', () => {
    expect(parseDecisions([{ clienteId: 'OUTRO' }], 'PK2', '-03:00', now)).toEqual([]);
  });
  it('usa a decisão final, mesmo se o ensemble divergir e a telemetria for parcial', () => {
    const d = { context: { diagnosticId: 'diag' }, clienteId: 'PK2', tagName: 'TEST', readingTimestampUtc: '2026-11-04T12:00:03Z', readingValue: 10, completedAtUtc: '2026-11-04T12:00:04Z', runtimeDecision: { finalAnomaly: false, source: 'CandidateMismatch' }, ensemble: { legacyAnomaly: true, normalizedScore: 0.9, threshold: 0.5 }, completeness: { status: 2 } } as Diagnostic;
    expect(fromDiagnostic(d, 'PK2', 'TEST', now)?.anomaly).toBe(false);
    expect(fromDiagnostic({ ...d, runtimeDecision: null }, 'PK2', 'TEST', now)).toBeUndefined();
    expect(fromDiagnostic({ ...d, completedAtUtc: null }, 'PK2', 'TEST', now)).toBeUndefined();
    expect(fromDiagnostic(d, 'OUTRO', 'TEST', now)).toBeUndefined();
  });
  it('deduplica IDs e preserva horário do primeiro recebimento', () => {
    const first = decision();
    const merged = mergeDecisions([first], [decision({ receivedAt: now + 10000 }), decision({ id: 'db:2', analyzedAt: now + 500 })]);
    expect(merged).toHaveLength(2); expect(merged[1].receivedAt).toBe(now); expect(merged[0].id).toBe('db:2');
  });
  it('desde (34.2): volta à hora sem fuso da IAApi, com folga para trás', () => {
    const rows = [{ id: 1, tagName: 'A', ehAnomalia: false, valor: 1, clienteId: 'PK2', dataDeteccao: '2026-11-16T16:27:17.123' },
      { id: 2, tagName: 'A', ehAnomalia: false, valor: 1, clienteId: 'PK2', dataDeteccao: '2026-11-16T16:20:00' }];
    const parsed = parseDecisions(rows, 'PK2', '-03:00', now);
    expect(decisionsSince(parsed, '-03:00', 0)).toBe('2026-11-16T16:27:17.123');
    expect(decisionsSince(parsed, '-03:00')).toBe('2026-11-16T16:22:17.123');
    expect(decisionsSince(parseDecisions(rows, 'PK2', '+05:30', now), '+05:30', 0)).toBe('2026-11-16T16:27:17.123');
    expect(decisionsSince([], '-03:00')).toBeUndefined();
    expect(decisionsSince(parsed, 'inválido')).toBeUndefined();
  });
  it('consulta incremental (desde) acumula as mesmas decisões e alertas que a consulta completa', () => {
    // Servidor simulado: linhas chegam ao longo do tempo, algumas gravadas fora de ordem.
    const offset = '-03:00';
    const base = Date.parse('2026-11-16T19:00:00Z');
    const raw = (i: number, atrasoMs = 0) => ({ id: i, clienteId: 'PK2', tagName: `T${i % 4}`, ehAnomalia: i % 7 === 0, valor: i, score: i / 10,
      dataDeteccao: new Date(base + i * 5000 - atrasoMs - 3 * 3600000).toISOString().slice(0, 23) });
    const gravadas: ReturnType<typeof raw>[] = [];
    const servidor = (since?: string) => gravadas.filter(r => since === undefined || timestamp(r.dataDeteccao, offset)! >= timestamp(since, offset)!)
      .sort((a, b) => b.dataDeteccao.localeCompare(a.dataDeteccao));
    let completa: Decision[] = [], incremental: Decision[] = [];
    let alertasCompleta: Decision[] = [], alertasIncremental: Decision[] = [];
    for (let poll = 0; poll < 1100; poll++) {
      gravadas.push(raw(gravadas.length + 1));
      if (poll % 50 === 10) gravadas.push(raw(gravadas.length + 1, 60000)); // gravada 1 min "atrasada"
      const init = poll > 0;
      const tudo = parseDecisions(servidor(), 'PK2', offset, now);
      alertasCompleta = mergeDecisions(alertasCompleta, newAnomalies(completa, tudo, init), 20);
      completa = mergeDecisions(completa, tudo, Math.max(1000, tudo.length));
      const novas = parseDecisions(servidor(init ? decisionsSince(incremental, offset) : undefined), 'PK2', offset, now);
      alertasIncremental = mergeDecisions(alertasIncremental, newAnomalies(incremental, novas, init), 20);
      incremental = mergeDecisions(incremental, novas, Math.max(1000, incremental.length + novas.length));
    }
    expect(completa.length).toBeGreaterThan(1000); // acima do limite padrão de 1.000 do mergeDecisions
    expect(incremental).toEqual(completa);
    expect(alertasIncremental).toEqual(alertasCompleta);
  });
  it('não alerta no carregamento inicial, em repetição ou no preenchimento de histórico', () => {
    const d = decision({ anomaly: true });
    expect(newAnomalies([], [d], false)).toEqual([]);
    expect(newAnomalies([d], [d], true)).toEqual([]);
    expect(newAnomalies([d], [decision({ id: 'old', anomaly: true, analyzedAt: now - 90000 })], true)).toEqual([]);
    expect(newAnomalies([d], [decision({ id: 'new', anomaly: true, analyzedAt: now + 1000 })], true)).toHaveLength(1);
  });
  it('uma análise pública mais nova prevalece sobre um diagnóstico antigo', () => {
    const d = decision({ readingAt: undefined });
    expect(latestDecision([d], [decision({ id: 'diag', source: 'diagnostic', analyzedAt: now - 5000 })], 'TEST')).toBe(d);
  });
  it('ordena e deduplica leituras sem descartar valores zero', () => {
    const r = parseHistory({ historico: [{ dataHora: '2026-11-04T12:00:04Z', valor: 0 }, { dataHora: '2026-11-04T12:00:03Z', valor: 10 }, { dataHora: '2026-11-04T12:00:04Z', valor: 0 }] }, '-03:00');
    expect(r).toHaveLength(2); expect(r[1].value).toBe(0);
    expect(latestReading(r, [decision({ readingAt: now })])?.at).toBe(now);
  });
});

describe('observabilidade dos tempos no monitor', () => {
  it('mostra claramente a idade de um dado e diferencia ausência e relógio divergente', () => {
    expect(ageLabel(undefined, now)).toBe('sem horário');
    expect(ageLabel(now - 2000, now)).toBe('2 s atrás');
    expect(ageLabel(now - 9 * 60 * 1000, now)).toBe('9 min atrás');
    expect(ageLabel(now + 6000, now)).toBe('relógios divergentes');
  });
  it('aceita diagnóstico com horário industrial Unspecified sem inventar UTC', () => {
    const d: Diagnostic = {
      context: { diagnosticId: 'c', receivedAtUtc: '2026-11-04T12:00:05Z' },
      clienteId: 'PK2', tagName: 'TEST', readingTimestampUtc: null,
      readingTimestampOriginal: '2026-11-04T09:00:03', readingTimestampKind: 'Unspecified', readingValue: 10,
      completedAtUtc: '2026-11-04T12:00:04Z', completeness: { status: 2, missingSections: ['ReadingTimestampUtc'] },
      runtimeDecision: { finalAnomaly: false, source: 'Ensemble', reasons: [] },
      detectors: [],
    };
    const parsed = fromDiagnostic(d, 'PK2', 'TEST', now, '-03:00');
    expect(parsed?.anomaly).toBe(false);
    expect(parsed?.readingAt).toBe(Date.parse('2026-11-04T12:00:03Z'));
    expect(parsed?.readingAtBasis).toBe('configured-offset');
    expect(d.readingTimestampUtc).toBeNull();
  });
});
