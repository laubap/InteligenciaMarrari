namespace IAApi.Services.AnomalyDetection;

public class MadDetector : IAnomalyDetector
{
    private readonly AnomalyDetectionOptions _options;

public MadDetector(
    AnomalyDetectionOptions options)
{
    ArgumentNullException.ThrowIfNull(options);

    _options = options;
}

    public string Nome =>
        "Mediana e MAD";

    public double Peso =>
        _options.MadWeight;

    public DetectorResult Detectar(
        DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var contexto =
            input.Contexto;

        if (contexto.Valores.Length < 10)
        {
            return NaoAplicavel(
                "Histórico insuficiente para calcular MAD.");
        }

        var mediana =
            contexto.Mediana;

        var mad =
            contexto.Mad;

        /*
         * Escala equivalente a sigma.
         *
         * Mesmo quando MAD=0, não abandonamos automaticamente o detector.
         * A BaselineProtectionProfile fornece um piso físico/estatístico.
         */
        var escalaRobustaOriginal =
            mad > 0 &&
            double.IsFinite(mad)
                ? mad * 1.4826
                : 0;

        var protecao =
            BaselineProtectionProfile.Build(
                input,
                mediana);

        var escalaEfetiva =
            protecao.ResolveEffectiveScale(
                escalaRobustaOriginal,
                _options.MadThreshold);

        if (escalaEfetiva <= 0 ||
            !double.IsFinite(
                escalaEfetiva))
        {
            return NaoAplicavel(
                "Não foi possível determinar uma escala robusta segura.");
        }

        var deltaAbsoluto =
            Math.Abs(
                input.ValorAtual -
                mediana);

        /*
         * delta / (MAD*1.4826) é numericamente equivalente ao
         * modified Z-score clássico 0.6745*delta/MAD.
         *
         * Usar a escala equivalente permite aplicar o mesmo piso de
         * baixa variância utilizado pelo Z-Score.
         */
        var zModificadoBruto =
            deltaAbsoluto /
            escalaEfetiva;

        var dentroDeadband =
            protecao.IsInsideDeadband(
                input.ValorAtual,
                mediana);

        var zModificado =
            dentroDeadband
                ? 0
                : zModificadoBruto;

        var ehAnomalia =
            !dentroDeadband &&
            zModificado >=
            _options.MadThreshold;

        return new DetectorResult
        {
            Aplicavel =
                true,

            EhAnomalia =
                ehAnomalia,

            Score =
                zModificado,

            ScoreNormalizado =
                dentroDeadband
                    ? 0
                    : DetectorMath.NormalizarPorLimite(
                        zModificado,
                        _options.MadThreshold),

            Peso =
                Peso,

            LimiteInferior =
                mediana -
                (_options.MadThreshold *
                 escalaEfetiva),

            LimiteSuperior =
                mediana +
                (_options.MadThreshold *
                 escalaEfetiva),

            Media =
                mediana,

            DesvioPadrao =
                escalaEfetiva,

            Detector =
                Nome,

            Explicacao =
                dentroDeadband
                    ? $"A variação absoluta de {deltaAbsoluto:F6} " +
                      $"está dentro do deadband de " +
                      $"{protecao.Deadband:F6}; o MAD foi suprimido."
                    : ehAnomalia
                        ? $"O valor possui score robusto efetivo de " +
                          $"{zModificado:F2} em relação à mediana."
                        : $"O score robusto efetivo é " +
                          $"{zModificado:F2}, dentro do comportamento esperado.",

            Metricas =
                new Dictionary<string, double>
                {
                    ["mediana"] =
                        mediana,

                    ["madOriginal"] =
                        double.IsFinite(mad)
                            ? mad
                            : 0,

                    ["escalaRobustaOriginal"] =
                        escalaRobustaOriginal,

                    ["escalaEfetiva"] =
                        escalaEfetiva,

                    ["zModificadoEfetivo"] =
                        zModificado,

                    ["zModificadoAntesDeadband"] =
                        zModificadoBruto,

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
