namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV08.4B
///
/// Orquestra a qualificação operacional de um grupo multivariado usando:
/// - modelo persistido atual;
/// - backtest de referência;
/// - holdout posterior ao treinamento;
/// - MultivariateOperationalQualificationEvaluator.
///
/// Este serviço NÃO:
/// - altera ApprovedGroups;
/// - altera readiness;
/// - habilita promoção;
/// - altera RuntimeDecision;
/// - treina ou sobrescreve modelo.
/// </summary>
public sealed class OperationalMultivariateQualificationService
{
    private readonly MultivariateModelPersistenceService
        _modelPersistence;

    private readonly OperationalMultivariateBacktestService
        _backtestService;

    private readonly MultivariateOperationalQualificationEvaluator
        _qualificationEvaluator;

    private readonly MultivariateIndustrialEventReviewStore
        _reviewStore;

    private readonly MultivariateIndustrialReviewAssessmentService
        _reviewAssessmentService;

    public OperationalMultivariateQualificationService(
        MultivariateModelPersistenceService modelPersistence,
        OperationalMultivariateBacktestService backtestService,
        MultivariateOperationalQualificationEvaluator qualificationEvaluator,
        MultivariateIndustrialEventReviewStore reviewStore,
        MultivariateIndustrialReviewAssessmentService reviewAssessmentService)
    {
        _modelPersistence =
            modelPersistence ??
            throw new ArgumentNullException(
                nameof(modelPersistence));

        _backtestService =
            backtestService ??
            throw new ArgumentNullException(
                nameof(backtestService));

        _qualificationEvaluator =
            qualificationEvaluator ??
            throw new ArgumentNullException(
                nameof(qualificationEvaluator));

        _reviewStore =
            reviewStore ??
            throw new ArgumentNullException(
                nameof(reviewStore));

        _reviewAssessmentService =
            reviewAssessmentService ??
            throw new ArgumentNullException(
                nameof(reviewAssessmentService));
    }

    public async Task<OperationalMultivariateQualificationRunResult>
        QualificarAsync(
            string clienteId,
            string groupName,
            DateTime referenciaInicio,
            DateTime referenciaFim,
            DateTime holdoutInicio,
            DateTime holdoutFim,
            double anomalyScoreThreshold = 75.0,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            clienteId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            groupName);

        if (referenciaFim <=
            referenciaInicio)
        {
            throw new ArgumentException(
                "A data final da referência deve ser maior que a data inicial.",
                nameof(referenciaFim));
        }

        if (holdoutFim <=
            holdoutInicio)
        {
            throw new ArgumentException(
                "A data final do holdout deve ser maior que a data inicial.",
                nameof(holdoutFim));
        }

        if (holdoutInicio <
            referenciaFim)
        {
            throw new ArgumentException(
                "A janela de holdout não pode sobrepor a janela de referência.",
                nameof(holdoutInicio));
        }

        if (!double.IsFinite(
                anomalyScoreThreshold) ||
            anomalyScoreThreshold <= 0 ||
            anomalyScoreThreshold >= 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(anomalyScoreThreshold),
                "O threshold deve estar no intervalo (0, 100).");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var clienteNormalizado =
            clienteId.Trim();

        var grupoNormalizado =
            groupName.Trim();

        var model =
            await _modelPersistence
                .CarregarAsync(
                    clienteNormalizado,
                    grupoNormalizado,
                    cancellationToken);

        if (model is null)
        {
            throw new InvalidOperationException(
                $"Nenhum modelo multivariado treinado foi encontrado " +
                $"para o grupo '{grupoNormalizado}'.");
        }

