

namespace IAApi.Services.AnomalyDetection;

public class StatisticalDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

    public StatisticalDetector(
        AnomalyDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    public string Nome => "Z-Score";

    public double Peso =>
        _options.ZScoreWeight;

    public DetectorResult Detectar(
        DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var media =
            input.MediaHistorica;

        var desvioOriginal =
            input.DesvioPadraoHistorico;

        var valorAtual =
            input.ValorAtual;

        var protecao =
            BaselineProtectionProfile.Build(
                input,
                media);

        var escalaEfetiva =
            protecao.ResolveEffectiveScale(
                desvioOriginal,
                _options.ZScoreThreshold);

        if (escalaEfetiva <= 0 ||
            !double.IsFinite(
                escalaEfetiva))
        {
            return NaoAplicavel(
                "Não foi possível determinar uma escala estatística segura.");
        }

        var deltaAbsoluto =
            Math.Abs(
                valorAtual -
                media);

        var zScoreBruto =
            deltaAbsoluto /
            escalaEfetiva;

        /*
         * Deadband é um gate físico.
         *
         * Dentro dele o detector permanece aplicável, mas devolve score zero.
         * Isso é importante porque o ensemble legado ainda soma scores de
         * detectores que não votaram anomalia.
         */
        var dentroDeadband =
            protecao.IsInsideDeadband(
                valorAtual,
                media);

        var zScore =
            dentroDeadband
                ? 0
                : zScoreBruto;

        var ehAnomalia =
            !dentroDeadband &&
            zScore >=
            _options.ZScoreThreshold;

        var limiteInferior =
            media -
            (_options.ZScoreThreshold *
             escalaEfetiva);

        var limiteSuperior =
            media +
            (_options.ZScoreThreshold *
             escalaEfetiva);

        return new DetectorResult
        {
            Aplicavel =
                true,

            EhAnomalia =
                ehAnomalia,

            Score =
                zScore,

            ScoreNormalizado =
                dentroDeadband
                    ? 0
                    : DetectorMath.NormalizarPorLimite(
                        zScore,
                        _options.ZScoreThreshold),

            Peso =
                Peso,

            LimiteInferior =
                limiteInferior,

            LimiteSuperior =
                limiteSuperior,

            Media =
                media,

            DesvioPadrao =
                escalaEfetiva,

            Detector =
                Nome,

            Explicacao =
                dentroDeadband
                    ? $"A variação absoluta de {deltaAbsoluto:F6} " +
                      $"está dentro do deadband de " +
                      $"{protecao.Deadband:F6}; o Z-Score foi suprimido."
                    : ehAnomalia
                        ? $"O valor está a {zScore:F2} escalas efetivas " +
                          $"da referência histórica."
                        : $"O Z-Score efetivo é {zScore:F2}, dentro do " +
                          $"limite configurado.",

            Metricas =
                new Dictionary<string, double>
                {
                    ["zScoreEfetivo"] =
                        zScore,

                    ["zScoreAntesDeadband"] =
                        zScoreBruto,

                    ["media"] =
                        media,

                    ["desvioPadraoOriginal"] =
                        double.IsFinite(desvioOriginal)
                            ? desvioOriginal
                            : 0,

                    ["escalaEfetiva"] =
                        escalaEfetiva,

                    ["deltaAbsoluto"] =
                        deltaAbsoluto,

                    ["deadband"] =
                        protecao.Deadband,

                    ["deadbandAutomaticoP95"] =
                        protecao.AutomaticDeadbandP95,

                    ["confiancaDeadband"] =
                        protecao.DeadbandConfidence,

                    ["dentroDeadband"] =
                        dentroDeadband
                            ? 1
                            : 0,

                    ["pisoRelativoEscala"] =
                        protecao.RelativeScaleFloor
                }
        };
    }

    private DetectorResult NaoAplicavel(
        string explicacao)
    {
        return new DetectorResult
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
}
