import { afterEach, expect, it, vi } from 'vitest';
import { startPolling } from '../src/polling';
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
