namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Contrato de segunda geração para a saída de um detector.
///
/// Este contrato separa:
/// - aplicabilidade;
/// - voto binário;
/// - força da evidência;
/// - severidade;
/// - confiança no cálculo;
/// - peso base;
/// - família do detector.
///
/// Durante a migração, o DetectorResult atual permanece em uso pelo
/// Ensemble atual. O DetectorResultV2 será produzido em paralelo para
/// comparação e para o futuro Ensemble V2.
/// </summary>
public sealed class DetectorResultV2
{
    /// <summary>
    /// Versão explícita do contrato para logs, relatórios e serialização.
    /// </summary>
    public int ContractVersion { get; init; } = 2;

    /// <summary>
    /// Nome estável e legível do detector.
    /// </summary>
    public string Detector { get; init; } = "";

    /// <summary>
    /// Família de evidência produzida pelo detector.
    /// </summary>
    public DetectorFamily Family { get; init; } =
        DetectorFamily.Unknown;

    /// <summary>
    /// Indica se o detector possuía condições técnicas para executar.
    /// </summary>
    public bool Aplicavel { get; init; }

    /// <summary>
    /// Decisão binária segundo o threshold interno do detector.
    /// Não representa, sozinha, a decisão final do ensemble.
    /// </summary>
    public bool VotouAnomalia { get; init; }

    /// <summary>
    /// Força contínua da evidência de comportamento anômalo.
    /// Faixa obrigatória: 0.0 a 1.0.
    /// </summary>
    public double Evidencia { get; init; }

    /// <summary>
    /// Intensidade física ou estatística do comportamento observado.
    /// Faixa obrigatória: 0.0 a 1.0.
    /// </summary>
    public double Severidade { get; init; }

    /// <summary>
    /// Qualidade e confiabilidade do cálculo realizado pelo detector.
    /// Não representa probabilidade de a anomalia ser real.
    /// Faixa obrigatória: 0.0 a 1.0.
    /// </summary>
    public double Confianca { get; init; }

    /// <summary>
    /// Peso configurado do detector antes dos ajustes do Ensemble V2.
    /// Faixa recomendada: 0.0 a 1.0.
    /// </summary>
    public double PesoBase { get; init; }

    /// <summary>
    /// Explicação técnica, sem afirmar causas industriais não comprovadas.
    /// </summary>
    public string Explicacao { get; init; } = "";

    /// <summary>
    /// Motivo pelo qual o detector não foi aplicável.
    /// Deve permanecer nulo quando Aplicavel for true.
    /// </summary>
    public string? MotivoNaoAplicavel { get; init; }

    /// <summary>
    /// Limites e referências usados pelo detector, quando disponíveis.
    /// </summary>
    public LimitesDetector? Limites { get; init; }

    /// <summary>
    /// Métricas técnicas para auditoria, benchmark e explicabilidade.
    /// </summary>
    public IReadOnlyDictionary<string, double> Metricas { get; init; } =
        new Dictionary<string, double>();

    /// <summary>
    /// Cria um resultado não aplicável coerente com o ADR001.
    /// </summary>
    public static DetectorResultV2 NaoAplicavel(
        string detector,
        DetectorFamily family,
        double pesoBase,
        string motivo)
    {
        return new DetectorResultV2
        {
            Detector = detector,
            Family = family,
            Aplicavel = false,
            VotouAnomalia = false,
            Evidencia = 0,
            Severidade = 0,
            Confianca = 0,
            PesoBase = Clamp01(pesoBase),
            Explicacao = motivo,
            MotivoNaoAplicavel = motivo
        };
    }

    /// <summary>
    /// Cria um resultado aplicável e normaliza os campos de 0 a 1.
    /// </summary>
    public static DetectorResultV2 AplicavelComResultado(
        string detector,
        DetectorFamily family,
        bool votouAnomalia,
        double evidencia,
        double severidade,
        double confianca,
        double pesoBase,
        string explicacao,
        LimitesDetector? limites = null,
        IReadOnlyDictionary<string, double>? metricas = null)
    {
        return new DetectorResultV2
        {
            Detector = detector,
            Family = family,
            Aplicavel = true,
            VotouAnomalia = votouAnomalia,
            Evidencia = Clamp01(evidencia),
            Severidade = Clamp01(severidade),
            Confianca = Clamp01(confianca),
            PesoBase = Clamp01(pesoBase),
            Explicacao = explicacao,
            MotivoNaoAplicavel = null,
            Limites = limites,
            Metricas =
                metricas ??
                new Dictionary<string, double>()
        };
    }

    /// <summary>
    /// Verifica as invariantes do contrato e devolve os problemas encontrados.
    /// Útil para testes e benchmark durante a migração.
    /// </summary>
    public IReadOnlyList<string> Validar()
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(Detector))
        {
            erros.Add("O nome do detector é obrigatório.");
        }

        if (Family == DetectorFamily.Unknown)
        {
            erros.Add("A família do detector não foi informada.");
        }

        ValidarFaixa(
            Evidencia,
            nameof(Evidencia),
            erros);

        ValidarFaixa(
            Severidade,
            nameof(Severidade),
            erros);

        ValidarFaixa(
            Confianca,
            nameof(Confianca),
            erros);

        ValidarFaixa(
            PesoBase,
            nameof(PesoBase),
            erros);

        if (!Aplicavel)
        {
            if (VotouAnomalia)
            {
                erros.Add(
                    "Um detector não aplicável não pode votar anomalia.");
            }

            if (Evidencia != 0 ||
                Severidade != 0 ||
                Confianca != 0)
            {
                erros.Add(
                    "Um detector não aplicável deve possuir evidência, " +
                    "severidade e confiança iguais a zero.");
            }

            if (string.IsNullOrWhiteSpace(
                    MotivoNaoAplicavel))
            {
                erros.Add(
                    "O motivo de não aplicabilidade é obrigatório.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(
                     MotivoNaoAplicavel))
        {
            erros.Add(
                "Um detector aplicável não deve possuir " +
                "MotivoNaoAplicavel.");
        }

        return erros;
    }

    private static double Clamp01(
        double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return Math.Clamp(
            value,
            0,
            1);
    }

    private static void ValidarFaixa(
        double value,
        string fieldName,
        ICollection<string> errors)
    {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value > 1)
        {
            errors.Add(
                $"{fieldName} deve ser um número finito entre 0 e 1.");
        }
    }
}
