import { useEffect, useRef, useState } from 'react';
import { api } from './api';
import { fromDiagnostic, mergeDecisions, newAnomalies, parseDecisions, parseHistory } from './domain';
import { mapLimited, startPolling } from './polling';
import type { Config, Decision, Shadow, Snapshot } from './types';

const empty = (): Snapshot => ({ decisions: [], tags: {}, shadow: [], revision: 0 });
const message = (e: unknown) => e instanceof Error ? e.message : 'Falha de consulta';
export function useMonitor(config: Config, selected: string[], interval: number) {
  const [snapshot, setSnapshot] = useState<Snapshot>(empty);
  const [alerts, setAlerts] = useState<Decision[]>([]);
  const data = useRef<Snapshot>(empty());
  const initialized = useRef(false);
  const selection = selected.join('\u0000');
  useEffect(() => {
    const publish = (signal: AbortSignal) => {
      if (!signal.aborted) setSnapshot({ ...data.current, tags: { ...data.current.tags }, revision: ++data.current.revision });
    };
    const stopDecisions = startPolling(async signal => {
      try {
        const raw = await api.decisions(config.client, signal);
        if (signal.aborted) return;
        const now = Date.now();
        const incoming = parseDecisions(raw, config.client, config.sourceOffset, now);
        const fresh = newAnomalies(data.current.decisions, incoming, initialized.current);
        if (fresh.length) setAlerts(old => mergeDecisions(old, fresh, 20));
        data.current.decisions = mergeDecisions(data.current.decisions, incoming, Math.max(1000, incoming.length));
        data.current.decisionsError = undefined;
        data.current.decisionsReceivedAt = now;
        data.current.lastResponseAt = now;
        initialized.current = true;
      } catch (e) { if (!signal.aborted) data.current.decisionsError = message(e); }
      publish(signal);
    }, interval);
    return stopDecisions;
  }, [config, interval]);
  useEffect(() => {
    const tags = selection ? selection.split('\u0000') : [];
    const publish = (signal: AbortSignal) => {
      if (!signal.aborted) setSnapshot({ ...data.current, tags: { ...data.current.tags }, revision: ++data.current.revision });
    };
    const entry = (tag: string) => data.current.tags[tag] ||= { readings: [], diagnostics: [] };
    // Histórico tem seu próprio ciclo: consultas lentas ao Bridge não atrasam decisões.
    const stopHistory = startPolling(signal => mapLimited(tags, async tag => {
      try {
        const raw = await api.history(config.client, tag, signal);
        if (signal.aborted) return;
        entry(tag).readings = parseHistory(raw, config.sourceOffset);
        entry(tag).historyError = undefined; entry(tag).historyReceivedAt = Date.now();
      } catch (e) { if (!signal.aborted) entry(tag).historyError = message(e); }
      publish(signal);
    }, signal), config.historyMs);
    const details = new Map<string, Decision | null>();
    const shadows = new Map<string, Shadow>();
    const stopLab = config.diagnosticsConfigured ? startPolling(async signal => {
      await mapLimited(tags, async tag => {
        try {
          const summaries = await api.diagnostics(config.client, tag, signal);
          if (!Array.isArray(summaries)) throw new Error('Contrato inválido de diagnósticos.');
          for (const summary of summaries) {
            if (signal.aborted) return;
            if (!details.has(summary.diagnosticId)) {
              const d = await api.diagnostic(summary.diagnosticId, signal);
              if (signal.aborted) return;
              details.set(summary.diagnosticId, fromDiagnostic(d, config.client, tag, Date.now(), config.sourceOffset) || null);
            }
          }
          const results = summaries.map(s => details.get(s.diagnosticId)).filter((d): d is Decision => !!d);
          entry(tag).diagnostics = mergeDecisions(entry(tag).diagnostics, results, 200);
          entry(tag).diagnosticError = undefined;
          if (details.size > 1000) { const keep = new Set(summaries.map(s => s.diagnosticId)); for (const id of details.keys()) if (!keep.has(id)) details.delete(id); }
        } catch (e) { if (!signal.aborted) entry(tag).diagnosticError = message(e); }
        publish(signal);
      }, signal);
      if (!tags.length || signal.aborted) return;
      try {
        const summaries = await api.shadow(config.client, signal);
        const groups = new Map<string, Shadow>();
        for (const s of summaries) if (!groups.has(s.groupName)) groups.set(s.groupName, s);
        for (const s of groups.values()) {
          if (signal.aborted) return;
          if (!shadows.has(s.diagnosticId)) shadows.set(s.diagnosticId, await api.shadowDetail(s.diagnosticId, signal));
        }
        if (signal.aborted) return;
        data.current.shadow = [...groups.values()].map(s => shadows.get(s.diagnosticId)!);
        data.current.shadowError = undefined;
        if (shadows.size > 200) { const keep = new Set([...groups.values()].map(s => s.diagnosticId)); for (const id of shadows.keys()) if (!keep.has(id)) shadows.delete(id); }
      } catch (e) { if (!signal.aborted) data.current.shadowError = message(e); }
      publish(signal);
    }, interval) : () => {};
    return () => { stopHistory(); stopLab(); };
  }, [config, selection, interval]);
  return { snapshot, alerts, clearAlerts: () => setAlerts([]) };
}
