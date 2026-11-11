import { Chart } from './Chart';
import { ageLabel, latestDecision, latestReading, readingState } from './domain';
import { testRunDecisions } from './testRuns';
import type { TestRun } from './testRuns';
import type { Config, Decision, Snapshot, Tag } from './types';

export const date = (n?: number) => n === undefined ? 'Não informado' : new Date(n).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' });
export const number = (n?: number | null) => n == null ? '—' : n.toLocaleString('pt-BR', { maximumFractionDigits: 4 });
const delta = (a?: number, b?: number) => a === undefined || b === undefined ? 'Não disponível' : `${((b - a) / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 2 })} s${b < a ? ' (verificar relógios)' : ''}`;
const stateClass = (s: string) => s === 'ANOMALIA' ? 'danger' : s === 'NORMAL' ? 'good' : s === 'AGUARDANDO' || s === 'DESATUALIZADO' ? 'warning' : 'muted';
export function Badge({ value }: { value: string }) { return <span className={`badge ${stateClass(value)}`}><i/>{value}</span>; }
export function TagCard({ tag, snapshot, config, now, highlighted, selectedRun }: { tag: Tag; snapshot: Snapshot; config: Config; now: number; highlighted: boolean; selectedRun?: TestRun }) {
  const data = snapshot.tags[tag.tagName];
  const diagnostics = data?.diagnostics || [];
  const last = latestDecision(snapshot.decisions, diagnostics, tag.tagName);
  const runEntries = testRunDecisions([...snapshot.decisions, ...diagnostics], selectedRun, now);
  const lastInRun = selectedRun ? latestDecision(runEntries, [], tag.tagName) : undefined;
  const displayedDecision = selectedRun ? lastInRun : last;
  const reading = latestReading(data?.readings || [], diagnostics);
  const diagnosticFresh = last?.source === 'diagnostic' && !data?.diagnosticError;
  const unavailable = Boolean((snapshot.decisionsError && !diagnosticFresh) || (data?.historyError && !diagnosticFresh));
  const state = readingState(displayedDecision, reading, unavailable, now, config.staleMs);
  const shadows = snapshot.shadow.filter(s => s.expectedTags?.includes(tag.tagName) &&
    (!selectedRun || (Date.parse(s.recordedAtUtc) >= selectedRun.startedAt && Date.parse(s.recordedAtUtc) <= (selectedRun.endedAt ?? now))));
  const analyzed = last?.analyzedAt;
  const decisionTime = displayedDecision?.analyzedAt;
  const analysisAge = decisionTime === undefined ? undefined : now - decisionTime;
  const analysisStale = analysisAge === undefined || analysisAge > config.staleMs || analysisAge < -5000;
  const readingStale = reading?.at === undefined || now - reading.at > config.staleMs || now - reading.at < -5000;
  const history = data?.readings || [];
  const chartReadings = [...new Map([...history, ...diagnostics.filter(d => d.readingAt !== undefined).map(d => ({ at: d.readingAt!, value: d.value }))].map(p => [p.at, p])).values()]
    .filter(p => !selectedRun || (p.at >= selectedRun.startedAt && p.at <= (selectedRun.endedAt ?? now)))
    .sort((a, b) => a.at - b.at);
  const chartDiagnostics = testRunDecisions(diagnostics, selectedRun, now);
  return <article className={`tag-card ${highlighted ? 'new-anomaly' : ''}`}>
    <div className="card-top"><div><div className="eyebrow">{tag.iaAtiva ? 'IA ATIVA' : 'IA DESATIVADA NA API'}</div><h3 title={tag.tagName}>{tag.tagName}</h3><p>{tag.description || 'Descrição não fornecida pela API'}</p></div><Badge value={state}/></div>
    <div className="analysis-time-highlight">
      <div><span>ÚLTIMA ANÁLISE REGISTRADA PELA IA</span><strong>{date(analyzed)}</strong><small>{ageLabel(analyzed, now)} · horário do registro da decisão, não da leitura de origem</small></div>
      {selectedRun && <div className="analysis-time-run"><span>ÚLTIMA ANÁLISE DESTE TESTE</span><strong>{lastInRun ? date(lastInRun.analyzedAt) : 'NENHUMA AINDA'}</strong><small>{lastInRun ? `${lastInRun.anomaly ? 'ANOMALIA' : 'NORMAL'} · valor analisado ${number(lastInRun.value)}` : 'Nenhuma decisão registrada dentro do período escolhido'}</small></div>}
    </div>
    <div className="telemetry-freshness" aria-label="Atualização dos dados">
      <span className={readingStale ? 'age-chip late' : 'age-chip fresh'}>Leitura: {ageLabel(reading?.at, now)}</span>
      <span className={analysisStale ? 'age-chip late' : 'age-chip fresh'}>Análise do cartão: {ageLabel(decisionTime, now)}</span>
    </div>
    <div className="reading"><small className="section-mini-label">ÚLTIMO VALOR RECEBIDO DO HISTÓRICO</small><strong>{number(reading?.value)}</strong><span>{tag.unit || 'valor'}</span><small>Horário da leitura: {date(reading?.at)}</small><small>Consulta ao histórico: {date(data?.historyReceivedAt)}{data?.historyError ? ' · FALHOU' : ''}</small></div>
    <div className={`decision-strip ${analysisStale ? 'decision-old' : ''}`}>
      <div><span>{selectedRun ? 'ÚLTIMA DECISÃO DESTE TESTE' : 'ÚLTIMA DECISÃO REGISTRADA (PODE SER ANTIGA)'}</span>
        <b className={displayedDecision?.anomaly ? 'red' : displayedDecision ? 'green' : ''}>{displayedDecision ? displayedDecision.anomaly ? 'ANOMALIA' : 'NORMAL' : 'SEM ANÁLISE'}</b>
        <small>{analysisStale ? 'Não representa a situação atual' : 'Decisão registrada recentemente'}</small>
      </div>
      <div><span>QUANDO A IA REGISTROU</span><b>{date(decisionTime)}</b><small>Idade: {ageLabel(decisionTime, now)}</small></div>
    </div>
    <p className="state-note">{state === 'AGUARDANDO' ? displayedDecision?.readingAt === undefined && displayedDecision ? 'A API confirma a decisão acima, mas não informa a leitura de origem. A classificação da leitura atual aguarda correlação.' : 'Ainda não há decisão confirmada para esta leitura.' : state === 'DESATUALIZADO' ? 'Leitura ou análise ultrapassou o limite de idade configurado.' : state === 'INDISPONÍVEL' ? 'Consulta indisponível. Dados anteriores preservados; não representam confirmação atual.' : 'Decisão final e leitura correspondem em timestamp e valor.'}</p>
    {displayedDecision && <p className="value-caution">Valor usado nessa decisão: <b>{number(displayedDecision.value)}</b>. Não confundir com a última leitura disponível acima.
      {displayedDecision.readingAt === undefined ? ' A API persistida não informa quando essa leitura foi coletada.' : displayedDecision.readingAtBasis === 'configured-offset' ? ' Horário da leitura estimado com o fuso configurado; não há UTC de origem confirmado.' : ''}
    </p>}
    {selectedRun && <p className="chart-run-note">Gráfico limitado ao intervalo do teste, com os dados de histórico atualmente disponíveis na API.</p>}
    <Chart readings={chartReadings} decisions={chartDiagnostics}/>
    <dl className="metrics"><div><dt>{displayedDecision?.source === 'diagnostic' ? 'Score normalizado · ensemble' : 'Score · persistido'}</dt><dd>{number(displayedDecision?.score)}</dd></div><div><dt>Threshold · ensemble</dt><dd>{number(displayedDecision?.threshold)}</dd></div><div><dt>Fonte da decisão</dt><dd>{displayedDecision?.method || (displayedDecision ? 'Decisão persistida' : '—')}</dd></div><div><dt>Valor analisado</dt><dd>{number(displayedDecision?.value)}</dd></div></dl>
    <details><summary>Tempos, detectores e multivariada</summary><dl className="timing">
      <div><dt>Leitura analisada</dt><dd>{date(displayedDecision?.readingAt)}</dd></div>
      <div><dt>Primeiro recebimento nesta sessão do painel</dt><dd>{date(displayedDecision?.receivedAt)}</dd></div>
      <div><dt>Leitura → análise</dt><dd>{displayedDecision?.readingAtBasis === 'configured-offset' ? `${delta(displayedDecision?.readingAt, decisionTime)} (fuso assumido)` : delta(displayedDecision?.readingAt, decisionTime)}</dd></div>
      <div><dt>Análise → monitor</dt><dd>{delta(decisionTime, displayedDecision?.receivedAt)}</dd></div>
      <div><dt>Leitura → monitor</dt><dd>{delta(displayedDecision?.readingAt, displayedDecision?.receivedAt)}</dd></div>
      <div><dt>Última consulta ao histórico</dt><dd>{date(data?.historyReceivedAt)}</dd></div>
    </dl>
    <div className="diagnostics"><b>Resultados intermediários dos detectores</b><p>Votos e score do ensemble não substituem a decisão operacional.</p>
      {displayedDecision?.detail?.detectors?.length ? displayedDecision.detail.detectors.map((d, i) => <div className="detector" key={d.name + i}><span>{d.name}</span><span>{d.status === 1 || d.status === 'Executed' ? d.votedAnomaly ? 'Voto anômalo' : 'Sem voto anômalo' : `Não executado / status ${d.status}`}</span><b>{number(d.normalizedScore)}</b></div>) : <p>Detalhes não disponíveis nesta fonte.</p>}
    </div>
    <div className="diagnostics"><b>Multivariada · experimental / shadow</b><p>Estes eventos não confirmam uma anomalia operacional da tag.</p>
      {!config.diagnosticsConfigured ? <p>Chave de diagnóstico não configurada no servidor local.</p> : data?.diagnosticError || snapshot.shadowError ? <p className="red">{data?.diagnosticError || snapshot.shadowError}</p> : !shadows.length ? <p>Sem evento de grupo associado a esta tag no buffer consultado.</p> : shadows.map(s => <div className="shadow" key={s.diagnosticId}><b>{s.groupName}</b><span>{now - Date.parse(s.recordedAtUtc) > config.staleMs ? 'DESATUALIZADO · ' : ''}{s.mahalanobisExecuted && typeof s.anomaly === 'boolean' ? s.anomaly ? 'Resultado shadow anômalo' : 'Resultado shadow normal' : 'Sem execução confirmada neste evento'}</span><small>{date(Date.parse(s.recordedAtUtc))} · vetor {s.vectorComplete ? 'completo' : 'incompleto'} · {s.reasonCode || `evento ${s.eventKind}`}</small><small>Score {number(s.normalizedScore)} / threshold {number(s.threshold)}</small></div>)}
    </div></details>
    {data?.historyError && <div className="inline-error">Histórico: {data.historyError}</div>}
    {data?.diagnosticError && <div className="inline-error">Diagnóstico: {data.diagnosticError}</div>}
  </article>;
}
export function DetectionList({ decisions, limit = 50 }: { decisions: Decision[]; limit?: number }) {
  return <div className="table-wrap"><table><thead><tr><th>Análise / registro</th><th>Tag</th><th>Decisão operacional</th><th>Valor</th><th>Score persistido</th><th>Recebido no monitor</th></tr></thead><tbody>
    {decisions.slice(0, limit).map(d => <tr key={d.id}><td>{date(d.analyzedAt)}</td><td className="tag-cell">{d.tag}</td><td><Badge value={d.anomaly ? 'ANOMALIA' : 'NORMAL'}/></td><td>{number(d.value)}</td><td>{number(d.score)}</td><td>{date(d.receivedAt)}</td></tr>)}
    {!decisions.length && <tr><td colSpan={6} className="empty">Nenhuma decisão recebida para as tags selecionadas.</td></tr>}
  </tbody></table></div>;
}
