namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Converte o histórico bruto da tag em vetores de características
/// utilizados no treinamento e na avaliação do Isolation Forest.
/// </summary>
public class IsolationForestFeatureExtractor
{
    /// <summary>
    /// Extrai as amostras utilizadas para treinar o modelo.
    ///
    /// Cada ponto histórico é analisado em relação aos pontos
    /// anteriores dentro de uma janela móvel.
    /// </summary>
    public IReadOnlyList<IsolationForestFeature> ExtrairTreinamento(
        DetectorInput input,
        int tamanhoJanela)
    {
        ValidarEntrada(input, tamanhoJanela);

        var historico = input.Historico
            .Where(ponto =>
                ponto != null &&
                double.IsFinite(ponto.Valor))
            .GroupBy(ponto => ponto.DataHora)
            .Select(grupo => grupo.Last())
            .OrderBy(ponto => ponto.DataHora)
            .ToList();

        var resultado = new List<IsolationForestFeature>();

        /*
         * Começamos no índice tamanhoJanela para que cada ponto
         * tenha uma janela anterior completa para comparação.
         */
        for (
            var indice = tamanhoJanela;
            indice < historico.Count;
            indice++)
        {
            var janelaAnterior = historico
                .Skip(indice - tamanhoJanela)
                .Take(tamanhoJanela)
                .ToList();

            var pontoAnterior =
                historico[indice - 1];

            var pontoAtual =
                historico[indice];

            var feature = CriarFeature(
                valorAtual: pontoAtual.Valor,
                dataHoraAtual: pontoAtual.DataHora,
                pontoAnterior: pontoAnterior,
                janelaAnterior: janelaAnterior
            );

            resultado.Add(feature);
        }

        return resultado;
    }

    /// <summary>
    /// Cria as características correspondentes ao valor atual
    /// que será avaliado pelo modelo.
    /// </summary>
    public IsolationForestFeature ExtrairAtual(
        DetectorInput input,
        int tamanhoJanela)
    {
        ValidarEntrada(input, tamanhoJanela);

        var historico = input.Historico
            .Where(ponto =>
                ponto != null &&
                double.IsFinite(ponto.Valor))
            .GroupBy(ponto => ponto.DataHora)
            .Select(grupo => grupo.Last())
            .OrderBy(ponto => ponto.DataHora)
            .ToList();

        if (historico.Count < tamanhoJanela)
        {
            throw new InvalidOperationException(
                $"São necessários pelo menos {tamanhoJanela} " +
                "pontos para extrair a característica atual."
            );
        }

        var janelaAnterior = historico
            .TakeLast(tamanhoJanela)
            .ToList();

        var pontoAnterior =
            historico.Last();

        var dataHoraAtual =
            input.DataHoraAtual == default
                ? pontoAnterior.DataHora.AddMinutes(
                    CalcularIntervaloMedianoMinutos(historico)
                )
                : input.DataHoraAtual;

        /*
         * Em algumas fontes o timestamp atual pode ser igual ou
         * anterior ao último timestamp histórico. Nesse caso usamos
         * o intervalo mediano da série.
         */
        if (dataHoraAtual <= pontoAnterior.DataHora)
        {
            var intervalo =
                CalcularIntervaloMedianoMinutos(historico);

            if (intervalo <= 0)
            {
                intervalo = 1;
            }

            dataHoraAtual =
                pontoAnterior.DataHora.AddMinutes(intervalo);
        }

        return CriarFeature(
            valorAtual: input.ValorAtual,
            dataHoraAtual: dataHoraAtual,
            pontoAnterior: pontoAnterior,
            janelaAnterior: janelaAnterior
        );
    }

    private static IsolationForestFeature CriarFeature(
        double valorAtual,
        DateTime dataHoraAtual,
        HistoricoDetectorPoint pontoAnterior,
        IReadOnlyList<HistoricoDetectorPoint> janelaAnterior)
    {
        var valoresJanela = janelaAnterior
            .Select(ponto => ponto.Valor)
            .Where(double.IsFinite)
            .ToArray();

        if (valoresJanela.Length == 0)
        {
            throw new InvalidOperationException(
                "A janela móvel não possui valores válidos."
            );
        }

        var mediaMovel =
            valoresJanela.Average();

        var desvioMovel =
            DetectorMath.DesvioPadrao(valoresJanela);

        var diferencaMediaMovel =
            valorAtual - mediaMovel;

        var zScoreLocal =
            desvioMovel > 0
                ? diferencaMediaMovel / desvioMovel
                : 0;

        var intervaloMinutos = (
            dataHoraAtual -
            pontoAnterior.DataHora
        ).TotalMinutes;

        if (intervaloMinutos <= 0)
        {
            intervaloMinutos = 1;
        }

        var taxaVariacao =
            (valorAtual - pontoAnterior.Valor) /
            intervaloMinutos;

        var lambda = 0.25;

        var ewmaAnterior =
            CalcularEwma(
                valoresJanela,
                lambda
            );

        var ewmaAtual =
            (lambda * valorAtual) +
            ((1 - lambda) * ewmaAnterior);

        var horaDecimal =
            dataHoraAtual.Hour +
            (dataHoraAtual.Minute / 60.0) +
            (dataHoraAtual.Second / 3600.0);

        var anguloHora =
            2.0 *
            Math.PI *
            horaDecimal /
            24.0;

        return new IsolationForestFeature
        {
            Valor = valorAtual,
            MediaMovel = mediaMovel,
            DesvioMovel = desvioMovel,
            DiferencaMediaMovel = diferencaMediaMovel,
            ZScoreLocal = zScoreLocal,
            TaxaVariacaoPorMinuto = taxaVariacao,
            Ewma = ewmaAtual,
            HoraSin = Math.Sin(anguloHora),
            HoraCos = Math.Cos(anguloHora)
        };
    }

    private static double CalcularEwma(
        IReadOnlyList<double> valores,
        double lambda)
    {
        if (valores.Count == 0)
        {
            return 0;
        }

        var ewma = valores[0];

        for (var indice = 1; indice < valores.Count; indice++)
        {
            ewma =
                (lambda * valores[indice]) +
                ((1 - lambda) * ewma);
        }

        return ewma;
    }

    private static double CalcularIntervaloMedianoMinutos(
        IReadOnlyList<HistoricoDetectorPoint> historico)
    {
        if (historico.Count < 2)
        {
            return 1;
        }

        var intervalos = new List<double>();

        for (var indice = 1; indice < historico.Count; indice++)
        {
            var intervalo = (
                historico[indice].DataHora -
                historico[indice - 1].DataHora
            ).TotalMinutes;

            if (
                intervalo > 0 &&
                double.IsFinite(intervalo))
            {
                intervalos.Add(intervalo);
            }
        }

        if (intervalos.Count == 0)
        {
            return 1;
        }

        return DetectorMath.Median(intervalos);
    }

    private static void ValidarEntrada(
        DetectorInput input,
        int tamanhoJanela)
    {
        if (input == null)
        {
            throw new ArgumentNullException(
                nameof(input)
            );
        }

        if (input.Historico == null)
        {
            throw new ArgumentException(
                "O histórico do detector não foi informado.",
                nameof(input)
            );
        }

        if (tamanhoJanela < 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tamanhoJanela),
                "A janela deve possuir pelo menos três pontos."
            );
        }

        if (
            !double.IsFinite(input.ValorAtual))
        {
            throw new ArgumentException(
                "O valor atual da tag é inválido.",
                nameof(input)
            );
        }
    }
}