using System.Globalization;
using IAApi.Cp300OfflineReplay;

// Replay offline CP300: nao inicia a API, nao consulta o Bridge, nao abre o
// banco operacional (ia_config.db) e nao altera configuracao.
// Subcomando "placar": so le CSVs (replay + gabarito) e o mapa JSON.
if (args.Length > 0 && string.Equals(args[0], "placar", StringComparison.OrdinalIgnoreCase))
    return Cp300PlacarCli.Run(args[1..]);
return await Cp300ReplayCli.RunAsync(args);

internal static class Cp300PlacarCli
{
    private const string Usage =
        "Uso: dotnet run --project tools/Cp300OfflineReplay -- placar " +
        "--replay <replay.csv> --gabarito <dados.csv> --saida <prefixo> " +
        "[--mapa placar_falhas_cp300.json] [--janela-antes 20] [--duracao-minima 3] [--tolerancia-buraco 2] [--cliente PK2]";

    public static int Run(string[] args)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] valued = ["replay", "gabarito", "saida", "mapa", "janela-antes", "duracao-minima", "tolerancia-buraco", "cliente"];
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i].StartsWith("--", StringComparison.Ordinal) ? args[i][2..] : null;
            if (name is null || !valued.Contains(name, StringComparer.OrdinalIgnoreCase) || i + 1 >= args.Length)
            {
                Console.Error.WriteLine(name is null ? $"Argumento inesperado: {args[i]}" : $"Opcao invalida ou sem valor: {args[i]}");
                Console.Error.WriteLine(Usage);
                return 2;
            }

            parsed[name] = args[++i];
        }

        if (!parsed.ContainsKey("replay") || !parsed.ContainsKey("gabarito") || !parsed.ContainsKey("saida"))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        try
        {
            var options = new Cp300PlacarOptions
            {
                CsvReplay = parsed["replay"],
                CsvGabarito = parsed["gabarito"],
                MapaPath = parsed.GetValueOrDefault("mapa"),
                JanelaAntesMinutos = parsed.TryGetValue("janela-antes", out var n)
                    ? int.Parse(n, NumberStyles.Integer, CultureInfo.InvariantCulture) : 20,
                DuracaoMinimaMinutos = parsed.TryGetValue("duracao-minima", out var dur)
                    ? int.Parse(dur, NumberStyles.Integer, CultureInfo.InvariantCulture) : 3,
                ToleranciaBuracoMinutos = parsed.TryGetValue("tolerancia-buraco", out var tol)
                    ? int.Parse(tol, NumberStyles.Integer, CultureInfo.InvariantCulture) : 2,
                ClienteId = parsed.GetValueOrDefault("cliente")
            };
            var resultado = Cp300Placar.Calcular(options);
            foreach (var aviso in resultado.Avisos)
                Console.WriteLine("AVISO " + aviso);
            foreach (var e in resultado.Eventos)
                Console.WriteLine(
                    $"Evento {e.Slot} {e.Codigo:00} {e.Inicio:yyyy-MM-dd HH:mm}-{e.Fim:HH:mm}: " +
                    (e.Detectado
                        ? $"detectado {e.PrimeiroAlarme:HH:mm} em {string.Join("+", e.TagsDeteccao)} ({e.DuracaoDeteccao} min seguidos), atraso {e.AtrasoMinutos} min, {e.SeveridadeNaDeteccao} permil"
                        : "nao detectado") +
                    (e.SinalBreve is { } breve
                        ? $" | sinal breve {breve:HH:mm} em {string.Join("+", e.TagsSinalBreve)} ({e.DuracaoSinalBreve} min), atraso {e.AtrasoSinalBreve} min, {e.SeveridadeSinalBreve} permil"
                        : ""));
            Console.WriteLine(
                $"Alarmes: {resultado.TotalAlarmes} = atribuidos {resultado.AlarmesAtribuidos} + coincidencias {resultado.AlarmesCoincidencia} + falsos fora {resultado.AlarmesFalsosFora}");
            foreach (var arquivo in Cp300PlacarRelatorio.Gravar(resultado, parsed["saida"]))
                Console.WriteLine("Saida: " + arquivo);
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(Usage);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PLACAR CP300 FALHOU: {ex.Message}");
            return 1;
        }
    }
}

internal static class Cp300ReplayCli
{
    private const string Usage =
        "Uso: dotnet run --project tools/Cp300OfflineReplay -- " +
        "--entrada <historico.csv> --saida <replay.csv> " +
        "(--treino-ate <yyyy-MM-ddTHH:mm:ss> | --treino-inicio <data> --treino-fim <data> [--inicio <data>]) " +
        "[--fim <yyyy-MM-ddTHH:mm:ss>] [--cliente PK2] [--tags TAG22,TAG23,...] " +
        "[--appsettings appsettings.json] [--horas-historico N] [--historico-inclui-atual] [--historico-causal] " +
        "[--regime TAGx=TAGy]... [--permitir-falha-no-treino] [--verbose]";

