import { describe, expect, it } from 'vitest';
import { dateTimeLocalValue, isWithinTestRun, parseDateTimeLocal, restoreTestRuns, testRunDecisions } from '../src/testRuns';
import type { Decision } from '../src/types';

const makeDecision = (at: number, tag = 'PK2.TAG27'): Decision => ({
  id: `db:${at}`, tag, anomaly: false, value: 1, analyzedAt: at, receivedAt: at + 1000, source: 'persisted',
});

const start = new Date('2026-11-05T13:10:00-03:00').getTime();
const end = new Date('2026-11-05T14:35:00-03:00').getTime();
const run = { id: 'run-1', name: 'Cenário 1', startedAt: start, endedAt: end };

describe('sessões de teste no monitor', () => {
  it('inclui as duas fronteiras do intervalo e exclui dados anteriores e posteriores', () => {
    const decisions = [makeDecision(start - 1), makeDecision(start), makeDecision(end), makeDecision(end + 1)];
    expect(testRunDecisions(decisions, run, end + 10000).map(d => d.analyzedAt)).toEqual([start, end]);
  });
  it('sessão ativa termina no horário atual, não inclui decisões no futuro', () => {
    const active = { id: 'running', name: 'Cenário 1', startedAt: start };
    expect(isWithinTestRun(start + 1000, active, start + 2000)).toBe(true);
    expect(isWithinTestRun(start + 3000, active, start + 2000)).toBe(false);
  });
  it('sem sessão selecionada mantém todas as decisões', () => {
    const all = [makeDecision(start - 1), makeDecision(end + 1)];
    expect(testRunDecisions(all, undefined, end)).toEqual(all);
  });
  it('restaura somente sessões válidas do armazenamento local', () => {
    expect(restoreTestRuns([run, { ...run, id: 'bad', endedAt: start - 1 }, { name: 'sem id' }, null])).toEqual([run]);
    expect(restoreTestRuns('não é uma lista')).toEqual([]);
  });
  it('datas datetime-local respeitam o fuso local do navegador', () => {
    expect(parseDateTimeLocal(dateTimeLocalValue(start))).toBe(start);
    expect(parseDateTimeLocal('data-inválida')).toBeUndefined();
  });
});