        /*
         * Executa as duas janelas separadamente.
         *
         * OperationalMultivariateBacktestService usa estado temporal
         * privado por execução, portanto referência e holdout não
         * compartilham buffer.
         */
        var referencia =
            await _backtestService
                .ExecutarAsync(
                    clienteNormalizado,
                    grupoNormalizado,
                    referenciaInicio,
                    referenciaFim,
                    anomalyScoreThreshold,
                    cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var holdout =
            await _backtestService
                .ExecutarAsync(
                    clienteNormalizado,
                    grupoNormalizado,
                    holdoutInicio,
                    holdoutFim,
                    anomalyScoreThreshold,
                    cancellationToken);

        // MV08.4C — pacote auditável para revisão humana dos eventos.
        // Usa somente os eventos anômalos já presentes nos TopEvents.
        var industrialValidationEvidence =
            new MultivariateIndustrialValidationEvidenceBuilder()
                .Build(
                    model,
                    referencia.Summary,
                    holdout.Summary,
                    anomalyScoreThreshold);

        // MV08.4D2A — a validação industrial deixa de ser informada
        // manualmente e passa a ser derivada das revisões persistidas.
        var persistedReviews =
            await _reviewStore
                .ListAsync(
                    clienteNormalizado,
                    grupoNormalizado,
                    model.Version,
                    model.TrainedAtUtc,
                    anomalyScoreThreshold,
                    cancellationToken);

        var industrialReviewAssessment =
            _reviewAssessmentService
                .Assess(
                    industrialValidationEvidence,
                    persistedReviews);

        var qualification =
            _qualificationEvaluator
                .Evaluate(
                    new MultivariateOperationalQualificationContext
                    {
                        Model =
                            model,

                        ReferenceBacktest =
                            referencia.Summary,

                        HoldoutBacktest =
                            holdout.Summary,

                        HoldoutStartUtc =
                            holdoutInicio,

                        HoldoutEndUtc =
                            holdoutFim,

                        IndustrialValidationConfirmed =
                            industrialReviewAssessment
                                .IndustrialValidationConfirmed,

                        IndustrialValidationReference =
                            industrialReviewAssessment
                                .IndustrialValidationReference
                    });

        return new OperationalMultivariateQualificationRunResult
        {
            ClienteId =
                clienteNormalizado,

            GroupName =
                grupoNormalizado,

            Threshold =
                anomalyScoreThreshold,

            ModelVersion =
                model.Version,

            ModelAlgorithm =
                model.Algorithm,

            ModelTrainedAtUtc =
                model.TrainedAtUtc,

            ModelSampleCount =
                model.SampleCount,

            ReferenciaInicio =
                referenciaInicio,

            ReferenciaFim =
                referenciaFim,

            HoldoutInicio =
                holdoutInicio,

            HoldoutFim =
                holdoutFim,

            ReferenceSummary =
                referencia.Summary,

            HoldoutSummary =
                holdout.Summary,

            Qualification =
                qualification,

            IndustrialValidationEvidence =
                industrialValidationEvidence,

            IndustrialReviewAssessment =
                industrialReviewAssessment
        };
    }
}

public sealed class OperationalMultivariateQualificationRunResult
{
    public string ClienteId { get; init; } =
        "";

    public string GroupName { get; init; } =
        "";

    public double Threshold { get; init; }

    public int ModelVersion { get; init; }

    public string ModelAlgorithm { get; init; } =
        "";

    public DateTime ModelTrainedAtUtc { get; init; }

    public long ModelSampleCount { get; init; }

    public DateTime ReferenciaInicio { get; init; }

    public DateTime ReferenciaFim { get; init; }

    public DateTime HoldoutInicio { get; init; }

    public DateTime HoldoutFim { get; init; }

    public MultivariateHistoricalBacktestSummary
        ReferenceSummary { get; init; } =
            new();

    public MultivariateHistoricalBacktestSummary
        HoldoutSummary { get; init; } =
            new();

    public MultivariateOperationalQualificationResult
        Qualification { get; init; } =
            new();

    public MultivariateIndustrialValidationEvidence
        IndustrialValidationEvidence { get; init; } =
            new();

    public MultivariateIndustrialReviewAssessmentResult
        IndustrialReviewAssessment { get; init; } =
            new();
}