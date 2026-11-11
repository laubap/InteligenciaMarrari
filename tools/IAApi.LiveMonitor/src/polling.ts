// Um ciclo só agenda o próximo depois de terminar; stop cancela fetches e timer.
export function startPolling(task: (signal: AbortSignal) => Promise<void>, interval: number, onError: (e: unknown) => void = () => {}) {
  const controller = new AbortController();
  let timer: ReturnType<typeof setTimeout> | undefined;
  const run = async () => {
    try { await task(controller.signal); } catch (e) { if (!controller.signal.aborted) onError(e); }
    if (!controller.signal.aborted) timer = setTimeout(run, interval);
  };
  void run();
  return () => { controller.abort(); if (timer) clearTimeout(timer); };
}
export async function mapLimited<T>(items: T[], task: (item: T) => Promise<void>, signal: AbortSignal) {
  let index = 0;
  await Promise.all(Array.from({ length: Math.min(3, items.length) }, async () => {
    while (index < items.length && !signal.aborted) await task(items[index++]);
  }));
}