    public static async Task<int> RunAsync(string[] args)
    {
        Dictionary<string, string?> parsed;
        IReadOnlyDictionary<string, string> regimes;
        try
        {
            var regimeArgs = new List<string>();
            parsed = Parse(args, regimeArgs);
            regimes = Cp300Replay.ParseRegimes(regimeArgs);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        if (!parsed.TryGetValue("entrada", out var entrada) || entrada is null ||
            !parsed.TryGetValue("saida", out var saida) || saida is null ||
            (!parsed.ContainsKey("treino-ate") && !parsed.ContainsKey("treino-inicio") && !parsed.ContainsKey("treino-fim")))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var stdout = Console.Out;
        var verbose = parsed.ContainsKey("verbose");
        try
        {
            var options = new Cp300ReplayOptions
            {
                CsvEntrada = entrada,
                CsvSaida = saida,
                InstanteTreino = OptionalDate(parsed, "treino-ate"),
                TreinoInicio = OptionalDate(parsed, "treino-inicio"),
                TreinoFim = OptionalDate(parsed, "treino-fim"),
                InicioReplay = OptionalDate(parsed, "inicio"),
                PermitirFalhaNoTreino = parsed.ContainsKey("permitir-falha-no-treino"),
                FimReplay = parsed.TryGetValue("fim", out var fim) && fim is not null ? ParseDate(fim) : null,
                ClienteId = parsed.GetValueOrDefault("cliente"),
                Tags = parsed.TryGetValue("tags", out var tags) && tags is not null
                    ? tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : null,
                AppSettingsPath = parsed.GetValueOrDefault("appsettings") ?? "appsettings.json",
                HorasHistorico = parsed.TryGetValue("horas-historico", out var horas) && horas is not null
                    ? int.Parse(horas, NumberStyles.Integer, CultureInfo.InvariantCulture)
                    : null,
                HistoricoIncluiLeituraAtual = parsed.ContainsKey("historico-inclui-atual"),
                HistoricoCausal = parsed.ContainsKey("historico-causal"),
                Regimes = regimes,
                Log = stdout
            };

            // O runtime escreve muito no console (BASELINE, HISTORICO...).
            // Sem --verbose, so as mensagens da ferramenta aparecem.
            if (!verbose)
                Console.SetOut(TextWriter.Null);

            var summary = await Cp300Replay.RunAsync(options);
            foreach (var ignorada in summary.TagsIgnoradas)
                stdout.WriteLine("Ignorada: " + ignorada);
            stdout.WriteLine($"Saida: {Path.GetFullPath(saida)}");
            return summary.Divergencias == 0 ? 0 : 1;
        }
        catch (ArgumentException ex)
        {
            // Combinacao invalida de opcoes (ex.: --treino-ate com --treino-inicio).
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(Usage);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"REPLAY CP300 FALHOU: {ex.Message}");
            return 1;
        }
        finally
        {
            Console.SetOut(stdout);
        }
    }

    private static DateTime? OptionalDate(Dictionary<string, string?> parsed, string name) =>
        parsed.TryGetValue(name, out var text) && text is not null ? ParseDate(text) : null;

    private static DateTime ParseDate(string text) =>
        DateTime.ParseExact(text, ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm:ss"],
            CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static Dictionary<string, string?> Parse(string[] args, List<string> regimeArgs)
    {
        string[] flags = ["historico-inclui-atual", "historico-causal", "verbose", "permitir-falha-no-treino"];
        string[] valued = ["entrada", "saida", "treino-ate", "treino-inicio", "treino-fim", "inicio", "fim", "cliente", "tags", "appsettings", "horas-historico"];
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Argumento inesperado: {args[i]}");
            var name = args[i][2..];
            if (string.Equals(name, "regime", StringComparison.OrdinalIgnoreCase))
            {
                // Repetivel: --regime TAG27=TAG42 --regime TAG28=TAG43 (ou pares separados por virgula).
                if (i + 1 >= args.Length)
                    throw new ArgumentException("Falta valor para --regime");
                regimeArgs.Add(args[++i]);
            }
            else if (flags.Contains(name, StringComparer.OrdinalIgnoreCase))
                result[name] = null;
            else if (valued.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Falta valor para --{name}");
                result[name] = args[++i];
            }
            else
                throw new ArgumentException($"Opcao desconhecida: --{name}");
        }

        return result;
    }
}
