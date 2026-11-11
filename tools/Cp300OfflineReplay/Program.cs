using System.Globalization;
using IAApi.Cp300OfflineReplay;

// Replay offline CP300: nao inicia a API, nao consulta o Bridge, nao abre o
// banco operacional (ia_config.db) e nao altera configuracao.
return await Cp300ReplayCli.RunAsync(args);

internal static class Cp300ReplayCli
{
    private const string Usage =
        "Uso: dotnet run --project tools/Cp300OfflineReplay -- " +
        "--entrada <historico.csv> --saida <replay.csv> --treino-ate <yyyy-MM-ddTHH:mm:ss> " +
        "[--fim <yyyy-MM-ddTHH:mm:ss>] [--cliente PK2] [--tags TAG22,TAG23,...] " +
        "[--appsettings appsettings.json] [--horas-historico N] [--historico-inclui-atual] [--verbose]";

    public static async Task<int> RunAsync(string[] args)
    {
        Dictionary<string, string?> parsed;
        try
        {
            parsed = Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        if (!parsed.TryGetValue("entrada", out var entrada) || entrada is null ||
            !parsed.TryGetValue("saida", out var saida) || saida is null ||
            !parsed.TryGetValue("treino-ate", out var treinoAte) || treinoAte is null)
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
                InstanteTreino = ParseDate(treinoAte),
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

    private static DateTime ParseDate(string text) =>
        DateTime.ParseExact(text, ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm:ss"],
            CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static Dictionary<string, string?> Parse(string[] args)
    {
        string[] flags = ["historico-inclui-atual", "verbose"];
        string[] valued = ["entrada", "saida", "treino-ate", "fim", "cliente", "tags", "appsettings", "horas-historico"];
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Argumento inesperado: {args[i]}");
            var name = args[i][2..];
            if (flags.Contains(name, StringComparer.OrdinalIgnoreCase))
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
