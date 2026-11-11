import type { Decision } from './types';

/** A sessão de teste marca um intervalo de registros; não dispara nem reinicia o CLP. */
export interface TestRun {
  id: string;
  name: string;
  startedAt: number;
  endedAt?: number;
}

const validTime = (value: unknown): value is number =>
  typeof value === 'number' && Number.isFinite(value) && value > 0;

export function restoreTestRuns(input: unknown): TestRun[] {
  if (!Array.isArray(input)) return [];
  return input.filter((x): x is TestRun => {
    if (!x || typeof x !== 'object') return false;
    const run = x as Partial<TestRun>;
    return typeof run.id === 'string' && !!run.id && typeof run.name === 'string' &&
      validTime(run.startedAt) && (run.endedAt === undefined ||
        (validTime(run.endedAt) && run.endedAt >= run.startedAt));
  }).slice(-25);
}

export function isWithinTestRun(at: number, run: TestRun, now: number): boolean {
  const end = run.endedAt ?? now;
  return Number.isFinite(at) && at >= run.startedAt && at <= end;
}

export function testRunDecisions(decisions: Decision[], run: TestRun | undefined, now: number): Decision[] {
  if (!run) return decisions;
  return decisions.filter(d => isWithinTestRun(d.analyzedAt, run, now));
}

/** datetime-local usa o fuso do navegador; não converte o registro da API. */
export function dateTimeLocalValue(at: number): string {
  const d = new Date(at);
  if (!Number.isFinite(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function parseDateTimeLocal(input: string): number | undefined {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(input)) return undefined;
  const date = new Date(input);
  return Number.isFinite(date.getTime()) ? date.getTime() : undefined;
}
