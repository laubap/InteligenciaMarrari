using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IAApi.Cp300OfflineReplay;

// Subcomando "placar": compara a saida de um replay com o gabarito do simulador
// (slots TAG61-TAG72 e TAG73) por EVENTO de falha, sem contar coincidencias
// como acerto (Docs/PROBLEMAS_ENCONTRADOS.md, P21). So le arquivos CSV/JSON:
// nao acessa Bridge, PSi4, CLP nem banco, e nao executa codigo de runtime.

public sealed record Cp300PlacarOptions
{
    /// <summary>CSV gerado pelo replay (data_hora, tag, decisao_final, erro...).</summary>
    public required string CsvReplay { get; init; }

    /// <summary>CSV de dados (ClienteId,TagName,DataHora,Valor) com os slots e a TAG73.</summary>
    public required string CsvGabarito { get; init; }

    /// <summary>Mapa falha -> tags afetadas (JSON). Padrao: placar_falhas_cp300.json ao lado do executavel.</summary>
    public string? MapaPath { get; init; }

    /// <summary>N: a tag nao pode ter alarmado em [inicio - N, inicio) para contar como deteccao.</summary>
    public int JanelaAntesMinutos { get; init; } = 20;

    /// <summary>D: minutos seguidos de alarme na mesma tag elegivel para contar como deteccao.
    /// Sequencias mais curtas aparecem como "sinal breve". 1 = qualquer alarme.</summary>
    public int DuracaoMinimaMinutos { get; init; } = 3;

    /// <summary>Buraco maximo (min) entre leituras do mesmo codigo dentro de um evento.</summary>
    public int ToleranciaBuracoMinutos { get; init; } = 2;

    public string? ClienteId { get; init; }
}

public sealed record Cp300PlacarEvento
{
    public required string Slot { get; init; }
    public required int Codigo { get; init; }
    public required string NomeFalha { get; init; }
    public required DateTime Inicio { get; init; }
    public required DateTime Fim { get; init; }
    public required int Alvo { get; init; }
    public required string AlvoDescricao { get; init; }
    public required int SeveridadeMaxima { get; init; }
    public IReadOnlyList<string> TagsAfetadas { get; init; } = [];
    public IReadOnlyList<string> TagsForaDoReplay { get; init; } = [];
    /// <summary>Tag -> minutos com alarme na janela [inicio - N, inicio).</summary>
    public IReadOnlyDictionary<string, int> TagsExcluidasAlarmeAntes { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> TagsElegiveis { get; init; } = [];
    public bool Detectado => PrimeiroAlarme is not null;
    /// <summary>Inicio da primeira sequencia de alarme com >= D minutos numa tag elegivel.</summary>
    public DateTime? PrimeiroAlarme { get; init; }
    public IReadOnlyList<string> TagsDeteccao { get; init; } = [];
    /// <summary>Duracao (min) da sequencia que deu a deteccao, limitada ao fim da falha.</summary>
    public int? DuracaoDeteccao { get; init; }
    public int? AtrasoMinutos { get; init; }
    public int? SeveridadeNaDeteccao { get; init; }
    public int? FaseNaDeteccao { get; init; }
    /// <summary>Primeira sequencia com menos de D minutos, so se vier antes da deteccao.</summary>
    public DateTime? SinalBreve { get; init; }
    public IReadOnlyList<string> TagsSinalBreve { get; init; } = [];
    public int? DuracaoSinalBreve { get; init; }
    public int? AtrasoSinalBreve { get; init; }
    public int? SeveridadeSinalBreve { get; init; }
    public int? FaseSinalBreve { get; init; }
    /// <summary>Minutos-tag com alarme dentro do evento nas tags elegiveis.</summary>
    public int MinutosTagAtribuidos { get; init; }
    /// <summary>Minutos-tag com alarme dentro do evento nas demais tags (P21).</summary>
    public int MinutosTagCoincidencia { get; init; }
    public IReadOnlyList<string> Observacoes { get; init; } = [];
}

public sealed record Cp300PlacarTag(
    string Tag, int Avaliados, int AvaliadosFora, int Alarmes, int Atribuidos, int Coincidencias, int FalsosFora);

public sealed record Cp300PlacarHora(
    int Hora, int AvaliadosFora, int FalsosFora, IReadOnlyList<(string Tag, int Alarmes)> PorTag);

public sealed record Cp300PlacarResultado
{
    public required string CsvReplay { get; init; }
    public required string CsvGabarito { get; init; }
    public required string Mapa { get; init; }
    public required int JanelaAntesMinutos { get; init; }
    public required int DuracaoMinimaMinutos { get; init; }
    public required DateTime ReplayInicio { get; init; }
    public required DateTime ReplayFim { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required int MinutosTagAvaliados { get; init; }
    public required int LeiturasComErro { get; init; }
    public required bool TemTagAnomaliaAtiva { get; init; }
    public required IReadOnlyList<Cp300PlacarEvento> Eventos { get; init; }
    public required int EventosForaDoReplay { get; init; }
    public required int TotalAlarmes { get; init; }
    public required int AlarmesAtribuidos { get; init; }
    public required int AlarmesCoincidencia { get; init; }
    public required int AlarmesFalsosFora { get; init; }
    /// <summary>Minutos (do replay) com falha ativa: algum evento ou TAG73 = 1.</summary>
    public required int MinutosComFalha { get; init; }
    public required IReadOnlyList<Cp300PlacarTag> PorTag { get; init; }
    public required IReadOnlyList<Cp300PlacarHora> PorHora { get; init; }
    public IReadOnlyList<string> Avisos { get; init; } = [];
}

/// <summary>Mapa editavel falha -> tags (placar_falhas_cp300.json).</summary>
public sealed class Cp300MapaFalhas
{
    public const string ArquivoPadrao = "placar_falhas_cp300.json";

