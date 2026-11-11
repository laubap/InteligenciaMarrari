export type State = 'NORMAL' | 'ANOMALIA' | 'AGUARDANDO' | 'DESATUALIZADO' | 'INDISPONÍVEL';
export interface Config { client: string; pollMs: number; historyMs: number; staleMs: number; sourceOffset: string; diagnosticsConfigured: boolean; apiOrigin: string }
export interface Tag { clienteId: string; tagName: string; iaAtiva: boolean; description?: string; unit?: string }
export interface Reading { at: number; value: number }
export interface Detector { name: string; status: number | string; votedAnomaly: boolean; normalizedScore?: number | null }
export interface Diagnostic {
  context: { diagnosticId: string; receivedAtUtc: string };
  clienteId: string; tagName: string; readingTimestampUtc: string | null; readingTimestampOriginal?: string | null; readingTimestampKind?: string | null; readingValue: number;
  completedAtUtc: string | null; completeness: { status: number | string; missingSections: string[] };
  runtimeDecision?: { finalAnomaly: boolean; source: string; reasons: string[] } | null;
  ensemble?: { normalizedScore: number; originalScore: number; threshold: number; legacyAnomaly: boolean } | null;
  detectors: Detector[];
}
export interface DiagnosticSummary { diagnosticId: string; clienteId: string; tagName: string; readingTimestampUtc: string | null; readingTimestampOriginal?: string | null; readingTimestampKind?: string | null; finalAnomaly: boolean | null }
export interface Shadow {
  diagnosticId: string; vectorId: string; clienteId: string; groupName: string;
  eventKind: number | string; recordedAtUtc: string; expectedTags?: string[];
  mahalanobisExecuted: boolean; anomaly: boolean | null; vectorComplete: boolean;
  normalizedScore?: number | null; threshold?: number | null; reasonCode?: string | null;
}
export interface Decision {
  id: string; tag: string; anomaly: boolean; value: number; analyzedAt: number;
  readingAt?: number; readingAtBasis?: 'known-utc' | 'configured-offset'; receivedAt: number; score?: number; threshold?: number;
  source: 'persisted' | 'diagnostic'; method?: string; detail?: Diagnostic;
}
export interface TagData { readings: Reading[]; historyError?: string; historyReceivedAt?: number; diagnostics: Decision[]; diagnosticError?: string }
export interface Snapshot {
  decisions: Decision[]; decisionsError?: string; decisionsReceivedAt?: number;
  tags: Record<string, TagData>; shadow: Shadow[]; shadowError?: string;
  lastResponseAt?: number; revision: number;
}
