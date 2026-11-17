import { afterEach, expect, it, vi } from 'vitest';
import { mapLimited, startPolling } from '../src/polling';
afterEach(() => vi.useRealTimers());
it('não sobrepõe consultas lentas, retoma após erro e para sem reagendar', async () => {
  vi.useFakeTimers();
  let finish!: () => void;
  let signal!: AbortSignal;
  const task = vi.fn((s: AbortSignal) => { signal = s; return new Promise<void>(resolve => { finish = resolve; }); });
  const stop = startPolling(task, 2000);
  await vi.advanceTimersByTimeAsync(15000);
  expect(task).toHaveBeenCalledTimes(1);
  finish(); await vi.advanceTimersByTimeAsync(2000);
  expect(task).toHaveBeenCalledTimes(2);
  stop(); expect(signal.aborted).toBe(true);
  finish(); await vi.advanceTimersByTimeAsync(10000);
  expect(task).toHaveBeenCalledTimes(2);
});
it('continua consultando após falha transitória', async () => {
  vi.useFakeTimers();
  const task = vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValue(undefined);
  const error = vi.fn(); const stop = startPolling(task, 2000, error);
  await vi.advanceTimersByTimeAsync(2000);
  expect(error).toHaveBeenCalledTimes(1); expect(task).toHaveBeenCalledTimes(2); stop();
});
it('histórico (34.4): 12 tags com busca lenta nunca passam de 3 simultâneas nem repetem tag em curso', async () => {
  vi.useFakeTimers();
  const tags = Array.from({ length: 12 }, (_, i) => `PK2.TAG${i + 1}`);
  const running = new Set<string>(); let max = 0; let repeated = 0; let calls = 0;
  const stop = startPolling(signal => mapLimited(tags, async tag => {
    if (running.has(tag)) repeated++;
    running.add(tag); calls++; max = Math.max(max, running.size);
    await new Promise(resolve => setTimeout(resolve, 36600)); // média de 16/11 (P42)
    running.delete(tag);
  }, signal), 60000);
  await vi.advanceTimersByTimeAsync(10 * 60000);
  stop();
  expect(max).toBe(3); expect(repeated).toBe(0); expect(calls).toBeGreaterThan(12);
});
