using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using IAApi.Cp300OfflineReplay;

namespace IAApi.Tests.Cp300OfflineReplayTool;

/// <summary>
/// Subcomando "placar" sobre CSVs sinteticos (gabarito + saida de replay).
/// Isolamento: so le e grava arquivos temporarios; nao executa o replay, nao
/// acessa Bridge, PSi4, CLP nem banco.
///
/// Linha do tempo: 1 leitura/min de 00:00 a 02:59 (180 min).
/// Slots A/B no layout do CP300 (TAG61-68), TAG44 = bomba principal, TAG73 = anomalia ativa.
/// </summary>
public sealed class Cp300PlacarTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private const int Minutos = 180;
    private static readonly string[] TagsReplay = ["TAG22", "TAG25", "TAG27", "TAG28", "TAG29", "TAG30"];

    private const string MapaTeste = """
        // mapa minimo para os testes (mesmo formato do placar_falhas_cp300.json)
        {
          "tagBombaPrincipal": "TAG44",
          "tagAnomaliaAtiva": "TAG73",
          "slots": [
            { "nome": "A", "codigo": "TAG61", "alvo": "TAG62", "severidade": "TAG63", "fase": "TAG64" },
            { "nome": "B", "codigo": "TAG65", "alvo": "TAG66", "severidade": "TAG67", "fase": "TAG68" },
            { "nome": "C", "codigo": "TAG69", "alvo": "TAG70", "severidade": "TAG71", "fase": "TAG72" },
          ],
          "bombas": {
            "1": { "nome": "B-01", "corrente": "TAG27", "vibracao": "TAG29", "temperaturaMancal": "TAG36" },
            "2": { "nome": "B-02", "corrente": "TAG28", "vibracao": "TAG30", "temperaturaMancal": "TAG37" }
          },
          "canaisSensor": { "1": "TAG22", "4": "TAG25" },
          "falhas": {
            "2":  { "nome": "Obstrucao", "tags": [ "ALVO.corrente", "OUTRA.corrente", "OUTRA.vibracao" ] },
            "9":  { "nome": "Lubrificacao", "tags": [ "ALVO.temperaturaMancal", "ALVO.vibracao" ] },
            "10": { "nome": "Desbalanceamento", "tags": [ "ALVO.vibracao" ] },
            "21": { "nome": "Drift", "tags": [ "CANAL" ] }
          }
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "IAApi-Cp300Placar-Tests", Guid.NewGuid().ToString("N"));

    public Cp300PlacarTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Placar_DeteccaoNaTagAfetada_ComAtrasoESeveridade_ECoincidenciaNaoConta()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 10, alvo: 1, inicio: 60, fim: 89);
        var alarmes = new Alarmes()
            .Add("TAG29", Range(64, 89))   // deteccao real
            .Add("TAG30", Range(70, 72))   // outra bomba: falha 10 nao afeta -> coincidencia
            .Add("TAG28", 10, 130);        // fora das falhas

        var r = Run(g, alarmes);

        var e = Assert.Single(r.Eventos);
        Assert.True(e.Detectado);
        Assert.Equal(["TAG29"], e.TagsDeteccao);
        Assert.Equal(T0.AddMinutes(64), e.PrimeiroAlarme);
        Assert.Equal(4, e.AtrasoMinutos);
        Assert.Equal(Severidade(64), e.SeveridadeNaDeteccao);
        Assert.Equal(2, e.FaseNaDeteccao);
        Assert.Equal(Severidade(89), e.SeveridadeMaxima);
        Assert.Equal("B-01", e.AlvoDescricao);
        Assert.Equal(26, e.MinutosTagAtribuidos);
        Assert.Equal(3, e.MinutosTagCoincidencia);

        Assert.Equal(31, r.TotalAlarmes);
        Assert.Equal(26, r.AlarmesAtribuidos);
        Assert.Equal(3, r.AlarmesCoincidencia);
        Assert.Equal(2, r.AlarmesFalsosFora);
        Assert.Equal(30, r.MinutosComFalha);

        var tag28 = r.PorTag.Single(t => t.Tag == "TAG28");
        Assert.Equal(2, tag28.FalsosFora);
        Assert.Equal(Minutos - 30, tag28.AvaliadosFora);
        Assert.Equal(1, r.PorHora.Single(h => h.Hora == 0).FalsosFora);
        Assert.Equal(0, r.PorHora.Single(h => h.Hora == 1).FalsosFora);
        Assert.Equal(1, r.PorHora.Single(h => h.Hora == 2).FalsosFora);
        Assert.Equal(new[] { ("TAG28", 1) }, r.PorHora.Single(h => h.Hora == 2).PorTag);
    }

    [Fact]
    public void Placar_TagQueAlarmavaNaJanelaAntes_NaoContaComoDeteccao()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 10, alvo: 1, inicio: 60, fim: 89);
        var alarmes = new Alarmes().Add("TAG29", 50).Add("TAG29", Range(64, 89));

        var com20 = Run(g, alarmes, janelaAntes: 20);
        var e = Assert.Single(com20.Eventos);
        Assert.False(e.Detectado);
        Assert.Equal(new Dictionary<string, int> { ["TAG29"] = 1 }, e.TagsExcluidasAlarmeAntes);
        Assert.Empty(e.TagsElegiveis);
        Assert.Equal(0, com20.AlarmesAtribuidos);
        Assert.Equal(26, com20.AlarmesCoincidencia);
        Assert.Equal(1, com20.AlarmesFalsosFora);

        // Com N = 5 o alarme das 00:50 fica fora da janela [00:55, 01:00).
        var com5 = Run(g, alarmes, janelaAntes: 5);
        Assert.True(Assert.Single(com5.Eventos).Detectado);
        Assert.Equal(26, com5.AlarmesAtribuidos);
    }

    [Fact]
    public void Placar_AlvoZero_UsaBombaPrincipalAntesDaFalha()
    {
        var g = new Gabarito(principal: 2);
        g.Falha("A", codigo: 10, alvo: 0, inicio: 60, fim: 89);
        var alarmes = new Alarmes().Add("TAG30", Range(62, 70)).Add("TAG29", Range(61, 70));

        var e = Assert.Single(Run(g, alarmes).Eventos);

        Assert.Equal("principal = B-02", e.AlvoDescricao);
        Assert.Equal(["TAG30"], e.TagsAfetadas);
        Assert.Equal(["TAG30"], e.TagsDeteccao);
        Assert.Equal(2, e.AtrasoMinutos);
        Assert.Equal(10, e.MinutosTagCoincidencia); // TAG29 = bomba que nao e alvo
    }

    [Fact]
    public void Placar_FalhaOUTRA_ResolveReserva_ETagForaDoReplayFicaListada()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 2, alvo: 0, inicio: 60, fim: 70);
        g.Falha("B", codigo: 9, alvo: 1, inicio: 100, fim: 110);
        var alarmes = new Alarmes().Add("TAG30", 65);

        var r = Run(g, alarmes);

        var obstrucao = r.Eventos.Single(x => x.Codigo == 2);
        Assert.Equal(["TAG27", "TAG28", "TAG30"], obstrucao.TagsAfetadas);
        // 1 minuto de alarme: com a duracao minima padrao (3) e so sinal breve.
        Assert.False(obstrucao.Detectado);
        Assert.Equal(["TAG30"], obstrucao.TagsSinalBreve);
        Assert.Equal(1, obstrucao.DuracaoSinalBreve);
        Assert.Equal(5, obstrucao.AtrasoSinalBreve);
        Assert.Equal(1, obstrucao.MinutosTagAtribuidos); // a classificacao dos alarmes nao muda
        Assert.Equal(["TAG30"], Assert.Single(Run(g, alarmes, duracaoMinima: 1).Eventos, x => x.Codigo == 2).TagsDeteccao);
        var lubrificacao = r.Eventos.Single(x => x.Codigo == 9);
        Assert.Equal(["TAG36"], lubrificacao.TagsForaDoReplay);
        Assert.False(lubrificacao.Detectado);
    }

    [Fact]
    public void Placar_DuracaoMinima_SinalBreveAntesDaDeteccaoFicaLadoALado()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 10, alvo: 1, inicio: 60, fim: 89);
        var alarmes = new Alarmes().Add("TAG29", 62, 63).Add("TAG29", Range(70, 89));

        var e = Assert.Single(Run(g, alarmes).Eventos);

        Assert.True(e.Detectado);
        Assert.Equal(T0.AddMinutes(70), e.PrimeiroAlarme);
        Assert.Equal(10, e.AtrasoMinutos);
        Assert.Equal(20, e.DuracaoDeteccao);
        Assert.Equal(Severidade(70), e.SeveridadeNaDeteccao);
        Assert.Equal(T0.AddMinutes(62), e.SinalBreve);
        Assert.Equal(["TAG29"], e.TagsSinalBreve);
        Assert.Equal(2, e.DuracaoSinalBreve);
        Assert.Equal(2, e.AtrasoSinalBreve);
        Assert.Equal(Severidade(62), e.SeveridadeSinalBreve);
        Assert.Equal(22, e.MinutosTagAtribuidos);

        // D = 2: a sequencia de 2 min ja e deteccao, e nao ha sinal breve.
        var d2 = Assert.Single(Run(g, alarmes, duracaoMinima: 2).Eventos);
        Assert.Equal(T0.AddMinutes(62), d2.PrimeiroAlarme);
        Assert.Null(d2.SinalBreve);
    }

    [Fact]
    public void Placar_DuracaoMinima_SequenciaCortadaNoFimDaFalha_SinalBreveDepoisDaDeteccaoIgnorado()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 2, alvo: 1, inicio: 60, fim: 89);
        var alarmes = new Alarmes()
            .Add("TAG27", Range(64, 70))   // deteccao (7 min)
            .Add("TAG30", 75)              // breve, mas depois da deteccao: nao informado
            .Add("TAG28", Range(88, 95));  // so 88-89 dentro da falha

        var e = Assert.Single(Run(g, alarmes).Eventos);
        Assert.Equal(["TAG27"], e.TagsDeteccao);
        Assert.Equal(7, e.DuracaoDeteccao);
        Assert.Null(e.SinalBreve);

        var semTag27 = Assert.Single(Run(g, new Alarmes().Add("TAG28", Range(88, 95))).Eventos);
        Assert.False(semTag27.Detectado);
        Assert.Equal(2, semTag27.DuracaoSinalBreve);
    }

    [Fact]
    public void Placar_DuracaoMinimaInvalida_Recusa()
    {
        Assert.Throws<ArgumentException>(() => Run(new Gabarito(), new Alarmes(), duracaoMinima: 0));
    }

    [Fact]
    public void Placar_FalhasSobrepostas_AlarmeAtribuidoAUmaNaoViraCoincidenciaDaOutra()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 10, alvo: 1, inicio: 60, fim: 119);
        g.Falha("B", codigo: 9, alvo: 1, inicio: 90, fim: 119);
        var alarmes = new Alarmes().Add("TAG29", Range(64, 119));

        var r = Run(g, alarmes);

        var a = r.Eventos.Single(x => x.Slot == "A");
        var b = r.Eventos.Single(x => x.Slot == "B");
        Assert.True(a.Detectado);
        Assert.Equal(56, a.MinutosTagAtribuidos);
        // Slot B: TAG29 ja alarmava -> excluida; agravamento nao e deteccao (P24).
        Assert.False(b.Detectado);
        Assert.Equal(20, b.TagsExcluidasAlarmeAntes["TAG29"]);
        Assert.Equal(0, b.MinutosTagCoincidencia);
        Assert.Equal(0, r.AlarmesCoincidencia);
        Assert.Equal(56, r.AlarmesAtribuidos);
    }

    [Fact]
    public void Placar_Tag73SemSlot_ContaComoFalhaAtiva_NaoComoFalsoFora()
    {
        var g = new Gabarito();
        g.Set("TAG73", 150, 1);
        var alarmes = new Alarmes().Add("TAG22", 150, 151);

        var r = Run(g, alarmes);

        Assert.Empty(r.Eventos);
        Assert.Equal(1, r.MinutosComFalha);
        Assert.Equal(1, r.AlarmesCoincidencia);
        Assert.Equal(1, r.AlarmesFalsosFora);
    }

    [Fact]
    public void Placar_FalhaDeSensor_UsaCanalDoAlvo_IgnorandoSentidoNegativo()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 21, alvo: 104, inicio: 30, fim: 69);
        var alarmes = new Alarmes().Add("TAG25", Range(45, 69));

        var e = Assert.Single(Run(g, alarmes).Eventos);

        Assert.Equal(["TAG25"], e.TagsAfetadas);
        Assert.Equal(15, e.AtrasoMinutos);
        Assert.Equal("sensor 4 (negativo)", e.AlvoDescricao);
    }

    [Fact]
    public void Placar_BuracoNoGabarito_SeparaEventosAcimaDaTolerancia()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 10, alvo: 1, inicio: 60, fim: 89);
        g.Remove(["TAG61", "TAG62", "TAG63", "TAG64"], 70, 72); // 3 minutos sem leitura

        Assert.Equal(2, Run(g, new Alarmes(), tolerancia: 2).Eventos.Count);
        Assert.Single(Run(g, new Alarmes(), tolerancia: 4).Eventos);
    }

    [Fact]
    public void Placar_LeituraComErro_NaoContaComoAvaliadaNemAlarme()
    {
        var g = new Gabarito();
        var alarmes = new Alarmes().Add("TAG22", 5);
        alarmes.Erros.Add(("TAG22", 5));

        var r = Run(g, alarmes);

        Assert.Equal(1, r.LeiturasComErro);
        Assert.Equal(0, r.TotalAlarmes);
        Assert.Equal(Minutos - 1, r.PorTag.Single(t => t.Tag == "TAG22").Avaliados);
    }

    [Fact]
    public void Placar_GravaMarkdownECsv()
    {
        var g = new Gabarito();
        g.Falha("A", codigo: 10, alvo: 1, inicio: 60, fim: 89);
        var r = Run(g, new Alarmes().Add("TAG29", Range(64, 89)).Add("TAG28", 10));

        var arquivos = Cp300PlacarRelatorio.Gravar(r, Path.Combine(_root, "saida", "placar"));

        Assert.Equal(4, arquivos.Count);
        Assert.All(arquivos, a => Assert.True(File.Exists(a)));
        var md = File.ReadAllText(arquivos[0]);
        Assert.Contains("| A | 10 Desbalanceamento | 01/01 01:00 → 01:29 | B-01 |", md);
        Assert.Contains("**Sim** (26 min seguidos; confirmada às 01:06) | TAG29 | 01:04 | 4 |", md);
        Assert.Contains("Detecção (≥ 3 min)", md);
        Assert.Contains("Sinal breve (< 3 min)", md);
        var eventos = File.ReadAllLines(arquivos[1]);
        Assert.Equal(2, eventos.Length);
        Assert.StartsWith("A,10,Desbalanceamento,2026-01-01T01:00:00,2026-01-01T01:29:00,1,B-01,", eventos[1]);
        Assert.Contains("TAG28,180,150,1,0,0,1", File.ReadAllLines(arquivos[2]));
        Assert.Contains("0,360,1,TAG28=1", File.ReadAllLines(arquivos[3]));
    }

    [Fact]
    public void Mapa_RecusaRotuloComoTagAfetada()
    {
        var path = Path.Combine(_root, "mapa_rotulo.json");
        File.WriteAllText(path, """
            { "slots": [ { "nome": "A", "codigo": "TAG61", "alvo": "TAG62", "severidade": "TAG63", "fase": "TAG64" } ],
              "falhas": { "10": { "nome": "x", "tags": [ "TAG73" ] } } }
            """);

        var ex = Assert.Throws<InvalidDataException>(() => Cp300MapaFalhas.Load(path));
        Assert.Contains("rotulo", ex.Message);
    }

    [Fact]
    public void Mapa_DoRepositorio_ValidaECobreOsCodigosDoCenarios()
    {
        var mapa = Cp300MapaFalhas.Load(FindMapa());

        int[] codigosCenarios = [1, 2, 3, 4, 6, 7, 8, 9, 10, 12, 20, 21, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33];
        Assert.All(codigosCenarios, c => Assert.True(mapa.Falhas.ContainsKey(c.ToString(CultureInfo.InvariantCulture)), $"codigo {c}"));
        Assert.Equal(["A", "B", "C"], mapa.Slots.Select(s => s.Nome));

        // Preset 6 (CENARIOS §4): desbalanceamento na B-01 -> so a vibracao da B-01.
        var obs = new List<string>();
        Assert.Equal(["TAG29"], mapa.ResolverTags(mapa.Falhas["10"], 1, 1, TagsReplay, obs));
        // Preset 1: obstrucao com B-01 principal -> TAG27/31/33 e a auxiliar B-02.
        Assert.Equal(["TAG27", "TAG31", "TAG33", "TAG28", "TAG30", "TAG32"], mapa.ResolverTags(mapa.Falhas["2"], 1, 0, TagsReplay, obs));
        Assert.Empty(obs);
    }

    // ---------------------------------------------------------------- helpers

    private Cp300PlacarResultado Run(Gabarito g, Alarmes alarmes, int janelaAntes = 20, int tolerancia = 2, int duracaoMinima = 3)
    {
        var id = Guid.NewGuid().ToString("N");
        var mapa = Path.Combine(_root, "mapa.json");
        File.WriteAllText(mapa, MapaTeste);
        var gabarito = Path.Combine(_root, $"gabarito_{id}.csv");
        var replay = Path.Combine(_root, $"replay_{id}.csv");
        g.Write(gabarito);
        alarmes.Write(replay);
        return Cp300Placar.Calcular(new Cp300PlacarOptions
        {
            CsvReplay = replay,
            CsvGabarito = gabarito,
            MapaPath = mapa,
            JanelaAntesMinutos = janelaAntes,
            ToleranciaBuracoMinutos = tolerancia,
            DuracaoMinimaMinutos = duracaoMinima
        });
    }

    private static int[] Range(int a, int b) => Enumerable.Range(a, b - a + 1).ToArray();

    private static int Severidade(int minuto) => 4 + 10 * (minuto - 60);

    private sealed class Gabarito
    {
        private readonly Dictionary<string, SortedDictionary<int, double>> _series = new();

        public Gabarito(int principal = 1)
        {
            foreach (var tag in new[] { "TAG61", "TAG62", "TAG63", "TAG64", "TAG65", "TAG66", "TAG67", "TAG68", "TAG73" })
                for (var m = 0; m < Minutos; m++)
                    Set(tag, m, 0);
            for (var m = 0; m < Minutos; m++)
                Set("TAG44", m, principal);
        }

        public void Set(string tag, int minuto, double valor)
        {
            if (!_series.TryGetValue(tag, out var s))
                _series[tag] = s = new SortedDictionary<int, double>();
            s[minuto] = valor;
        }

        public void Remove(string[] tags, int de, int ate)
        {
            foreach (var tag in tags)
                for (var m = de; m <= ate; m++)
                    _series[tag].Remove(m);
        }

        /// <summary>Severidade = 4 + 10/min desde o inicio do slot (fase 2, rampa).</summary>
        public void Falha(string slot, int codigo, int alvo, int inicio, int fim)
        {
            var b = slot == "A" ? 61 : 65;
            for (var m = inicio; m <= fim; m++)
            {
                Set($"TAG{b}", m, codigo);
                Set($"TAG{b + 1}", m, alvo);
                Set($"TAG{b + 2}", m, 4 + 10 * (m - inicio));
                Set($"TAG{b + 3}", m, 2);
                Set("TAG73", m, 1);
            }
        }

        public void Write(string path)
        {
            var sb = new StringBuilder("ClienteId,TagName,DataHora,Valor\n");
            foreach (var (tag, s) in _series)
                foreach (var (m, v) in s)
                    sb.Append($"PK2,{tag},{T0.AddMinutes(m):yyyy-MM-dd'T'HH:mm:ss},{v.ToString(CultureInfo.InvariantCulture)}\n");
            File.WriteAllText(path, sb.ToString());
        }
    }

    private sealed class Alarmes
    {
        private readonly HashSet<(string Tag, int Minuto)> _alarmes = [];
        public HashSet<(string Tag, int Minuto)> Erros { get; } = [];

        public Alarmes Add(string tag, params int[] minutos)
        {
            foreach (var m in minutos)
                _alarmes.Add((tag, m));
            return this;
        }

        /// <summary>Formato do replay: varias linhas (detectores) por leitura, decisao repetida.</summary>
        public void Write(string path)
        {
            var sb = new StringBuilder("data_hora,cliente,tag,valor,detector,decisao_final,erro\n");
            for (var m = 0; m < Minutos; m++)
                foreach (var tag in TagsReplay)
                {
                    var dh = T0.AddMinutes(m).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                    if (Erros.Contains((tag, m)))
                    {
                        sb.Append($"{dh},PK2,{tag},1,,,\"historico com menos de 10 pontos, erro\"\n");
                        continue;
                    }

                    var d = _alarmes.Contains((tag, m)) ? "1" : "0";
                    sb.Append($"{dh},PK2,{tag},1,Z-Score,{d},\n");
                    sb.Append($"{dh},PK2,{tag},1,MAD,{d},\n");
                }

            File.WriteAllText(path, sb.ToString());
        }
    }

    private static string FindMapa([CallerFilePath] string arquivoFonte = "")
    {
        static DirectoryInfo? Raiz(string? inicio)
        {
            var dir = string.IsNullOrEmpty(inicio) ? null : new DirectoryInfo(inicio);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IAApi.csproj")))
                dir = dir.Parent;
            return dir;
        }

        var raiz = Raiz(AppContext.BaseDirectory) ?? Raiz(Path.GetDirectoryName(arquivoFonte))
            ?? throw new InvalidOperationException("Raiz do repo nao encontrada.");
        return Path.Combine(raiz.FullName, "tools", "Cp300OfflineReplay", Cp300MapaFalhas.ArquivoPadrao);
    }
}
