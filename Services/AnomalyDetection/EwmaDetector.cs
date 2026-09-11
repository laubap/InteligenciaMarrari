using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

public class EwmaDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public EwmaDetector(IOptions<AnomalyDetectionOptions> options)
    {
        _options = options.Value;
    }

    public string Nome => "EWMA";
    public double Peso => _options.EwmaWeight;

    public DetectorResult Detectar(DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Historico.Count < 10)
        {
            return NaoAplicavel("Histórico insuficiente para calcular EWMA.");
        }

        var contexto = input.Contexto;
        var cadenciaMediana = contexto.IntervaloMedianoMinutos;

        if (cadenciaMediana <= 0 || !double.IsFinite(cadenciaMediana))
        {
            return NaoAplicavel(
                "Não foi possível determinar a cadência temporal da tag.");
        }

        var alphaBase = Math.Clamp(_options.EwmaLambda, 0.01, 1.0);
        var mediaAlvo = input.MediaHistorica;
        var desvioBase = input.DesvioPadraoHistorico;

        if (desvioBase <= 0 || !double.IsFinite(desvioBase))
        {
            desvioBase = contexto.DesvioPadrao;
        }

        var protecao = BaselineProtectionProfile.Build(input, mediaAlvo);
        var escalaBaseEfetiva =
            protecao.ResolveEffectiveScale(desvioBase, _options.EwmaSigmaLimit);

        if (escalaBaseEfetiva <= 0 || !double.IsFinite(escalaBaseEfetiva))
        {
            return NaoAplicavel(
                "Não foi possível determinar uma escala segura para o EWMA.");
        }

        var ewma = mediaAlvo;
        var varianceFactor = 0.0;
        DateTime? timestampAnterior = null;
        var alphaMinimo = 1.0;
        var alphaMaximo = 0.0;
        var somaAlpha = 0.0;
        var quantidadeAlpha = 0;

        foreach (var ponto in input.Historico)
        {
            var intervalo = timestampAnterior.HasValue
                ? (ponto.DataHora - timestampAnterior.Value).TotalMinutes
                : cadenciaMediana;

            var alphaEfetivo =
                ResolveEffectiveAlpha(alphaBase, intervalo, cadenciaMediana);

            ewma =
                (alphaEfetivo * ponto.Valor) +
                ((1 - alphaEfetivo) * ewma);

            varianceFactor =
                (alphaEfetivo * alphaEfetivo) +
                ((1 - alphaEfetivo) *
                 (1 - alphaEfetivo) *
                 varianceFactor);

            alphaMinimo = Math.Min(alphaMinimo, alphaEfetivo);
            alphaMaximo = Math.Max(alphaMaximo, alphaEfetivo);
            somaAlpha += alphaEfetivo;
            quantidadeAlpha++;
            timestampAnterior = ponto.DataHora;
        }

        var ultimoPonto = input.Historico[^1];
        var intervaloAtual =
            (input.DataHoraAtual - ultimoPonto.DataHora).TotalMinutes;

        if (intervaloAtual <= 0 || !double.IsFinite(intervaloAtual))
        {
            intervaloAtual = cadenciaMediana;
        }

        var alphaAtual =
            ResolveEffectiveAlpha(alphaBase, intervaloAtual, cadenciaMediana);

        var ewmaAtual =
            (alphaAtual * input.ValorAtual) +
            ((1 - alphaAtual) * ewma);

        varianceFactor =
            (alphaAtual * alphaAtual) +
            ((1 - alphaAtual) *
             (1 - alphaAtual) *
             varianceFactor);

        var desvioEwma =
            escalaBaseEfetiva *
            Math.Sqrt(Math.Max(varianceFactor, 0));

        if (desvioEwma <= 0 || !double.IsFinite(desvioEwma))
        {
            return NaoAplicavel(
                "Não foi possível calcular o desvio temporal do EWMA.");
        }

        var deltaEwma = Math.Abs(ewmaAtual - mediaAlvo);
        var dentroDeadband =
            protecao.Deadband > 0 &&
            deltaEwma <= protecao.Deadband;

        var scoreEwmaBruto = deltaEwma / desvioEwma;
        var scoreEwma = dentroDeadband ? 0 : scoreEwmaBruto;

        var ehAnomalia =
            !dentroDeadband &&
            scoreEwma >= _options.EwmaSigmaLimit;

        return new DetectorResult
        {
            Aplicavel = true,
            EhAnomalia = ehAnomalia,
            Score = scoreEwma,
            ScoreNormalizado = dentroDeadband
                ? 0
                : DetectorMath.NormalizarPorLimite(
                    scoreEwma,
                    _options.EwmaSigmaLimit),
            Peso = Peso,
            LimiteInferior =
                mediaAlvo - (_options.EwmaSigmaLimit * desvioEwma),
            LimiteSuperior =
                mediaAlvo + (_options.EwmaSigmaLimit * desvioEwma),
            Media = mediaAlvo,
            DesvioPadrao = desvioEwma,
            Detector = Nome,
            Explicacao = dentroDeadband
                ? $"O deslocamento do EWMA ({deltaEwma:F6}) está dentro do deadband ({protecao.Deadband:F6})."
                : ehAnomalia
                    ? $"O EWMA temporal atingiu {ewmaAtual:F4}, com score {scoreEwma:F2}."
                    : $"O EWMA temporal atual é {ewmaAtual:F4}, dentro dos limites esperados.",
            Metricas = new Dictionary<string, double>
            {
                ["ewmaAnterior"] = ewma,
                ["ewmaAtual"] = ewmaAtual,
                ["lambdaBase"] = alphaBase,
                ["lambdaAtualEfetivo"] = alphaAtual,
                ["lambdaMedioEfetivo"] =
                    quantidadeAlpha > 0 ? somaAlpha / quantidadeAlpha : alphaBase,
                ["lambdaMinimoEfetivo"] =
                    quantidadeAlpha > 0 ? alphaMinimo : alphaBase,
                ["lambdaMaximoEfetivo"] =
                    quantidadeAlpha > 0 ? alphaMaximo : alphaBase,
                ["cadenciaMedianaMinutos"] = cadenciaMediana,
                ["intervaloAtualMinutos"] = intervaloAtual,
                ["fatorVarianciaEwma"] = varianceFactor,
                ["desvioEwma"] = desvioEwma,
                ["scoreEwma"] = scoreEwma,
                ["scoreEwmaAntesDeadband"] = scoreEwmaBruto,
                ["deadband"] = protecao.Deadband,
                ["dentroDeadband"] = dentroDeadband ? 1 : 0
            }
        };
    }

    private static double ResolveEffectiveAlpha(
        double alphaBase,
        double intervalMinutes,
        double medianIntervalMinutes)
    {
        if (alphaBase >= 1)
            return 1;

        if (intervalMinutes <= 0 ||
            !double.IsFinite(intervalMinutes) ||
            medianIntervalMinutes <= 0 ||
            !double.IsFinite(medianIntervalMinutes))
        {
            return alphaBase;
        }

        var relativeInterval =
            Math.Clamp(intervalMinutes / medianIntervalMinutes, 0.05, 50.0);

        var alpha =
            1 - Math.Pow(1 - alphaBase, relativeInterval);

        return Math.Clamp(alpha, 0.0001, 1.0);
    }

    private DetectorResult NaoAplicavel(string explicacao) => new()
    {
        Aplicavel = false,
        EhAnomalia = false,
        Score = 0,
        ScoreNormalizado = 0,
        Detector = Nome,
        Peso = Peso,
        Explicacao = explicacao
    };
}
