namespace IAApi.Benchmark.Cli;

public sealed record BenchmarkCommandSelection
{
    public bool PersistenceTemporalExperiment { get; init; }

    public bool ExecutarTesteGrupos { get; init; }
    public bool ExecutarTesteBufferMultivariado { get; init; }
    public bool ExecutarTesteDatasetMultivariado { get; init; }
    public bool ExecutarTesteDetectorMultivariado { get; init; }
    public bool ExecutarTreinamentoMultivariado { get; init; }

    public bool ExecutarBenchmark { get; init; }
    public bool ExecutarBenchmarkClean { get; init; }
    public bool ExecutarBenchmarkFast { get; init; }
    public bool ExecutarBenchmarkCleanFast { get; init; }

    public bool ExecutarAvaliacao { get; init; }
    public int IndiceEvaluate { get; init; }

    public bool ExecutarAvaliacaoFunctionalOracle { get; init; }
    public int IndiceEvaluateFunctionalOracle { get; init; }

    public bool ExecutarAvaliacaoClean { get; init; }
    public int IndiceEvaluateClean { get; init; }

    public bool ExecutarInvestigacaoCleanAll { get; init; }
    public bool ExecutarAvaliacaoImpactoDeadband { get; init; }
    public bool ExecutarAnaliseCausaRaiz { get; init; }
    public bool ExecutarAnaliseResultados { get; init; }
    public bool ExecutarAnaliseSensibilidadeDetectores { get; init; }
    public bool ExecutarAdaptiveEnsembleLab { get; init; }
    public bool ExecutarFamilyConsensusReport { get; init; }
    public bool ExecutarConfidenceBenchmark { get; init; }

    public bool UsarGruposMultivariadosDoBenchmark =>
        ExecutarBenchmark ||
        ExecutarBenchmarkClean ||
        ExecutarBenchmarkFast ||
        ExecutarBenchmarkCleanFast ||
        ExecutarTesteGrupos ||
        ExecutarTesteBufferMultivariado ||
        ExecutarTesteDatasetMultivariado ||
        ExecutarTesteDetectorMultivariado ||
        ExecutarTreinamentoMultivariado;

    public int QuantidadeModosSelecionados =>
        (ExecutarBenchmark ? 1 : 0) +
        (ExecutarBenchmarkClean ? 1 : 0) +
        (ExecutarBenchmarkFast ? 1 : 0) +
        (ExecutarBenchmarkCleanFast ? 1 : 0) +
        (ExecutarAvaliacao ? 1 : 0) +
        (ExecutarAvaliacaoFunctionalOracle ? 1 : 0) +
        (ExecutarAvaliacaoClean ? 1 : 0) +
        (ExecutarInvestigacaoCleanAll ? 1 : 0) +
        (ExecutarTesteGrupos ? 1 : 0) +
        (ExecutarTesteBufferMultivariado ? 1 : 0) +
        (ExecutarTesteDatasetMultivariado ? 1 : 0) +
        (ExecutarTesteDetectorMultivariado ? 1 : 0) +
        (ExecutarAvaliacaoImpactoDeadband ? 1 : 0) +
        (ExecutarAnaliseCausaRaiz ? 1 : 0) +
        (ExecutarAnaliseResultados ? 1 : 0) +
        (ExecutarAnaliseSensibilidadeDetectores ? 1 : 0) +
        (ExecutarAdaptiveEnsembleLab ? 1 : 0) +
        (ExecutarFamilyConsensusReport ? 1 : 0) +
        (ExecutarConfidenceBenchmark ? 1 : 0) +
        (ExecutarTreinamentoMultivariado ? 1 : 0);

    public static BenchmarkCommandSelection Parse(string[] args)
    {
        var indiceEvaluate = IndexOf(args, "--evaluate");
        var indiceEvaluateFunctionalOracle =
            IndexOf(args, "--evaluate-functional-oracle");
        var indiceEvaluateClean = IndexOf(args, "--evaluate-clean");

        return new BenchmarkCommandSelection
        {
            PersistenceTemporalExperiment =
                Has(args, "--experimental-persistence-temporal-confirmation"),

            ExecutarTesteGrupos = Has(args, "--test-groups"),
            ExecutarTesteBufferMultivariado =
                Has(args, "--test-multivariate-buffer"),
            ExecutarTesteDatasetMultivariado =
                Has(args, "--test-multivariate-dataset"),
            ExecutarTesteDetectorMultivariado =
                Has(args, "--test-multivariate-detector"),
            ExecutarTreinamentoMultivariado =
                Has(args, "--train-multivariate"),

            ExecutarBenchmark = Has(args, "--benchmark"),
            ExecutarBenchmarkClean = Has(args, "--benchmark-clean"),
            ExecutarBenchmarkFast = Has(args, "--benchmark-fast"),
            ExecutarBenchmarkCleanFast =
                Has(args, "--benchmark-clean-fast"),

            IndiceEvaluate = indiceEvaluate,
            ExecutarAvaliacao = indiceEvaluate >= 0,

            IndiceEvaluateFunctionalOracle =
                indiceEvaluateFunctionalOracle,
            ExecutarAvaliacaoFunctionalOracle =
                indiceEvaluateFunctionalOracle >= 0,

            IndiceEvaluateClean = indiceEvaluateClean,
            ExecutarAvaliacaoClean = indiceEvaluateClean >= 0,

            ExecutarInvestigacaoCleanAll =
                Has(args, "--investigate-clean-all"),
            ExecutarAvaliacaoImpactoDeadband =
                Has(args, "--evaluate-deadband-impact"),
            ExecutarAnaliseCausaRaiz =
                Has(args, "--analyze-false-positive-causes"),
            ExecutarAnaliseResultados =
                Has(args, "--analyze-results"),
            ExecutarAnaliseSensibilidadeDetectores =
                Has(args, "--analyze-detector-sensitivity"),
            ExecutarAdaptiveEnsembleLab =
                Has(args, "--adaptive-ensemble-lab"),
            ExecutarFamilyConsensusReport =
                Has(args, "--family-consensus-report"),
            ExecutarConfidenceBenchmark =
                Has(args, "--confidence-benchmark")
        };
    }

    private static bool Has(string[] args, string flag) =>
        args.Contains(flag, StringComparer.OrdinalIgnoreCase);

    private static int IndexOf(string[] args, string flag) =>
        Array.FindIndex(
            args,
            argument => string.Equals(
                argument,
                flag,
                StringComparison.OrdinalIgnoreCase));
}