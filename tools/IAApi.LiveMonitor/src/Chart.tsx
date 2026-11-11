import type { Decision, Reading } from './types';

const time = (n: number) => new Date(n).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
export function Chart({ readings, decisions }: { readings: Reading[]; decisions: Decision[] }) {
  const points = readings.slice(-600);
  if (!points.length) return <div className="empty-chart">Aguardando histórico de valores da API.</div>;
  const minTime = points[0].at, maxTime = points.at(-1)!.at;
  const values = points.map(p => p.value);
  const low = Math.min(...values), high = Math.max(...values), span = high - low || Math.max(Math.abs(low) * .02, 1);
  const x = (at: number) => 42 + (at - minTime) / (maxTime - minTime || 1) * 510;
  const y = (v: number) => 140 - (v - low + span * .1) / (span * 1.2) * 120;
  const path = points.map((p, i) => `${i ? 'L' : 'M'}${x(p.at)},${y(p.value)}`).join(' ');
  const anomalies = points.filter(p => decisions.some(d => d.anomaly && d.readingAt === p.at && d.value === p.value));
  return <div className="chart"><svg viewBox="0 0 580 176" role="img" aria-label="Valores por horário da leitura. Pontos vermelhos indicam anomalias com correlação confirmada.">
    {[low, low + (high - low) / 2, high].map((v, i) => <g key={i}><line x1="42" x2="552" y1={y(v)} y2={y(v)} stroke="#e7eceb"/><text x="36" y={y(v) + 3} textAnchor="end">{v.toLocaleString('pt-BR', { maximumFractionDigits: 2 })}</text></g>)}
    <path d={`${path} L${x(maxTime)},153 L42,153 Z`} fill="#e8f4ef"/><path d={path} stroke="#2a8069" strokeWidth="2" fill="none"/>
    {points.length === 1 && <circle cx={x(minTime)} cy={y(points[0].value)} r="3" fill="#2a8069"/>}
    {anomalies.map(p => <circle key={p.at} cx={x(p.at)} cy={y(p.value)} r="4.5" fill="#d14848" stroke="white" strokeWidth="2"><title>{time(p.at)} · ANOMALIA · {p.value}</title></circle>)}
    <text x="42" y="171">{time(minTime)}</text><text x="552" y="171" textAnchor="end">{time(maxTime)}</text>
  </svg><div className="chart-legend"><span>— Valor recebido</span><span className="red">● Anomalia correlacionada</span></div></div>;
}
