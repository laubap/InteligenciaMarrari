using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using IAApi.Cp300OfflineReplay;

namespace IAApi.Tests.Cp300OfflineReplayTool;

/// <summary>
/// Replay offline CP300 sobre um CSV sintetico pequeno. Isolamento: SQLite em
/// memoria, historico servido do CSV, appsettings.json do repositorio lido
/// apenas para pesos/limiares. Nenhum acesso a Bridge, PSi4 ou banco real.
///
/// Serie: 1 leitura/min. Janela de historico reduzida a 2 h para o teste.
/// Treino [00:00, 02:00), replay [02:00, 02:59].
///   TAG22: continua com ruido decimal; degrau de +15 as 02:30.
///   TAG28: bomba reserva, 0 o tempo todo ate 02:40, depois liga (~25).
///   TAG24: continua com ruido; congelada a partir de 02:20.
///   TAG73: rotulo do simulador; nunca pode entrar no replay.
/// </summary>
public sealed class Cp300OfflineReplayTests : IDisposable
{
    private static readonly DateTime Inicio = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime T = Inicio.AddHours(2);
    private static readonly DateTime Degrau = T.AddMinutes(30);
    private static readonly DateTime ReservaLiga = T.AddMinutes(40);
    private static readonly DateTime Congela = T.AddMinutes(20);
    private const int TotalMinutos = 180;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "IAApi-Cp300Replay-Tests", Guid.NewGuid().ToString("N"));

    public Cp300OfflineReplayTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Replay_GeraSeisDetectoresPorLeitura_ConferidoContraPredictionService()
    {
        var (summary, rows) = await RunAsync();

        Assert.Equal(3, summary.TagsReproduzidas);
        Assert.Equal(3 * 60, summary.Leituras);
        Assert.Equal(0, summary.LeiturasComErro);
        Assert.Equal(0, summary.Divergencias);
        Assert.Empty(summary.TagsIgnoradas);

        // Rotulo do simulador nunca vira entrada.
        Assert.DoesNotContain(rows, r => r["tag"] == "TAG73");
        Assert.Equal(["TAG22", "TAG24", "TAG28"], rows.Select(r => r["tag"]).Distinct().Order().ToArray());

        foreach (var reading in rows.GroupBy(r => (r["tag"], r["data_hora"])))
        {
            Assert.Equal(6, reading.Count());
            Assert.All(reading, r => Assert.Equal("ok", r["conferencia"]));
            Assert.All(reading, r => Assert.Equal("0", r["entrada_invalida"]));
            Assert.All(reading, r => Assert.Equal("Global", r["origem_baseline"]));
            // Contribuicoes somam a nota final (pontos ponderados / soma dos pesos).
            var soma = reading.Sum(r => D(r["contribuicao"]));
            // CSV grava 9 digitos significativos; tolerancia de arredondamento.
            Assert.InRange(soma - D(reading.First()["nota_final"]), -1e-4, 1e-4);
        }

        Assert.Equal(6, rows.Select(r => r["detector"]).Distinct().Count());
    }

    [Fact]
    public async Task Replay_UsaPesosDoAppsettings_ELimiarDoEnsemble()
    {
        var (_, rows) = await RunAsync();
        var pesos = rows.GroupBy(r => r["detector"]).ToDictionary(g => g.Key, g => D(g.First()["peso"]));

        // appsettings.json: Z/MAD/EWMA 0,15; RoC/Persistence/LevelShift 0,10 (defaults do codigo diferem).
        Assert.Equal(3, pesos.Values.Count(p => Math.Abs(p - 0.15) < 1e-9));
        Assert.Equal(3, pesos.Values.Count(p => Math.Abs(p - 0.10) < 1e-9));
        Assert.All(rows, r => Assert.Equal(60, D(r["limiar"])));
    }

    [Fact]
    public async Task Replay_HistoricoDeDeteccao_EJanelaDeTempoAnteriorALeitura()
    {
        var (_, semAtual) = await RunAsync();
        Assert.All(semAtual, r => Assert.Equal("120", r["pontos_historico"]));

        var (_, comAtual) = await RunAsync(incluiAtual: true);
        Assert.All(comAtual, r => Assert.Equal("121", r["pontos_historico"]));
    }

    [Fact]
    public async Task TagContinua_ComDegrau_DisparaAnomaliaNoDegrau()
    {
        var (_, rows) = await RunAsync();
        var tag = rows.Where(r => r["tag"] == "TAG22").ToList();

        Assert.All(tag, r => Assert.NotEqual("Discrete", r["tipo_tag"]));
        Assert.All(tag, r => Assert.Equal("profile", r["origem_deadband"]));

        var noDegrau = tag.Where(r => At(r) >= Degrau && At(r) < Degrau.AddMinutes(5)).ToList();
        Assert.Contains(noDegrau, r => r["decisao_final"] == "1");
        // No primeiro minuto do degrau o Z-Score satura.
        var z = noDegrau.Single(r => At(r) == Degrau && r["detector"] == "Z-Score");
        Assert.Equal("1", z["aplicavel"]);
        Assert.Equal(100, D(z["nota_detector"]));
    }

    [Fact]
    public async Task BombaReservaEmZero_ClassificadaDiscreta_BloqueiaTodosOsDetectores()
    {
        var (_, rows) = await RunAsync();
        var parada = rows.Where(r => r["tag"] == "TAG28" && At(r) < ReservaLiga).ToList();

        Assert.NotEmpty(parada);
        Assert.All(parada, r =>
        {
            Assert.Equal("Discrete", r["tipo_tag"]);
            Assert.True(D(r["confianca_tipo"]) >= 0.85);
            Assert.Equal("1", r["roteamento_por_tipo"]);
            Assert.Equal("0", r["aplicavel"]);
            Assert.Equal("BlockedByApplicabilityPolicy", r["motivo_nao_aplicavel"]);
            Assert.Equal(0, D(r["nota_final"]));
            Assert.Equal("0", r["decisao_final"]);
            // Perfil treinado so com zeros: media 0 / desvio 0 / deadband 0.
            Assert.Equal(0, D(r["media"]));
            Assert.Equal(0, D(r["desvio"]));
            Assert.Equal("relative-floor", r["origem_deadband"]);
        });
    }

    [Fact]
    public async Task ValorCongelado_ReportaDetectoresEBaseline()
    {
        var (_, rows) = await RunAsync();
        var congelado = rows.Where(r => r["tag"] == "TAG24" && At(r) >= Congela).ToList();

        Assert.Equal(40 * 6, congelado.Count);
        Assert.Single(congelado.Select(r => r["valor"]).Distinct());
        Assert.All(congelado, r =>
        {
            Assert.Equal("profile", r["origem_deadband"]);
            Assert.True(D(r["deadband"]) > 0);
            Assert.True(D(r["desvio"]) > 0);
            Assert.False(string.IsNullOrEmpty(r["tipo_tag"]));
            Assert.Equal("", r["erro"]);
        });
    }

    [Fact]
    public async Task RotuloPedidoExplicitamente_ERecusado()
    {
        var input = WriteCsv();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Cp300Replay.RunAsync(
            Options(input, Path.Combine(_root, "x.csv"), tags: ["TAG73"])));
        Assert.Contains("rotulo", ex.Message);
    }

    [Fact]
    public async Task Regime_AplicaBaselinePorEstado_ConferidoContraPredictionService()
    {
        // TAG30 alterna entre parada (0) e ligada (~100) em blocos de 15 min;
        // TAG43 informa o estado (0 = parada, 2 = operando).
        var input = WriteRegimeCsv();
        var output = Path.Combine(_root, $"regime-{Guid.NewGuid():N}.csv");
        var regimes = Cp300Replay.ParseRegimes(["TAG30=TAG43"]);

        var summary = await Cp300Replay.RunAsync(Options(input, output, tags: ["TAG30"], regimes: regimes));
        var rows = ReadOutput(output);

        Assert.Equal(60, summary.Leituras);
        Assert.Equal(0, summary.LeiturasComErro);
        Assert.Equal(0, summary.Divergencias);
        Assert.All(rows, r =>
        {
            Assert.Equal("ok", r["conferencia"]);
            Assert.Equal("OperationalRegime", r["origem_baseline"]);
            var estado = EstadoRegime(At(r));
            Assert.Equal(estado, D(r["valor_regime"]));
            // Media do regime: ~0 com a bomba parada, ~100 com ela ligada.
            if (estado == 0)
                Assert.InRange(D(r["media"]), -1, 1);
            else
                Assert.InRange(D(r["media"]), 95, 105);
        });

        // Sem --regime, o mesmo CSV continua no baseline global.
        var semRegime = Path.Combine(_root, $"sem-regime-{Guid.NewGuid():N}.csv");
        await Cp300Replay.RunAsync(Options(input, semRegime, tags: ["TAG30"]));
        Assert.All(ReadOutput(semRegime), r =>
        {
            Assert.Equal("Global", r["origem_baseline"]);
            Assert.Equal("", r["valor_regime"]);
        });
    }

    [Fact]
    public async Task Regime_RecusaRotuloComoTagDeRegime_ETagDeSiMesma()
    {
        var input = WriteRegimeCsv();
        var output = Path.Combine(_root, "x.csv");

        var rotulo = await Assert.ThrowsAsync<InvalidOperationException>(() => Cp300Replay.RunAsync(
            Options(input, output, tags: ["TAG30"], regimes: Cp300Replay.ParseRegimes(["TAG30=TAG73"]))));
        Assert.Contains("rotulo", rotulo.Message);
        Assert.Contains("TAG73", rotulo.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Cp300Replay.RunAsync(
            Options(input, output, tags: ["TAG30"], regimes: Cp300Replay.ParseRegimes(["TAG30=TAG30"]))));

        Assert.Throws<ArgumentException>(() => Cp300Replay.ParseRegimes(["TAG30"]));
        Assert.Throws<ArgumentException>(() => Cp300Replay.ParseRegimes(["TAG30=TAG43", "TAG30=TAG42"]));
        Assert.Equal(2, Cp300Replay.ParseRegimes(["TAG27=TAG42,TAG28=TAG43"]).Count);
    }

    // --- Treino por periodo e trava de TAG73 --------------------------------

    [Fact]
    public async Task TreinoPorPeriodo_IgualAo24hAntesDeT_GeraSaidaIdentica()
    {
        var input = WriteCsv();
        var ate = Path.Combine(_root, $"ate-{Guid.NewGuid():N}.csv");
        var periodo = Path.Combine(_root, $"periodo-{Guid.NewGuid():N}.csv");

        var sAte = await Cp300Replay.RunAsync(Options(input, ate));
        var sPeriodo = await Cp300Replay.RunAsync(Options(input, periodo) with
        {
            InstanteTreino = null, TreinoInicio = Inicio, TreinoFim = T
        });

        // [T - 2 h, T) por periodo = --treino-ate T com janela de 2 h.
        Assert.Equal(File.ReadAllText(ate), File.ReadAllText(periodo));
        Assert.Equal(sAte.PontosTreino, sPeriodo.PontosTreino);
        Assert.All(sPeriodo.PontosTreino.Values, n => Assert.Equal(120, n));
        Assert.Equal(Inicio, sPeriodo.TreinoInicio);
        Assert.Equal(T, sPeriodo.TreinoFim);
        Assert.Equal(T, sPeriodo.InicioReplay);
        Assert.Equal(0, sPeriodo.MinutosFalhaNoTreino);
    }

    [Fact]
    public async Task TreinoPorPeriodo_UsaSoOIntervalo_ERelataPontosPorTag()
    {
        var input = WriteCsv();
        var output = Path.Combine(_root, $"curto-{Guid.NewGuid():N}.csv");
        var log = new StringWriter();
        var curtoInicio = Inicio.AddMinutes(30);
        var curtoFim = Inicio.AddMinutes(90);

        var summary = await Cp300Replay.RunAsync(Options(input, output) with
        {
            InstanteTreino = null, TreinoInicio = curtoInicio, TreinoFim = curtoFim, InicioReplay = T, Log = log
        });

        Assert.All(summary.PontosTreino.Values, n => Assert.Equal(60, n));
        Assert.Equal(60 * 3, summary.Leituras);
        var texto = log.ToString();
        Assert.Contains("periodo escolhido", texto);
        Assert.Contains($"[{curtoInicio:O}, {curtoFim:O})", texto);
        Assert.Contains("Perfil TAG22: pontos_treino=60", texto);

        // O perfil muda com o periodo: a media global da TAG22 difere do treino de 2 h.
        var (_, padrao) = await RunAsync();
        var mediaPeriodo = D(ReadOutput(output).First(r => r["tag"] == "TAG22")["media"]);
        var mediaPadrao = D(padrao.First(r => r["tag"] == "TAG22")["media"]);
        Assert.NotEqual(mediaPadrao, mediaPeriodo);
    }

    [Fact]
    public async Task TravaTag73_RecusaTreinoComFalha_EPermiteComOpcaoExplicita()
    {
        // Falha rotulada de 01:00 a 01:09 (10 min) dentro do treino.
        var input = WriteCsv("falha-no-treino.csv", t => t >= Inicio.AddHours(1) && t < Inicio.AddMinutes(70));

        var recusaAte = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Cp300Replay.RunAsync(Options(input, Path.Combine(_root, "x1.csv"))));
        Assert.Contains("TAG73", recusaAte.Message);
        Assert.Contains("--permitir-falha-no-treino", recusaAte.Message);
        Assert.Contains("10 min", recusaAte.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Cp300Replay.RunAsync(
            Options(input, Path.Combine(_root, "x2.csv")) with
            {
                InstanteTreino = null, TreinoInicio = Inicio, TreinoFim = T
            }));

        var log = new StringWriter();
        var forcado = await Cp300Replay.RunAsync(Options(input, Path.Combine(_root, "x3.csv")) with
        {
            PermitirFalhaNoTreino = true, Log = log
        });
        Assert.Equal(10, forcado.MinutosFalhaNoTreino);
        Assert.Contains("AVISO Trava TAG73", log.ToString());

        // Periodo que evita a falha: aceito sem a opcao.
        var limpo = await Cp300Replay.RunAsync(Options(input, Path.Combine(_root, "x4.csv")) with
        {
            InstanteTreino = null, TreinoInicio = Inicio.AddMinutes(70), TreinoFim = T
        });
        Assert.Equal(0, limpo.MinutosFalhaNoTreino);
        Assert.All(limpo.PontosTreino.Values, n => Assert.Equal(50, n));
    }

    [Fact]
    public async Task TravaTag73_FalhaDentroDoReplay_NaoBloqueia()
    {
        // Falha so depois de T (e o cenario em teste): treino limpo, replay roda.
        var input = WriteCsv("falha-no-replay.csv", t => t >= T.AddMinutes(10) && t < T.AddMinutes(20));
        var summary = await Cp300Replay.RunAsync(Options(input, Path.Combine(_root, "x.csv")));
        Assert.Equal(0, summary.MinutosFalhaNoTreino);
        Assert.Equal(60 * 3, summary.Leituras);
    }

    [Fact]
    public async Task TravaTag73_SemTag73NoCsv_AvisaENaoVerifica()
    {
        var input = WriteCsv("sem-rotulo.csv", _ => false, comRotulo: false);
        var log = new StringWriter();
        var summary = await Cp300Replay.RunAsync(Options(input, Path.Combine(_root, "x.csv")) with { Log = log });
        Assert.Null(summary.MinutosFalhaNoTreino);
        Assert.Contains("TAG73 ausente no CSV, nao verificada", log.ToString());
    }

    [Fact]
    public async Task Regime_UsaJanelaMovelDeProducao_ComTravaNaParteAnteriorAoReplay()
    {
        // Treino por periodo [00:00, 00:50) limpo; falha rotulada em 01:20-01:24,
        // fora do treino mas dentro da janela movel [T - 2 h, T) usada pelo regime.
        var input = WriteRegimeCsv("regime-falha-janela.csv",
            t => t >= Inicio.AddMinutes(80) && t < Inicio.AddMinutes(85));
        var regimes = Cp300Replay.ParseRegimes(["TAG30=TAG43"]);
        Cp300ReplayOptions Base(string nome) => Options(input, Path.Combine(_root, nome), tags: ["TAG30"]) with
        {
            InstanteTreino = null, TreinoInicio = Inicio, TreinoFim = Inicio.AddMinutes(50), InicioReplay = T
        };

        // Sem --regime: so o treino e verificado, e ele esta limpo.
        var semRegime = await Cp300Replay.RunAsync(Base("sem.csv"));
        Assert.Equal(0, semRegime.MinutosFalhaNoTreino);
        Assert.Null(semRegime.MinutosFalhaNaJanelaDoRegime);

        // Com --regime: a janela movel do regime contem a falha -> recusado.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Cp300Replay.RunAsync(Base("com.csv") with { Regimes = regimes }));
        Assert.Contains("janela movel do regime", ex.Message);
        Assert.Contains("5 min", ex.Message);

        // Forcado: roda com o comportamento de producao e registra o periodo efetivo.
        var log = new StringWriter();
        var output = Path.Combine(_root, $"forcado-{Guid.NewGuid():N}.csv");
        var forcado = await Cp300Replay.RunAsync(Base("y.csv") with
        {
            CsvSaida = output, Regimes = regimes, PermitirFalhaNoTreino = true, Log = log
        });
        Assert.Equal(5, forcado.MinutosFalhaNaJanelaDoRegime);
        var texto = log.ToString();
        Assert.Contains("baseline movel de 2 h (producao)", texto);
        Assert.Contains($"dados anteriores ao replay usados: [{Inicio:O}, {T:O})", texto);
        Assert.Contains("Regime TAG30 <- TAG43, pontos por estado no treino: 0=30 2=20", texto);
        Assert.Contains("Regime TAG30 <- TAG43, pontos por estado na primeira janela movel: 0=60 2=60", texto);
        Assert.Contains(ReadOutput(output), r => r["origem_baseline"] == "OperationalRegime");
    }

    [Fact]
    public async Task TreinoPorPeriodo_RecusaCombinacoesInvalidas()
    {
        var input = WriteCsv();
        var output = Path.Combine(_root, "x.csv");

        // --treino-ate junto com periodo.
        await Assert.ThrowsAsync<ArgumentException>(() => Cp300Replay.RunAsync(
            Options(input, output) with { TreinoInicio = Inicio, TreinoFim = T }));
        // Periodo incompleto.
        await Assert.ThrowsAsync<ArgumentException>(() => Cp300Replay.RunAsync(
            Options(input, output) with { InstanteTreino = null, TreinoInicio = Inicio }));
        // Fim antes do inicio.
        await Assert.ThrowsAsync<ArgumentException>(() => Cp300Replay.RunAsync(
            Options(input, output) with { InstanteTreino = null, TreinoInicio = T, TreinoFim = Inicio }));
        // Replay comecando dentro do treino.
        await Assert.ThrowsAsync<ArgumentException>(() => Cp300Replay.RunAsync(
            Options(input, output) with
            {
                InstanteTreino = null, TreinoInicio = Inicio, TreinoFim = T, InicioReplay = T.AddMinutes(-1)
            }));
        // --inicio so com periodo.
        await Assert.ThrowsAsync<ArgumentException>(() => Cp300Replay.RunAsync(
            Options(input, output) with { InicioReplay = T }));
        // Nenhum modo.
        await Assert.ThrowsAsync<ArgumentException>(() => Cp300Replay.RunAsync(
            Options(input, output) with { InstanteTreino = null }));
    }

    // --- RC-2: regressao golden ----------------------------------------------

    private const string GoldenArquivo = "replay_bombas_regime.csv";
    private const string VariavelAtualizarGolden = "IAAPI_ATUALIZAR_GOLDEN";

    /// <summary>
    /// Saida completa do replay (bomba, outra bomba e set-point, com --regime
    /// simples) comparada com a referencia versionada em Golden/. Qualquer
    /// mudanca de decisao, nota, baseline ou formato quebra o teste.
    /// Comparacao exata do texto; so o fim de linha (CRLF/LF, por causa do
    /// core.autocrlf) e o BOM sao normalizados.
    /// Para regravar a referencia de proposito: IAAPI_ATUALIZAR_GOLDEN=1
    /// (o teste grava o arquivo e falha, para nao passar sem revisao).
    /// P31: com --historico-causal a saida tem de ser a mesma referencia
    /// (o CSV ja serve so pontos anteriores a leitura; o recorte nao remove nada).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Golden_BombasComRegime_SaidaIdenticaAReferencia(bool historicoCausal)
    {
        var input = WriteBombasCsv();
        var output = Path.Combine(_root, $"golden-{Guid.NewGuid():N}.csv");
        var regimes = Cp300Replay.ParseRegimes(["TAG29=TAG42", "TAG30=TAG43"]);

        var summary = await Cp300Replay.RunAsync(
            Options(input, output, tags: ["TAG29", "TAG30"], regimes: regimes) with
            {
                FimReplay = T.AddMinutes(29),
                HistoricoCausal = historicoCausal
            });

        Assert.Equal(2 * 30, summary.Leituras);
        Assert.Equal(0, summary.LeiturasComErro);
        Assert.Equal(0, summary.Divergencias);

        var atual = Normalizar(File.ReadAllText(output, Encoding.UTF8));
        var golden = GoldenPath();
        // A referencia so e regravada a partir da execucao sem a chave.
        if (!historicoCausal && Environment.GetEnvironmentVariable(VariavelAtualizarGolden) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(golden)!);
            File.WriteAllText(golden, atual, new UTF8Encoding(false));
            Assert.Fail($"Golden regravado em {golden}. Revise o diff e rode de novo sem {VariavelAtualizarGolden}.");
        }

        Assert.True(File.Exists(golden), $"Golden ausente: {golden} (gerar com {VariavelAtualizarGolden}=1).");
        var esperado = Normalizar(File.ReadAllText(golden, Encoding.UTF8));

        // O cenario cobre os dois caminhos do baseline e decisoes nos dois sentidos.
        var rows = ReadOutput(output);
        Assert.Contains(rows, r => r["origem_baseline"] == "OperationalRegime");
        Assert.Contains(rows, r => r["decisao_final"] == "1");
        Assert.Contains(rows, r => r["decisao_final"] == "0");

        if (atual != esperado)
        {
            var a = esperado.Split('\n');
            var b = atual.Split('\n');
            var i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i])
                i++;
            Assert.Fail($"Saida difere do golden na linha {i + 1} ({a.Length} x {b.Length} linhas).\n" +
                        $"esperado: {(i < a.Length ? a[i] : "<fim>")}\natual:    {(i < b.Length ? b[i] : "<fim>")}");
        }
    }

    private static string Normalizar(string texto) => texto.Replace("\r\n", "\n");

    private static string GoldenPath([CallerFilePath] string arquivoFonte = "") =>
        Path.Combine(Path.GetDirectoryName(arquivoFonte)!, "Golden", GoldenArquivo);

    /// <summary>
    /// B-01 (TAG29, estado TAG42) e B-02 (TAG30, estado TAG43), set-point TAG46.
    /// Ciclo de 60 min: B-01 sozinha (20), as duas (20), B-02 sozinha (20).
    /// Set-point alterna 450/380 a cada 45 min. Corrente ~ set-point/4 sozinha,
    /// metade disso em paralelo, 0 parada. TAG29 tem pico de +100 de T+15 a T+19.
    /// </summary>
    private string WriteBombasCsv()
    {
        var path = Path.Combine(_root, "historico-bombas.csv");
        var sb = new StringBuilder("ClienteId,TagName,DataHora,Valor\n");
        for (var i = 0; i < TotalMinutos; i++)
        {
            var t = Inicio.AddMinutes(i);
            var ts = t.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            var fase = i % 60 / 20; // 0 = B-01 sozinha, 1 = as duas, 2 = B-02 sozinha
            var b01 = fase < 2 ? 2 : 0;
            var b02 = fase > 0 ? 2 : 0;
            var setPoint = i / 45 % 2 == 0 ? 450 : 380;
            var porBomba = fase == 1 ? setPoint / 8.0 : setPoint / 4.0;
            var falha = t >= T.AddMinutes(15) && t < T.AddMinutes(20) ? 100 : 0;

            Append(sb, "TAG29", ts, b01 == 2 ? porBomba + falha + Ruido(i, 2) : 0);
            Append(sb, "TAG30", ts, b02 == 2 ? porBomba + Ruido(i, 5) : 0);
            Append(sb, "TAG42", ts, b01);
            Append(sb, "TAG43", ts, b02);
            Append(sb, "TAG46", ts, setPoint);
            Append(sb, "TAG73", ts, 0);
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    // ---------------------------------------------------------------------

    private static double EstadoRegime(DateTime t) =>
        (int)(t - Inicio).TotalMinutes / 15 % 2 == 0 ? 0 : 2;

    private string WriteRegimeCsv() => WriteRegimeCsv("historico-regime.csv", _ => false);

    private string WriteRegimeCsv(string nome, Func<DateTime, bool> falha)
    {
        var path = Path.Combine(_root, nome);
        if (File.Exists(path))
            return path;

        var sb = new StringBuilder("ClienteId,TagName,DataHora,Valor\n");
        for (var i = 0; i < TotalMinutos; i++)
        {
            var t = Inicio.AddMinutes(i);
            var ts = t.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            var estado = EstadoRegime(t);
            Append(sb, "TAG30", ts, estado == 0 ? 0 : 100 + 3 * Ruido(i, 1));
            Append(sb, "TAG43", ts, estado);
            Append(sb, "TAG73", ts, falha(t) ? 1 : 0);
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    private async Task<(Cp300ReplaySummary Summary, List<Dictionary<string, string>> Rows)> RunAsync(
        bool incluiAtual = false)
    {
        var input = WriteCsv();
        var output = Path.Combine(_root, $"saida-{Guid.NewGuid():N}.csv");
        var summary = await Cp300Replay.RunAsync(Options(input, output, incluiAtual: incluiAtual));
        return (summary, ReadOutput(output));
    }

    private static Cp300ReplayOptions Options(string input, string output,
        bool incluiAtual = false, string[]? tags = null,
        IReadOnlyDictionary<string, string>? regimes = null) => new()
    {
        Regimes = regimes,
        CsvEntrada = input,
        CsvSaida = output,
        InstanteTreino = T,
        FimReplay = T.AddMinutes(59),
        AppSettingsPath = FindAppSettings(),
        HorasHistorico = 2,
        HistoricoIncluiLeituraAtual = incluiAtual,
        Tags = tags
    };

    private string WriteCsv() => WriteCsv("historico.csv", _ => false);

    /// <summary>Mesma serie de WriteCsv; TAG73 = 1 onde falha(t), ou sem TAG73.</summary>
    private string WriteCsv(string nome, Func<DateTime, bool> falha, bool comRotulo = true)
    {
        var path = Path.Combine(_root, nome);
        if (File.Exists(path))
            return path;

        var sb = new StringBuilder("ClienteId,TagName,DataHora,Valor\n");
        var congelado = 0.0;
        for (var i = 0; i < TotalMinutos; i++)
        {
            var t = Inicio.AddMinutes(i);
            var ts = t.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

            var degrau = 50 + Ruido(i, 0) + (t >= Degrau ? 15 : 0);
            var reserva = t >= ReservaLiga ? 25 + Ruido(i, 3) : 0;
            if (t < Congela)
                congelado = 300 + 2 * Ruido(i, 7);

            Append(sb, "TAG22", ts, degrau);
            Append(sb, "TAG28", ts, reserva);
            Append(sb, "TAG24", ts, congelado);
            if (comRotulo)
                Append(sb, "TAG73", ts, falha(t) ? 1 : 0);
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }

    // Ruido deterministico, decimal, amplitude ~1.
    private static double Ruido(int i, int fase) =>
        Math.Round(0.6 * Math.Sin(0.7 * i + fase) + 0.4 * Math.Sin(1.9 * i + 2 * fase), 3);

    private static void Append(StringBuilder sb, string tag, string ts, double valor) =>
        sb.Append("PK2,").Append(tag).Append(',').Append(ts).Append(',')
          .Append(valor.ToString(CultureInfo.InvariantCulture)).Append('\n');

    private static List<Dictionary<string, string>> ReadOutput(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var header = lines[0].Split(',');
        Assert.Equal(Cp300Replay.Colunas, header);
        return lines.Skip(1).Select(line =>
        {
            var fields = SplitCsv(line);
            Assert.Equal(header.Length, fields.Count);
            return header.Zip(fields).ToDictionary(p => p.First, p => p.Second);
        }).ToList();
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static DateTime At(Dictionary<string, string> row) =>
        DateTime.ParseExact(row["data_hora"], "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static double D(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    private static string FindAppSettings([CallerFilePath] string arquivoFonte = "")
    {
        // Procura a raiz a partir da saida do build e, se ela estiver fora do
        // repositorio (ex.: dotnet test --artifacts-path), a partir deste arquivo.
        static DirectoryInfo? Raiz(string? inicio)
        {
            var dir = string.IsNullOrEmpty(inicio) ? null : new DirectoryInfo(inicio);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IAApi.csproj")))
                dir = dir.Parent;
            return dir;
        }

        var raiz = Raiz(AppContext.BaseDirectory) ?? Raiz(Path.GetDirectoryName(arquivoFonte))
            ?? throw new InvalidOperationException("Raiz do repo nao encontrada.");
        return Path.Combine(raiz.FullName, "appsettings.json");
    }
}
