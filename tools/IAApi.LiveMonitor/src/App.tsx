import { useEffect, useState } from 'react';
import { api } from './api';
import { ageLabel, latestDecision } from './domain';
import { startPolling } from './polling';
import { dateTimeLocalValue, parseDateTimeLocal, restoreTestRuns, testRunDecisions } from './testRuns';
import { Badge, date, DetectionList, TagCard } from './TagCard';
import { useMonitor } from './useMonitor';
import type { Config, Tag } from './types';
import type { TestRun } from './testRuns';

export function App({ config }: { config: Config }) {
  const storageKey = `iaapi-live-monitor:${config.client}:tags`;
  const runStorageKey = `iaapi-live-monitor:${config.client}:test-runs:v1`;
  const selectedRunStorageKey = `iaapi-live-monitor:${config.client}:selected-test-run:v1`;
  const [tags, setTags] = useState<Tag[]>([]);
  const [tagError, setTagError] = useState('');
  const [metadataError, setMetadataError] = useState('');
  const [selected, setSelected] = useState<string[]>(() => { try { const saved = JSON.parse(localStorage.getItem(storageKey) || '[]'); return Array.isArray(saved) ? saved.filter(x => typeof x === 'string') : []; } catch { return []; } });
  const [search, setSearch] = useState('');
  const [runs, setRuns] = useState<TestRun[]>(() => {
    try { return restoreTestRuns(JSON.parse(localStorage.getItem(runStorageKey) || '[]')); }
    catch { return []; }
  });
  const [selectedRunId, setSelectedRunId] = useState(() => {
    try { return localStorage.getItem(selectedRunStorageKey) || ''; }
    catch { return ''; }
  });
  const [newRunName, setNewRunName] = useState('Cenário 1');
  const [runError, setRunError] = useState('');
  const [rowsLimit, setRowsLimit] = useState(50);
  const [interval, setIntervalMs] = useState(config.pollMs);
  const [now, setNow] = useState(Date.now());
  const [metadata, setMetadata] = useState<Awaited<ReturnType<typeof api.metadata>>>([]);
  const { snapshot, alerts, clearAlerts } = useMonitor(config, selected, interval);
  useEffect(() => startPolling(async signal => {
    try { const rows = await api.tags(config.client, signal); if (!Array.isArray(rows) || rows.some(t => typeof t.tagName !== 'string' || typeof t.iaAtiva !== 'boolean')) throw new Error('Contrato inválido de tags.'); if (!signal.aborted) { setTags(rows.filter(t => t.clienteId === config.client)); setTagError(''); } }
    catch (e) { if (!signal.aborted) setTagError(e instanceof Error ? e.message : 'Não foi possível consultar tags.'); }
  }, 60000), [config]);
  useEffect(() => { const controller = new AbortController(); api.metadata(config.client, controller.signal).then(rows => { if (!controller.signal.aborted && Array.isArray(rows)) setMetadata(rows); }).catch(() => { if (!controller.signal.aborted) setMetadataError('Descrições do Bridge indisponíveis.'); }); return () => controller.abort(); }, [config]);
  useEffect(() => { try { localStorage.setItem(storageKey, JSON.stringify(selected)); } catch { /* armazenamento opcional */ } }, [storageKey, selected]);
  useEffect(() => { try { localStorage.setItem(runStorageKey, JSON.stringify(runs)); } catch { /* armazenamento opcional */ } }, [runStorageKey, runs]);
  useEffect(() => { try { localStorage.setItem(selectedRunStorageKey, selectedRunId); } catch { /* armazenamento opcional */ } }, [selectedRunStorageKey, selectedRunId]);
  useEffect(() => { setRowsLimit(50); }, [selectedRunId]);
  useEffect(() => { const timer = window.setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer); }, []);
  const activeRun = runs.find(r => r.endedAt === undefined);
  const selectedRun = runs.find(r => r.id === selectedRunId);
  const startRun = () => {
    if (activeRun) return;
    const startedAt = Date.now();
    const run: TestRun = { id: `run-${startedAt}`, name: newRunName.trim() || 'Cenário 1', startedAt };
    setRuns(old => [...old, run].slice(-25));
    setSelectedRunId(run.id);
    setRunError('');
  };
  const stopRun = () => {
    if (!activeRun) return;
    const endedAt = Date.now();
    setRuns(old => old.map(r => r.id === activeRun.id ? { ...r, endedAt } : r));
    setSelectedRunId(activeRun.id);
    setRunError('');
  };
  const updateRunTime = (field: 'startedAt' | 'endedAt', input: string) => {
    if (!selectedRun) return;
    const parsed = parseDateTimeLocal(input);
    if (parsed === undefined || (field === 'startedAt' && parsed > (selectedRun.endedAt ?? Date.now())) ||
      (field === 'endedAt' && parsed < selectedRun.startedAt)) {
      setRunError('Horário inválido: o fim deve ser posterior ao início, e o início não pode estar no futuro.');
      return;
    }
    setRuns(old => old.map(r => r.id === selectedRun.id ? { ...r, [field]: parsed } : r));
    setRunError('');
  };
  const enriched = tags.map(t => { const m = metadata.find(m => m.fullName === t.tagName || m.name === t.tagName); return { ...t, description: m?.descricao, unit: m?.unidade }; });
  const chosen = selected.map(name => enriched.find(t => t.tagName === name) || { tagName: name, clienteId: config.client, iaAtiva: false });
  const decisions = selected.map(name => latestDecision(snapshot.decisions, snapshot.tags[name]?.diagnostics || [], name));
  const latestAnalysis = decisions.filter((d): d is NonNullable<typeof d> => !!d)
    .reduce<number | undefined>((max, d) => max === undefined ? d.analyzedAt : Math.max(max, d.analyzedAt), undefined);
  const currentDecisions = decisions.filter(d => d && now - d.analyzedAt <= config.staleMs && now - d.analyzedAt >= -5000);
  const anomalies = currentDecisions.filter(d => d?.anomaly === true).length;
  const staleCount = selected.length - currentDecisions.length;
  const historyErrors = selected.filter(tag => !!snapshot.tags[tag]?.historyError).length;
  const historyRecent = selected.filter(tag => { const entry = snapshot.tags[tag]; const received = entry?.historyReceivedAt; return !entry?.historyError && received !== undefined && now - received <= Math.max(config.staleMs, config.historyMs * 2); }).length;
  const apiFresh = !!snapshot.decisionsReceivedAt && !snapshot.decisionsError && now - snapshot.decisionsReceivedAt <= Math.max(config.staleMs, interval * 3);
  const scopedDecisions = testRunDecisions(snapshot.decisions.filter(d => selected.includes(d.tag)), selectedRun, now);
  const runAnomalyCount = scopedDecisions.filter(d => d.anomaly).length;
  const runTagCount = new Set(scopedDecisions.map(d => d.tag)).size;
  const visibleAlerts = alerts.filter(d => selected.includes(d.tag) && (!selectedRun ||
    (d.analyzedAt >= selectedRun.startedAt && d.analyzedAt <= (selectedRun.endedAt ?? now))));
  const connected = apiFresh;
  const toggle = (name: string) => setSelected(old => old.includes(name) ? old.filter(n => n !== name) : [...old, name]);
  const exportDecisions = () => {
    const rows = scopedDecisions;
    const cell = (x: unknown) => `"${String(x ?? '').replaceAll('"', '""')}"`;
    const lines = [['tag', 'registro_decisao_utc', 'decisao', 'valor_analisado', 'score', 'leitura_em_utc_se_conhecida', 'primeiro_recebimento_nesta_abertura', 'fonte'].join(';'),
      ...rows.map(d => [d.tag, new Date(d.analyzedAt).toISOString(), d.anomaly ? 'ANOMALIA' : 'NORMAL', d.value, d.score, d.readingAt === undefined ? '' : new Date(d.readingAt).toISOString(), new Date(d.receivedAt).toISOString(), d.source].map(cell).join(';'))];
    const file = new Blob(['\ufeff', lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(file);
    const a = document.createElement('a'); a.href = url; a.download = `IAApi-${config.client}-${selectedRun ? selectedRun.name.replace(/[^a-zA-Z0-9_-]+/g, '-') : 'todas'}-decisoes-${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
  };
  return <div className="app-shell">
    <aside className="sidebar"><div className="brand"><span className="brand-icon">M</span><div>MARRARI<small>INTELIGÊNCIA INDUSTRIAL</small></div></div>
      <div className="workspace-label">AMBIENTE DE TESTES</div><div className="nav-active"><span>◉</span> Live Monitor <span className="tiny-dot"/></div>
      <div className="client-box"><span>CLIENTE</span><strong>{config.client}</strong><small>Testes industriais · CP300</small></div>
      <div className="selection-heading"><h2>Tags monitoradas</h2><span>{selected.length}</span></div>
      <label className="search"><span>Buscar tag</span><input placeholder="Nome ou descrição…" value={search} onChange={e => setSearch(e.target.value)}/></label>
      <div className="selection-actions"><button onClick={() => setSelected(tags.filter(t => t.iaAtiva).map(t => t.tagName))}>Selecionar ativas</button><button onClick={() => setSelected([])}>Limpar</button></div>
      <div className="tag-options">{enriched.filter(t => `${t.tagName} ${t.description || ''}`.toLowerCase().includes(search.toLowerCase())).map(t => <label key={t.tagName}><input type="checkbox" checked={selected.includes(t.tagName)} onChange={() => toggle(t.tagName)}/><span>{t.tagName}<small>{t.iaAtiva ? 'IA ativa' : 'IA desativada'}</small></span></label>)}{!tags.length && <p>{tagError || 'Aguardando catálogo da IAApi…'}</p>}</div>
      {metadataError && <p className="sidebar-note">{metadataError}</p>}
      <div className="sidebar-footer"><span>▣ Somente leitura</span><p>Monitor independente.<br/>Nenhum comando de detecção.</p></div>
    </aside>
    <main><header className="topbar"><span><span className="breadcrumb">IAApi /</span> Monitoramento</span><div className={`connection ${connected ? 'online' : ''}`}><i/>{connected ? 'IAApi conectada' : snapshot.decisionsError ? 'IAApi indisponível' : 'Conectando à IAApi'}</div></header>
      <div className="content"><div className="page-heading"><div><div className="eyebrow">ACOMPANHAMENTO OPERACIONAL</div><h1>Live Monitor<span> / {config.client}</span></h1><p>Acompanhe as decisões da IA durante os testes do CP300.</p></div><label className="poll-control">Consultar decisões a cada<select value={interval} onChange={e => setIntervalMs(Number(e.target.value))}>{[...new Set([config.pollMs, 1000, 2000, 5000, 10000])].sort((a, b) => a - b).map(v => <option key={v} value={v}>{v / 1000} segundos</option>)}</select></label></div>
      <div className="notice"><span>◷</span><p><b>A IA tem seu próprio ritmo.</b> O serviço espera 60 s após processar todas as tags. Cada ciclo leva 60 s + processamento; o polling apenas consulta os resultados.</p><span className="notice-label">SEM DISPARO DE ANÁLISE</span></div>
      <section className="test-session" aria-label="Sessão de teste">
        <div className="test-session-heading">
          <div><div className="eyebrow">REGISTRO DO EXPERIMENTO</div><h2>Sessão de teste</h2><p>Marque o início <b>antes</b> de iniciar o cenário no CLP. O painel filtra decisões pelo horário em que a IA as registrou.</p></div>
          <span className={`run-status ${activeRun ? 'running' : ''}`}>{activeRun ? '● TESTE EM ANDAMENTO' : 'SEM TESTE ATIVO'}</span>
        </div>
        <div className="test-session-controls">
          <label>Nome do novo teste<input aria-label="Nome do teste" value={newRunName} onChange={e => setNewRunName(e.target.value)} maxLength={80}/></label>
          <button type="button" className="primary-action" onClick={startRun} disabled={!!activeRun}>▶ Iniciar teste agora</button>
          <button type="button" className="stop-action" onClick={stopRun} disabled={!activeRun}>■ Encerrar teste</button>
          <label>Exibir análises de<select aria-label="Selecionar sessão" value={selectedRun?.id || ''} onChange={e => { setSelectedRunId(e.target.value); setRunError(''); }}>
            <option value="">Todas as datas</option>
            {[...runs].reverse().map(run => <option key={run.id} value={run.id}>{run.name} — {date(run.startedAt)} {run.endedAt === undefined ? '(em andamento)' : '(encerrado)'}</option>)}
          </select></label>
        </div>
        {selectedRun ? <>
          <div className="test-session-times">
            <label>Início registrado no painel<input aria-label="Início do teste" type="datetime-local" step="60" value={dateTimeLocalValue(selectedRun.startedAt)} onChange={e => updateRunTime('startedAt', e.target.value)}/></label>
            {selectedRun.endedAt !== undefined ? <label>Fim registrado no painel<input aria-label="Fim do teste" type="datetime-local" step="60" value={dateTimeLocalValue(selectedRun.endedAt)} onChange={e => updateRunTime('endedAt', e.target.value)}/></label> : <div className="run-end-current"><span>Fim do período</span><strong>Em andamento · até agora</strong></div>}
            <div className="test-session-counts"><span>Análises do período</span><strong>{scopedDecisions.length}</strong><small>{runAnomalyCount} anômala(s) · {runTagCount}/{selected.length} tags selecionadas com decisão</small></div>
          </div>
          <p className="test-session-hint">Os horários acima são do relógio deste computador e podem ser ajustados para corresponder ao início/fim do CLP. O filtro usa a data de registro da decisão, <b>não</b> a data da leitura industrial. Os dados são consultados novamente na IAApi; apenas as datas da sessão ficam salvas neste navegador.</p>
        </> : <p className="test-session-hint">Sem filtro de sessão: o painel mostra decisões de todas as datas que a API retornar. Inicie uma sessão para isolar as análises do próximo teste.</p>}
        {runError && <p className="test-session-error" role="alert">{runError}</p>}
      </section>
      {snapshot.decisionsError && <div className="error-banner" role="status"><b>Sem comunicação com a IAApi</b><span>{snapshot.decisionsError} Endereço: {config.apiOrigin}. O monitor tentará novamente automaticamente.</span></div>}
      {tagError && tags.length > 0 && <div className="error-banner">Catálogo de tags: {tagError}</div>}
      <section className="monitor-readiness" aria-label="Confiabilidade do monitoramento">
        <h2>Saúde do monitoramento agora</h2>
        <p>Este resumo é do momento atual, independente do intervalo do teste. Uma API conectada não significa que a IA está analisando dados recentes.</p>
        <div className="readiness-grid">
          <div><span>1 · Consulta de decisões</span><strong className={apiFresh ? 'green' : 'red'}>{apiFresh ? 'RESPONDENDO' : 'SEM RESPOSTA RECENTE'}</strong><small>Última resposta: {date(snapshot.decisionsReceivedAt)} · {ageLabel(snapshot.decisionsReceivedAt, now)}</small></div>
          <div><span>2 · Análise operacional mais recente</span><strong className={latestAnalysis !== undefined && now - latestAnalysis <= config.staleMs ? 'green' : 'red'}>{ageLabel(latestAnalysis, now)}</strong><small>Registrada às {date(latestAnalysis)} · pode ser antiga mesmo com API online</small></div>
          <div><span>3 · Históricos consultados</span><strong>{historyRecent}/{selected.length} recentes</strong><small>{historyErrors ? `${historyErrors} tag(s) com erro de histórico` : 'Sem erros de histórico registrados'} · não mede a idade da leitura</small></div>
          <div><span>4 · Decisões recentes das tags</span><strong>{currentDecisions.length}/{selected.length}</strong><small>{anomalies} anômala(s) · {staleCount} sem decisão recente · janela {config.staleMs / 1000}s</small></div>
        </div>
        {(staleCount > 0 || historyErrors > 0 || !apiFresh) && <p className="readiness-warning">Atenção: não conclua que a IA está normal apenas porque uma decisão antiga diz NORMAL. Confira os horários e a cobertura antes de avaliar a falha.</p>}
      </section>
      <div className="stats"><div><span>Tags selecionadas</span><strong>{selected.length.toString().padStart(2, '0')}</strong><small>{tags.length} disponíveis na API</small></div><div><span>Decisões normais RECENTES</span><strong className="green">{(currentDecisions.length - anomalies).toString().padStart(2, '0')}</strong><small>Dentro de {config.staleMs / 1000}s</small></div><div><span>Decisões anômalas RECENTES</span><strong className="red">{anomalies.toString().padStart(2, '0')}</strong><small>Não inclui anomalias antigas</small></div><div><span>Última análise registrada</span><strong className="time-stat">{latestAnalysis === undefined ? '—' : new Date(latestAnalysis).toLocaleTimeString('pt-BR')}</strong><small>{ageLabel(latestAnalysis, now)} · não é a hora da coleta</small></div></div>
      <div aria-live="polite">{visibleAlerts.length > 0 && <div className="anomaly-alert"><div><b>Nova anomalia detectada</b><span>{visibleAlerts[0].tag} · {date(visibleAlerts[0].analyzedAt)} · {visibleAlerts.length} nova(s) ocorrência(s)</span></div><button onClick={clearAlerts}>Dispensar destaque</button></div>}</div>
      <div className="section-heading"><div><h2>Monitoramento por tag</h2><p>O estado do card refere-se à leitura atual; a última decisão é exibida separadamente.</p></div><span className="caption">Limite de idade: {config.staleMs / 1000} s</span></div>
      {!selected.length ? <div className="empty-state"><div>⌁</div><h2>Escolha as tags do seu teste</h2><p>Selecione as tags na lateral. Os valores e decisões aparecerão assim que forem recebidos da IAApi.</p><span>Nenhum dado de demonstração é utilizado.</span></div> : <div className="card-grid">{chosen.map(tag => <TagCard key={tag.tagName} tag={tag} snapshot={snapshot} config={config} now={now} selectedRun={selectedRun} highlighted={visibleAlerts.some(a => a.tag === tag.tagName)}/>)}</div>}
      <div className="section-heading history-heading"><div><h2>{selectedRun ? `Análises do teste: ${selectedRun.name}` : 'Últimas análises · todas as datas'}</h2><p>{selectedRun ? `Registros de ${date(selectedRun.startedAt)} até ${selectedRun.endedAt === undefined ? 'agora' : date(selectedRun.endedAt)} · ` : ''}{scopedDecisions.length} decisão(ões) disponíveis · horário do registro da IA, não da coleta</p></div><button type="button" className="export-button" onClick={exportDecisions} disabled={!scopedDecisions.length}>Exportar {selectedRun ? 'teste' : 'decisões'} (CSV)</button></div>
      <DetectionList decisions={scopedDecisions} limit={rowsLimit}/>
      {scopedDecisions.length > rowsLimit && <button type="button" className="load-more" onClick={() => setRowsLimit(n => n + 50)}>Mostrar mais 50 análises ({Math.min(rowsLimit, scopedDecisions.length)} de {scopedDecisions.length})</button>}
      <footer>IAApi.LiveMonitor <span>Horários exibidos no fuso do navegador · fontes sem fuso interpretadas como UTC{config.sourceOffset} · histórico a cada {config.historyMs / 1000} s + tempo de consulta</span></footer>
    </div></main>
  </div>;
}