    public string TagBombaPrincipal { get; set; } = "TAG44";
    public string? TagAnomaliaAtiva { get; set; } = "TAG73";
    public List<Cp300SlotGabarito> Slots { get; set; } = [];
    public Dictionary<string, Dictionary<string, string>> Bombas { get; set; } = new();
    public Dictionary<string, string> CanaisSensor { get; set; } = new();
    public Dictionary<string, Cp300FalhaMapeada> Falhas { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Cp300MapaFalhas Load(string path)
    {
        var mapa = JsonSerializer.Deserialize<Cp300MapaFalhas>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Mapa vazio: {path}");
        mapa.Validate();
        return mapa;
    }

    public static string ResolverCaminhoPadrao()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Path.Combine(Directory.GetCurrentDirectory(), "tools", "Cp300OfflineReplay"), Directory.GetCurrentDirectory() })
        {
            var candidate = Path.Combine(dir, ArquivoPadrao);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"{ArquivoPadrao} nao encontrado; informe --mapa.");
    }

    public void Validate()
    {
        if (Slots.Count == 0)
            throw new InvalidDataException("Mapa sem 'slots'.");
        foreach (var (codigo, falha) in Falhas)
        {
            if (!int.TryParse(codigo, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                throw new InvalidDataException($"Codigo de falha invalido no mapa: '{codigo}'.");
            foreach (var token in falha.Tags)
            {
                if (token == "*" || token == "CANAL")
                    continue;
                var campo = TokenCampo(token);
                if (campo is not null)
                {
                    if (!Bombas.Values.Any(b => b.ContainsKey(campo)))
                        throw new InvalidDataException($"Falha {codigo}: campo de bomba desconhecido em '{token}'.");
                    continue;
                }

                if (!token.StartsWith("TAG", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Falha {codigo}: token invalido '{token}'.");
                if (Cp300Replay.IsLabelTag(token))
                    throw new InvalidDataException($"Falha {codigo}: {token} e rotulo do simulador e nao pode ser tag afetada.");
            }
        }
    }

    private static string? TokenCampo(string token)
    {
        foreach (var prefix in new[] { "ALVO.", "OUTRA." })
            if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return token[prefix.Length..];
        return null;
    }

    /// <summary>
    /// Resolve os tokens de uma falha. bomba = 1/2 (ou null se nao resolvida);
    /// alvo = valor bruto do slot (para CANAL).
    /// </summary>
    public IReadOnlyList<string> ResolverTags(
        Cp300FalhaMapeada falha, int? bomba, int alvo, IReadOnlyCollection<string> tagsReplay, List<string> observacoes)
    {
        var result = new List<string>();
        void Add(string? tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !result.Contains(tag, StringComparer.OrdinalIgnoreCase))
                result.Add(tag.ToUpperInvariant());
        }

        foreach (var token in falha.Tags)
        {
            if (token == "*")
            {
                foreach (var t in tagsReplay) Add(t);
            }
            else if (token == "CANAL")
            {
                var canal = (alvo % 100).ToString(CultureInfo.InvariantCulture);
                if (CanaisSensor.TryGetValue(canal, out var tag)) Add(tag);
                else observacoes.Add($"canal de sensor {canal} sem tag no mapa");
            }
            else if (token.StartsWith("ALVO.", StringComparison.OrdinalIgnoreCase) ||
                     token.StartsWith("OUTRA.", StringComparison.OrdinalIgnoreCase))
            {
                var outra = token.StartsWith("OUTRA.", StringComparison.OrdinalIgnoreCase);
                var campo = TokenCampo(token)!;
                if (bomba is null)
                {
                    // Alvo nao resolvido: ALVO vale para as duas bombas; OUTRA fica de fora.
                    if (!outra)
                        foreach (var b in Bombas.Values)
                            Add(b.GetValueOrDefault(campo));
                    continue;
                }

                var chave = outra
                    ? Bombas.Keys.FirstOrDefault(k => k != bomba.Value.ToString(CultureInfo.InvariantCulture))
                    : bomba.Value.ToString(CultureInfo.InvariantCulture);
                if (chave is not null && Bombas.TryGetValue(chave, out var campos))
                    Add(campos.GetValueOrDefault(campo));
            }
            else
            {
                Add(token);
            }
        }

        return result;
    }
}

public sealed class Cp300SlotGabarito
{
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Alvo { get; set; } = "";
    public string Severidade { get; set; } = "";
    public string Fase { get; set; } = "";
}

public sealed class Cp300FalhaMapeada
{
    public string Nome { get; set; } = "";
    public string? Origem { get; set; }
    public List<string> Tags { get; set; } = [];
}

public static class Cp300Placar
{
    public static Cp300PlacarResultado Calcular(Cp300PlacarOptions options)
    {
        if (options.JanelaAntesMinutos < 0)
            throw new ArgumentException("--janela-antes deve ser >= 0.");
        if (options.DuracaoMinimaMinutos < 1)
            throw new ArgumentException("--duracao-minima deve ser >= 1.");
        var d = options.DuracaoMinimaMinutos;
        var mapaPath = options.MapaPath ?? Cp300MapaFalhas.ResolverCaminhoPadrao();
        var mapa = Cp300MapaFalhas.Load(mapaPath);
        var gabarito = Cp300Csv.Load(options.CsvGabarito, options.ClienteId);
        var replay = ReplayAlarmes.Load(options.CsvReplay);
        if (replay.Avaliados.Count == 0)
            throw new InvalidDataException("CSV do replay sem leituras.");

        var avisos = new List<string>();
        var series = gabarito.Readings.ToDictionary(
            p => p.Key,
            p => p.Value.Select(l => (Minuto: Minuto(l.DataHora), Valor: (double)l.Valor)).ToArray(),
            StringComparer.OrdinalIgnoreCase);

        var tagsReplay = replay.Avaliados.Keys.Order(StringComparer.Ordinal).ToArray();
        var replayInicio = replay.Avaliados.Values.SelectMany(s => s).Min();
        var replayFim = replay.Avaliados.Values.SelectMany(s => s).Max();
        var n = options.JanelaAntesMinutos;

        // 1. Eventos de falha por slot.
        var brutos = new List<(Cp300SlotGabarito Slot, int Codigo, DateTime Inicio, DateTime Fim)>();
        foreach (var slot in mapa.Slots)
        {
            if (!series.TryGetValue(slot.Codigo, out var codigo))
            {
                avisos.Add($"Slot {slot.Nome}: {slot.Codigo} ausente no gabarito; slot nao avaliado.");
                continue;
            }

            brutos.AddRange(ExtrairEventos(codigo, options.ToleranciaBuracoMinutos)
                .Select(e => (slot, e.Codigo, e.Inicio, e.Fim)));
        }

        var foraDoReplay = brutos.Count(e => e.Fim < replayInicio || e.Inicio > replayFim);
        var eventosBrutos = brutos
            .Where(e => e.Fim >= replayInicio && e.Inicio <= replayFim)
            .OrderBy(e => e.Inicio).ThenBy(e => e.Slot.Nome, StringComparer.Ordinal)
            .ToList();

        // 2. Minutos com falha ativa (eventos + TAG73 = 1).
        var minutosFalha = new HashSet<DateTime>();
        foreach (var e in brutos)
            for (var m = e.Inicio; m <= e.Fim; m = m.AddMinutes(1))
                minutosFalha.Add(m);
        var temTag73 = mapa.TagAnomaliaAtiva is not null && series.ContainsKey(mapa.TagAnomaliaAtiva);
        if (temTag73)
        {
            foreach (var (minuto, valor) in series[mapa.TagAnomaliaAtiva!])
                if (Math.Round(valor) == 1)
                    minutosFalha.Add(minuto);
        }
        else
        {
            avisos.Add($"{mapa.TagAnomaliaAtiva ?? "Tag de anomalia ativa"} ausente no gabarito: falhas definidas so pelos slots.");
        }

        // 3. Placar por evento (regra P21).
        var atribuidos = new HashSet<(string Tag, DateTime Minuto)>();
        var eventos = new List<Cp300PlacarEvento>();
        foreach (var bruto in eventosBrutos)
        {
            var obs = new List<string>();
            var codigoTexto = bruto.Codigo.ToString(CultureInfo.InvariantCulture);
            var alvo = (int)Math.Round(ValorEm(series, bruto.Slot.Alvo, bruto.Inicio) ?? -1);
            mapa.Falhas.TryGetValue(codigoTexto, out var falha);
            var (bomba, alvoDescricao) = ResolverAlvo(mapa, series, alvo, bruto.Inicio);
            if (bruto.Inicio < replayInicio)
                obs.Add("falha comecou antes do replay");
            if (bruto.Fim > replayFim)
                obs.Add("falha termina depois do replay");

            IReadOnlyList<string> afetadas = [];
            if (falha is null)
                obs.Add($"codigo {bruto.Codigo} sem mapeamento: nenhuma tag conta como deteccao");
            else
            {
                if (alvo < 0)
                    obs.Add($"{bruto.Slot.Alvo} ausente no gabarito");
                if (bomba is null && falha.Tags.Any(t => t.StartsWith("ALVO.", StringComparison.OrdinalIgnoreCase)))
                    obs.Add("bomba alvo nao resolvida: ALVO vale para as duas bombas e OUTRA e ignorada");
                afetadas = mapa.ResolverTags(falha, bomba, Math.Max(alvo, 0), tagsReplay, obs);
            }

            var noReplay = afetadas.Where(t => replay.Avaliados.ContainsKey(t)).ToList();
            var foraReplay = afetadas.Where(t => !replay.Avaliados.ContainsKey(t)).ToList();
            var excluidas = new Dictionary<string, int>(StringComparer.Ordinal);
            var elegiveis = new List<string>();
            var janelaInicio = bruto.Inicio.AddMinutes(-n);
            foreach (var tag in noReplay)
            {
                var antes = replay.Alarmes[tag].GetViewBetween(janelaInicio, bruto.Inicio.AddMinutes(-1)).Count;
                if (n > 0 && antes > 0)
                    excluidas[tag] = antes;
                else
                    elegiveis.Add(tag);

                var cobertos = n == 0 ? 0 : replay.Avaliados[tag].GetViewBetween(janelaInicio, bruto.Inicio.AddMinutes(-1)).Count;
                if (n > 0 && cobertos < n)
                    obs.Add($"{tag}: janela antes incompleta ({cobertos} de {n} min no replay)");
            }

            var deteccao = new Candidato();
            var breve = new Candidato();
            var atribuidosEvento = 0;
            foreach (var tag in elegiveis)
            {
                var dentro = replay.Alarmes[tag].GetViewBetween(bruto.Inicio, bruto.Fim);
                atribuidosEvento += dentro.Count;
                foreach (var m in dentro)
                    atribuidos.Add((tag, m));
                foreach (var (ini, dur) in Sequencias(dentro))
                {
                    if (dur >= d)
                    {
                        deteccao.Considerar(tag, ini, dur);
                        break; // so a primeira sequencia longa de cada tag interessa
                    }

                    breve.Considerar(tag, ini, dur);
                }
            }

            // Sinal breve so e informado quando vem antes da deteccao (ou sem deteccao).
            if (deteccao.Minuto is not null && breve.Minuto >= deteccao.Minuto)
                breve = new Candidato();
            var primeiro = deteccao.Minuto;

            eventos.Add(new Cp300PlacarEvento
            {
                Slot = bruto.Slot.Nome,
                Codigo = bruto.Codigo,
                NomeFalha = falha?.Nome ?? "(sem mapeamento)",
                Inicio = bruto.Inicio,
                Fim = bruto.Fim,
                Alvo = alvo,
                AlvoDescricao = alvoDescricao,
                SeveridadeMaxima = (int)Math.Round(MaximoEntre(series, bruto.Slot.Severidade, bruto.Inicio, bruto.Fim) ?? 0),
                TagsAfetadas = afetadas,
                TagsForaDoReplay = foraReplay,
                TagsExcluidasAlarmeAntes = excluidas,
                TagsElegiveis = elegiveis,
                PrimeiroAlarme = primeiro,
                TagsDeteccao = deteccao.Tags,
                DuracaoDeteccao = deteccao.Duracao,
                AtrasoMinutos = primeiro is null ? null : (int)(primeiro.Value - bruto.Inicio).TotalMinutes,
                SeveridadeNaDeteccao = primeiro is null ? null : Arredondar(ValorEm(series, bruto.Slot.Severidade, primeiro.Value)),
                FaseNaDeteccao = primeiro is null ? null : Arredondar(ValorEm(series, bruto.Slot.Fase, primeiro.Value)),
                SinalBreve = breve.Minuto,
                TagsSinalBreve = breve.Tags,
                DuracaoSinalBreve = breve.Duracao,
                AtrasoSinalBreve = breve.Minuto is { } b ? (int)(b - bruto.Inicio).TotalMinutes : null,
                SeveridadeSinalBreve = breve.Minuto is { } b2 ? Arredondar(ValorEm(series, bruto.Slot.Severidade, b2)) : null,
                FaseSinalBreve = breve.Minuto is { } b3 ? Arredondar(ValorEm(series, bruto.Slot.Fase, b3)) : null,
                MinutosTagAtribuidos = atribuidosEvento,
                Observacoes = obs
            });
        }

        // Coincidencias por evento: alarmes dentro do evento nao atribuidos a nenhum evento
        // (um alarme atribuido a uma falha sobreposta nao e coincidencia da outra).
        eventos = eventos.Select(e => e with
        {
            MinutosTagCoincidencia = replay.Alarmes.Sum(p =>
                p.Value.GetViewBetween(e.Inicio, e.Fim).Count(m => !atribuidos.Contains((p.Key, m))))
        }).ToList();

        // 4. Classificacao de todos os alarmes: atribuido / coincidencia (dentro da falha) / falso fora.
        var porTag = new List<Cp300PlacarTag>();
        var porHora = new Dictionary<int, (int Avaliados, int Falsos, Dictionary<string, int> Tags)>();
        int total = 0, totalAtrib = 0, totalCoinc = 0, totalFora = 0;
        foreach (var tag in tagsReplay)
        {
            int avaliadosFora = 0, alarmes = 0, atrib = 0, coinc = 0, fora = 0;
            var alarmesTag = replay.Alarmes[tag];
            foreach (var m in replay.Avaliados[tag])
            {
                var dentroFalha = minutosFalha.Contains(m);
                if (!dentroFalha)
                {
                    avaliadosFora++;
                    var h = porHora.TryGetValue(m.Hour, out var acc) ? acc : (0, 0, new Dictionary<string, int>(StringComparer.Ordinal));
                    h.Item1++;
                    porHora[m.Hour] = h;
                }

                if (!alarmesTag.Contains(m))
                    continue;
                alarmes++;
                if (atribuidos.Contains((tag, m))) atrib++;
                else if (dentroFalha) coinc++;
                else
                {
                    fora++;
                    var h = porHora[m.Hour];
                    h.Item2++;
                    h.Item3[tag] = h.Item3.GetValueOrDefault(tag) + 1;
                    porHora[m.Hour] = h;
                }
            }

            porTag.Add(new Cp300PlacarTag(tag, replay.Avaliados[tag].Count, avaliadosFora, alarmes, atrib, coinc, fora));
            total += alarmes; totalAtrib += atrib; totalCoinc += coinc; totalFora += fora;
        }

        var minutosReplay = replay.Avaliados.Values.SelectMany(s => s).ToHashSet();
        return new Cp300PlacarResultado
        {
            CsvReplay = options.CsvReplay,
            CsvGabarito = options.CsvGabarito,
            Mapa = mapaPath,
            JanelaAntesMinutos = n,
            DuracaoMinimaMinutos = d,
            ReplayInicio = replayInicio,
            ReplayFim = replayFim,
            Tags = tagsReplay,
            MinutosTagAvaliados = replay.Avaliados.Values.Sum(s => s.Count),
            LeiturasComErro = replay.Erros,
            TemTagAnomaliaAtiva = temTag73,
            Eventos = eventos,
            EventosForaDoReplay = foraDoReplay,
            TotalAlarmes = total,
            AlarmesAtribuidos = totalAtrib,
            AlarmesCoincidencia = totalCoinc,
            AlarmesFalsosFora = totalFora,
            MinutosComFalha = minutosReplay.Count(minutosFalha.Contains),
            PorTag = porTag,
            PorHora = porHora.OrderBy(p => p.Key)
                .Select(p => new Cp300PlacarHora(p.Key, p.Value.Avaliados, p.Value.Falsos,
                    p.Value.Tags.OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal)
                        .Select(t => (t.Key, t.Value)).ToList()))
                .ToList(),
            Avisos = avisos
        };
    }

    internal static DateTime Minuto(DateTime t) => new(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute, t.Kind);

    /// <summary>Sequencias de minutos seguidos com alarme: (inicio, duracao em minutos).</summary>
    internal static IEnumerable<(DateTime Inicio, int Duracao)> Sequencias(IEnumerable<DateTime> alarmes)
    {
        DateTime? inicio = null, anterior = null;
        var duracao = 0;
        foreach (var m in alarmes)
        {
            if (anterior is not null && m == anterior.Value.AddMinutes(1))
                duracao++;
            else
            {
                if (inicio is not null)
                    yield return (inicio.Value, duracao);
                inicio = m;
                duracao = 1;
            }

            anterior = m;
        }

        if (inicio is not null)
            yield return (inicio.Value, duracao);
    }

    /// <summary>Menor minuto de inicio entre as tags; empates juntam as tags (duracao = a maior).</summary>
    private sealed class Candidato
    {
        public DateTime? Minuto { get; private set; }
        public List<string> Tags { get; } = [];
        public int? Duracao { get; private set; }

        public void Considerar(string tag, DateTime inicio, int duracao)
        {
            if (Minuto is null || inicio < Minuto)
            {
                Minuto = inicio;
                Tags.Clear();
                Duracao = null;
            }

            if (inicio != Minuto)
                return;
            if (!Tags.Contains(tag))
                Tags.Add(tag);
            Duracao = Math.Max(Duracao ?? 0, duracao);
        }
    }

    private static IEnumerable<(int Codigo, DateTime Inicio, DateTime Fim)> ExtrairEventos(
        (DateTime Minuto, double Valor)[] serie, int tolerancia)
    {
        int? atual = null;
        DateTime inicio = default, ultimo = default;
        foreach (var (minuto, valor) in serie)
        {
            var codigo = (int)Math.Round(valor);
            if (atual is not null && (codigo != atual || (minuto - ultimo).TotalMinutes > tolerancia))
            {
                yield return (atual.Value, inicio, ultimo);
                atual = null;
            }

            if (codigo == 0)
                continue;
            if (atual is null)
            {
                atual = codigo;
                inicio = minuto;
            }

            ultimo = minuto;
        }

        if (atual is not null)
            yield return (atual.Value, inicio, ultimo);
    }

    private static (int? Bomba, string Descricao) ResolverAlvo(
        Cp300MapaFalhas mapa, Dictionary<string, (DateTime Minuto, double Valor)[]> series, int alvo, DateTime inicio)
    {
        string Nome(int b) => mapa.Bombas.TryGetValue(b.ToString(CultureInfo.InvariantCulture), out var c)
            ? c.GetValueOrDefault("nome") ?? $"bomba {b}" : $"bomba {b}";

        if (alvo is 1 or 2)
            return (alvo, Nome(alvo));
        if (alvo == 0)
        {
            // Principal no instante da injecao: ultimo valor antes do inicio (ou no inicio).
            var principal = ValorAntes(series, mapa.TagBombaPrincipal, inicio) ?? ValorEm(series, mapa.TagBombaPrincipal, inicio);
            var b = principal is null ? (int?)null : (int)Math.Round(principal.Value);
            return b is 1 or 2
                ? (b, $"principal = {Nome(b.Value)}")
                : (null, $"principal ({mapa.TagBombaPrincipal} indisponivel)");
        }

        if (alvo >= 100)
            return (null, $"sensor {alvo % 100} (negativo)");
        return alvo > 0 ? (null, $"sensor {alvo}") : (null, "?");
    }

    private static double? ValorAntes(Dictionary<string, (DateTime Minuto, double Valor)[]> series, string tag, DateTime t)
    {
        if (!series.TryGetValue(tag, out var s))
            return null;
        double? v = null;
        foreach (var p in s)
        {
            if (p.Minuto >= t) break;
            v = p.Valor;
        }

        return v;
    }

    /// <summary>Valor no minuto t ou, se faltar, o ultimo antes dele.</summary>
    private static double? ValorEm(Dictionary<string, (DateTime Minuto, double Valor)[]> series, string tag, DateTime t)
    {
        if (!series.TryGetValue(tag, out var s))
            return null;
        double? v = null;
        foreach (var p in s)
        {
            if (p.Minuto > t) break;
            v = p.Valor;
        }

        return v;
    }

    private static double? MaximoEntre(Dictionary<string, (DateTime Minuto, double Valor)[]> series, string tag, DateTime a, DateTime b)
    {
        if (!series.TryGetValue(tag, out var s))
            return null;
        var dentro = s.Where(p => p.Minuto >= a && p.Minuto <= b).ToArray();
        return dentro.Length == 0 ? null : dentro.Max(p => p.Valor);
    }

    private static int? Arredondar(double? v) => v is null ? null : (int)Math.Round(v.Value);

    /// <summary>Alarmes do replay por tag: minuto com decisao_final = 1.</summary>
    private sealed class ReplayAlarmes
    {
        public Dictionary<string, SortedSet<DateTime>> Avaliados { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SortedSet<DateTime>> Alarmes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Erros { get; private set; }

        public static ReplayAlarmes Load(string path)
        {
            var result = new ReplayAlarmes();
            var erros = new HashSet<(string, DateTime)>();
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var header = Cp300Csv.Split(reader.ReadLine() ?? throw new InvalidDataException("CSV do replay vazio."))
                .Select(c => c.Trim()).ToArray();
            int Index(string name) => Array.FindIndex(header, c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
            var iData = Index("data_hora");
            var iTag = Index("tag");
            var iDecisao = Index("decisao_final");
            var iErro = Index("erro");
            if (iData < 0 || iTag < 0 || iDecisao < 0)
                throw new InvalidDataException("CSV do replay sem as colunas data_hora, tag e decisao_final.");

            string? line;
            var lineNumber = 1;
            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var f = Cp300Csv.Split(line);
                if (f.Count <= Math.Max(iData, Math.Max(iTag, iDecisao)))
                    throw new InvalidDataException($"Replay, linha {lineNumber}: colunas insuficientes.");
                if (!DateTime.TryParse(f[iData], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataHora))
                    throw new InvalidDataException($"Replay, linha {lineNumber}: data_hora '{f[iData]}' invalida.");
                var minuto = Minuto(dataHora);
                var tag = f[iTag].Trim().ToUpperInvariant();
                if (!result.Avaliados.TryGetValue(tag, out var avaliados))
                {
                    result.Avaliados[tag] = avaliados = [];
                    result.Alarmes[tag] = [];
                }

                if (iErro >= 0 && iErro < f.Count && !string.IsNullOrWhiteSpace(f[iErro]))
                {
                    // Leitura com erro nao conta como avaliada nem como alarme.
                    erros.Add((tag, minuto));
                    continue;
                }

                avaliados.Add(minuto);
                if (f[iDecisao].Trim() == "1")
                    result.Alarmes[tag].Add(minuto);
            }

            result.Erros = erros.Count;
            return result;
        }
    }
}

/// <summary>Saidas do placar: Markdown e CSVs.</summary>
public static class Cp300PlacarRelatorio
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<string> Gravar(Cp300PlacarResultado r, string prefixo)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(prefixo));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var arquivos = new[]
        {
            (prefixo + ".md", Markdown(r)),
            (prefixo + "_eventos.csv", CsvEventos(r)),
            (prefixo + "_por_tag.csv", CsvPorTag(r)),
            (prefixo + "_falsos_por_hora.csv", CsvPorHora(r))
        };
        foreach (var (path, conteudo) in arquivos)
            File.WriteAllText(path, conteudo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return arquivos.Select(a => Path.GetFullPath(a.Item1)).ToList();
    }

    public static string Markdown(Cp300PlacarResultado r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Placar do replay CP300");
        sb.AppendLine();
        sb.AppendLine($"- **Replay:** `{Path.GetFileName(r.CsvReplay)}` ({Dt(r.ReplayInicio)} → {Dt(r.ReplayFim)}; {r.Tags.Count} tags; {r.MinutosTagAvaliados.ToString("N0", Pt)} minutos-tag; {r.LeiturasComErro} leituras com erro)");
        sb.AppendLine($"- **Gabarito:** `{Path.GetFileName(r.CsvGabarito)}`; **mapa:** `{Path.GetFileName(r.Mapa)}`");
        sb.AppendLine($"- **Regra P21:** alarme só conta como detecção se for numa tag que a falha afeta (mapa), se essa tag não alarmou nos **{r.JanelaAntesMinutos} min** antes do início da falha e se ficar em alarme **≥ {r.DuracaoMinimaMinutos} min seguidos**. Sequências mais curtas antes da detecção aparecem como *sinal breve*.");
        sb.AppendLine($"- **Eventos no período do replay:** {r.Eventos.Count}" + (r.EventosForaDoReplay > 0 ? $" ({r.EventosForaDoReplay} evento(s) do gabarito fora do período, ignorados)" : ""));
        foreach (var aviso in r.Avisos)
            sb.AppendLine($"- **Aviso:** {aviso}");
        sb.AppendLine();

        sb.AppendLine("## Por evento de falha");
        sb.AppendLine();
        var dm = r.DuracaoMinimaMinutos;
        sb.AppendLine($"| Slot | Falha | Início → fim | Alvo | Sev. máx. (‰) | Detecção (≥ {dm} min) | Tag | 1º alarme | Atraso (min) | Severidade na detecção (‰) | Sinal breve (< {dm} min) | Tags afetadas no replay | Excluídas (alarmavam antes) | Minutos-tag atribuídos / coincidências |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var e in r.Eventos)
        {
            var afetadasReplay = e.TagsAfetadas.Except(e.TagsForaDoReplay).ToList();
            var afetadasTexto = afetadasReplay.Count == 0 ? "—" : string.Join(", ", afetadasReplay);
            if (e.TagsForaDoReplay.Count > 0)
                afetadasTexto += $" (fora do replay: {string.Join(", ", e.TagsForaDoReplay)})";
            var excluidas = e.TagsExcluidasAlarmeAntes.Count == 0
                ? "—"
                : string.Join(", ", e.TagsExcluidasAlarmeAntes.Select(p => $"{p.Key} ({p.Value})"));
            sb.Append($"| {e.Slot} | {e.Codigo:00} {e.NomeFalha} | {Dt(e.Inicio)} → {Hm(e.Fim)} | {e.AlvoDescricao} | {e.SeveridadeMaxima} | ");
            sb.Append(e.Detectado
                ? $"**Sim** ({e.DuracaoDeteccao} min seguidos; confirmada às {Hm(e.PrimeiroAlarme!.Value.AddMinutes(dm - 1))})"
                : "Não");
            sb.Append($" | {(e.Detectado ? string.Join(", ", e.TagsDeteccao) : "—")} | {(e.PrimeiroAlarme is { } p ? Hm(p) : "—")} | {e.AtrasoMinutos?.ToString(CultureInfo.InvariantCulture) ?? "—"} | ");
            sb.Append($"{SevFase(e.SeveridadeNaDeteccao, e.FaseNaDeteccao)} | ");
            sb.Append(e.SinalBreve is { } sb2
                ? $"{string.Join(", ", e.TagsSinalBreve)} às {Hm(sb2)} ({e.DuracaoSinalBreve} min; atraso {e.AtrasoSinalBreve} min; {e.SeveridadeSinalBreve?.ToString(CultureInfo.InvariantCulture) ?? "—"} ‰{(e.FaseSinalBreve is { } fb ? $", fase {fb}" : "")})"
                : "—");
            sb.Append($" | {afetadasTexto} | {excluidas} | {e.MinutosTagAtribuidos} / {e.MinutosTagCoincidencia} |");
            sb.AppendLine();
        }

        var comObs = r.Eventos.Where(e => e.Observacoes.Count > 0).ToList();
        if (comObs.Count > 0)
        {
            sb.AppendLine();
            foreach (var e in comObs)
                sb.AppendLine($"- Slot {e.Slot}, {e.Codigo:00} ({Hm(e.Inicio)}): {string.Join("; ", e.Observacoes)}.");
        }

        sb.AppendLine();
        sb.AppendLine("## Alarmes (minutos-tag)");
        sb.AppendLine();
        sb.AppendLine("| Total | Atribuídos às falhas | Coincidências dentro das falhas (P21) | Falsos fora das falhas | Minutos com falha ativa |");
        sb.AppendLine("|---|---|---|---|---|");
        sb.AppendLine($"| {r.TotalAlarmes} | {r.AlarmesAtribuidos} | {r.AlarmesCoincidencia} | {r.AlarmesFalsosFora} | {r.MinutosComFalha} |");
        sb.AppendLine();
        sb.AppendLine("*Atribuídos* = alarmes dentro de um evento numa tag elegível (afetada e sem alarme antes). *Coincidências* = demais alarmes com falha ativa. *Fora* = minutos sem evento e sem TAG73 = 1.");
        sb.AppendLine();

        sb.AppendLine("### Por tag");
        sb.AppendLine();
        sb.AppendLine("| Tag | Minutos avaliados (fora das falhas) | Alarmes | Atribuídos | Coincidências | Falsos fora |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var t in r.PorTag)
            sb.AppendLine($"| {t.Tag} | {t.Avaliados} ({t.AvaliadosFora}) | {t.Alarmes} | {t.Atribuidos} | {t.Coincidencias} | {t.FalsosFora} |");
        sb.AppendLine($"| **Total** | {r.PorTag.Sum(t => t.Avaliados)} ({r.PorTag.Sum(t => t.AvaliadosFora)}) | {r.TotalAlarmes} | {r.AlarmesAtribuidos} | {r.AlarmesCoincidencia} | {r.AlarmesFalsosFora} |");
        sb.AppendLine();

        sb.AppendLine("### Falsos fora das falhas, por hora do dia");
        sb.AppendLine();
        sb.AppendLine("| Hora | Minutos-tag avaliados | Falsos | Taxa | Tags |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var h in r.PorHora)
        {
            var taxa = h.AvaliadosFora == 0 ? 0 : 100.0 * h.FalsosFora / h.AvaliadosFora;
            var tags = h.PorTag.Count == 0 ? "—" : string.Join(", ", h.PorTag.Select(t => $"{t.Tag} {t.Alarmes}"));
            sb.AppendLine($"| {h.Hora:00}h | {h.AvaliadosFora} | {h.FalsosFora} | {taxa.ToString("0.0", Pt)} % | {tags} |");
        }

        var madrugada = r.PorHora.Where(h => h.Hora < 6).ToList();
        var dia = r.PorHora.Where(h => h.Hora >= 6).ToList();
        sb.AppendLine();
        sb.AppendLine($"Madrugada (00h–05h): {madrugada.Sum(h => h.FalsosFora)} falsos em {madrugada.Sum(h => h.AvaliadosFora)} minutos-tag. Dia (06h–23h): {dia.Sum(h => h.FalsosFora)} em {dia.Sum(h => h.AvaliadosFora)}.");
        return sb.ToString();
    }

    public static string CsvEventos(Cp300PlacarResultado r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("slot,codigo,falha,inicio,fim,alvo,alvo_descricao,severidade_max,detectado,tags_deteccao,primeiro_alarme,atraso_min,severidade_deteccao,fase_deteccao,tags_afetadas,tags_fora_do_replay,tags_excluidas_alarme_antes,tags_elegiveis,minutos_tag_atribuidos,minutos_tag_coincidencia,janela_antes_min,duracao_minima_min,duracao_deteccao_min,confirmacao,sinal_breve,tags_sinal_breve,duracao_sinal_breve_min,atraso_sinal_breve_min,severidade_sinal_breve,fase_sinal_breve,observacoes");
        foreach (var e in r.Eventos)
        {
            string[] campos =
            [
                e.Slot, I(e.Codigo), e.NomeFalha, Iso(e.Inicio), Iso(e.Fim), I(e.Alvo), e.AlvoDescricao, I(e.SeveridadeMaxima),
                e.Detectado ? "1" : "0", string.Join(";", e.TagsDeteccao), e.PrimeiroAlarme is { } p ? Iso(p) : "",
                I(e.AtrasoMinutos), I(e.SeveridadeNaDeteccao), I(e.FaseNaDeteccao),
                string.Join(";", e.TagsAfetadas), string.Join(";", e.TagsForaDoReplay),
                string.Join(";", e.TagsExcluidasAlarmeAntes.Select(x => $"{x.Key}={x.Value}")), string.Join(";", e.TagsElegiveis),
                I(e.MinutosTagAtribuidos), I(e.MinutosTagCoincidencia), I(r.JanelaAntesMinutos),
                I(r.DuracaoMinimaMinutos), I(e.DuracaoDeteccao),
                e.PrimeiroAlarme is { } c ? Iso(c.AddMinutes(r.DuracaoMinimaMinutos - 1)) : "",
                e.SinalBreve is { } b ? Iso(b) : "",
                string.Join(";", e.TagsSinalBreve), I(e.DuracaoSinalBreve), I(e.AtrasoSinalBreve),
                I(e.SeveridadeSinalBreve), I(e.FaseSinalBreve), string.Join("; ", e.Observacoes)
            ];
            sb.AppendLine(string.Join(",", campos.Select(Cp300Csv.Escape)));
        }

        return sb.ToString();
    }

    public static string CsvPorTag(Cp300PlacarResultado r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("tag,minutos_avaliados,minutos_avaliados_fora,alarmes,atribuidos,coincidencias,falsos_fora");
        foreach (var t in r.PorTag)
            sb.AppendLine(string.Join(",", t.Tag, I(t.Avaliados), I(t.AvaliadosFora), I(t.Alarmes), I(t.Atribuidos), I(t.Coincidencias), I(t.FalsosFora)));
        return sb.ToString();
    }

    public static string CsvPorHora(Cp300PlacarResultado r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("hora,minutos_tag_avaliados_fora,falsos_fora,por_tag");
        foreach (var h in r.PorHora)
            sb.AppendLine(string.Join(",", I(h.Hora), I(h.AvaliadosFora), I(h.FalsosFora),
                Cp300Csv.Escape(string.Join(";", h.PorTag.Select(t => $"{t.Tag}={t.Alarmes}")))));
        return sb.ToString();
    }

    private static string SevFase(int? severidade, int? fase) =>
        severidade is { } s ? s + (fase is { } f ? $" (fase {f})" : "") : "—";

    private static string I(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "";
    private static string Iso(DateTime t) => t.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
    private static string Dt(DateTime t) => t.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
    private static string Hm(DateTime t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);
}
