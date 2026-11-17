"""Calibra limiares de aviso do treino a partir do CSV do CP300 (somente leitura).

Uso: python -I avisos_cp300.py <csv>
"""
import csv, sys, math, statistics
from datetime import datetime, timedelta
from collections import defaultdict

CSV = sys.argv[1]
TAGS = [f"TAG{n}" for n in range(22, 34)]
serie = defaultdict(dict)  # tag -> {dt: valor}
with open(CSV, newline="", encoding="utf-8") as f:
    for r in csv.DictReader(f):
        t = r["TagName"]
        if t in TAGS or t == "TAG73":
            serie[t][datetime.fromisoformat(r["DataHora"])] = float(r["Valor"])

todos = sorted(set().union(*[serie[t].keys() for t in TAGS]))
ini, fim = todos[0], todos[-1]
print(f"CSV: {ini} a {fim}")

def pct(ordenados, p):
    pos = p / 100 * (len(ordenados) - 1)
    lo, hi = math.floor(pos), math.ceil(pos)
    if lo == hi:
        return ordenados[lo]
    return ordenados[lo] + (ordenados[hi] - ordenados[lo]) * (pos - lo)

def metricas(tag, a, b):
    pts = sorted((d, v) for d, v in serie[tag].items() if a <= d < b)
    n = len(pts)
    esperado = int((b - a).total_seconds() // 60)
    if n == 0:
        return None
    tempos = [a - timedelta(minutes=1)] + [d for d, _ in pts] + [b]
    buraco = max(int((tempos[i + 1] - tempos[i]).total_seconds() // 60) - 1 for i in range(len(tempos) - 1))
    vals = [v for _, v in pts]
    zeros = sum(1 for v in vals if v == 0) / n * 100
    # maior sequencia constante (qualquer valor) e nao nula, em minutos consecutivos
    maior_c = maior_cnz = run = 1
    for i in range(1, n):
        consecutivo = (pts[i][0] - pts[i - 1][0]) == timedelta(minutes=1)
        if consecutivo and pts[i][1] == pts[i - 1][1]:
            run += 1
        else:
            run = 1
        maior_c = max(maior_c, run)
        if pts[i][1] != 0:
            maior_cnz = max(maior_cnz, run)
    o = sorted(vals)
    q1, q3 = pct(o, 25), pct(o, 75)
    iqr = q3 - q1
    usados = [v for v in vals if q1 - 1.5 * iqr <= v <= q3 + 1.5 * iqr]
    outl = (n - len(usados)) / n * 100
    media = statistics.fmean(usados) if usados else float("nan")
    desvio = statistics.pstdev(usados) if len(usados) > 1 else 0.0
    rot = [v for d, v in serie["TAG73"].items() if a <= d < b]
    falha = sum(1 for v in rot if v == 1)
    return dict(n=n, esperado=esperado, buraco=buraco, zeros=zeros, const=maior_c,
                constnz=maior_cnz, outl=outl, media=media, desvio=desvio, falha=falha)

def resumo(nome, lst):
    if not lst:
        return f"{nome}: -"
    s = sorted(lst)
    return f"{nome}: min {s[0]:.1f} p50 {pct(s,50):.1f} p95 {pct(s,95):.1f} max {s[-1]:.1f}"

# 1) periodo validado da tarefa 27
A, B = datetime(2026, 11, 12, 15), datetime(2026, 11, 13, 15)
print("\n== Periodo validado [12/11 15:00, 13/11 15:00) ==")
print("tag   n/esp  buraco zeros% const constNZ outl%  media     desvio  TAG73min")
for t in TAGS:
    m = metricas(t, A, B)
    print(f"{t} {m['n']}/{m['esperado']} {m['buraco']:4d} {m['zeros']:6.1f} {m['const']:5d} {m['constnz']:5d} {m['outl']:6.1f} {m['media']:9.3f} {m['desvio']:9.3f} {m['falha']}")

# 2) janelas moveis de 24 h e 3 h, passo 1 h, separadas em limpas x com falha
for horas in (24, 3):
    limpas, falhas = defaultdict(list), defaultdict(list)
    a = ini.replace(minute=0, second=0)
    while a + timedelta(hours=horas) <= fim + timedelta(minutes=1):
        b = a + timedelta(hours=horas)
        for t in TAGS:
            m = metricas(t, a, b)
            if not m:
                continue
            alvo = falhas if m["falha"] > 0 else limpas
            for k in ("n", "buraco", "zeros", "const", "constnz", "outl"):
                alvo[(t, k)].append(m[k])
            alvo[("*", "n")].append(m["n"])
            alvo[("*", "buraco")].append(m["buraco"])
            alvo[("*", "constnz")].append(m["constnz"])
            alvo[("*", "outl")].append(m["outl"])
        a += timedelta(hours=1)
    print(f"\n== Janelas de {horas} h (passo 1 h) ==")
    for rotulo, alvo in (("LIMPAS", limpas), ("COM FALHA (TAG73=1)", falhas)):
        nj = len(alvo[("*", "n")]) // len(TAGS) if alvo[("*", "n")] else 0
        print(f"-- {rotulo}: {nj} janelas")
        for k in ("n", "buraco", "constnz", "outl"):
            print("  todas as tags " + resumo(k, alvo[("*", k)]))
        for t in TAGS:
            print(f"  {t} " + " | ".join(resumo(k, alvo[(t, k)]) for k in ("zeros", "constnz", "outl")))
