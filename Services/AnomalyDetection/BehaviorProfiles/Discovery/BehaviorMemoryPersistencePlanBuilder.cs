using IAApi.Services.AnomalyDetection;

namespace IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;

/// <summary>
/// Plano puro para persistência de um comportamento conhecido.
///
/// As FKs dos filhos permanecem zero nesta etapa porque o Id do comportamento
/// somente existe após a inserção no banco. O serviço transacional posterior
/// preencherá TagComportamentoIaId antes de gravar ocorrências e snapshot.
/// </summary>
public sealed record BehaviorMemoryPersistencePlan
{
    public TagComportamentoIa Behavior { get; init; } =
        new();

    public IReadOnlyList<TagComportamentoOcorrenciaIa> Occurrences { get; init; } =
        Array.Empty<TagComportamentoOcorrenciaIa>();

    public TagComportamentoSnapshotIa Snapshot { get; init; } =
        new();
}

/// <summary>
/// Monta os modelos persistentes já existentes a partir de uma identidade
/// comportamental conhecida.
///
/// Esta classe não acessa Entity Framework e não chama SaveChanges.
/// </summary>
public sealed class BehaviorMemoryPersistencePlanBuilder
{
    public BehaviorMemoryPersistencePlan Build(
        string clienteId,
        string tagName,
        BehaviorIdentityProposal proposal,
        BehaviorIdentityConfidenceResult identityConfidence,
        int snapshotVersion = 1,
        DateTime? createdAtUtc = null)
    {
        ValidateText(
            clienteId,
            nameof(clienteId));

        ValidateText(
            tagName,
            nameof(tagName));

        ArgumentNullException.ThrowIfNull(
            proposal);

        ArgumentNullException.ThrowIfNull(
            identityConfidence);

        if (!proposal.HasStableIdentityProposal ||
            proposal.Lifecycle != BehaviorMemoryLifecycle.Known ||
            string.IsNullOrWhiteSpace(
                proposal.ProposedIdentity))
        {
            throw new InvalidOperationException(
                "Somente uma identidade Known estável proposta ou reconciliada pode gerar plano de persistência.");
        }

        if (!identityConfidence.CanEvaluate)
        {
            throw new InvalidOperationException(
                "A confiança de identidade deve estar validada antes da persistência.");
        }

        if (snapshotVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshotVersion),
                "A versão do snapshot deve ser maior ou igual a 1.");
        }

        var candidate =
            proposal.Evaluation.Candidate;

        var occurrences =
            candidate.Occurrences
                .Where(x =>
                    x is not null &&
                    x.IsCharacterizable)
                .OrderBy(x =>
                    x.Inicio)
                .ThenBy(x =>
                    x.SourceSegmentIndex)
                .ThenBy(x =>
                    x.EpisodeIndex)
                .ToArray();

        if (occurrences.Length == 0)
        {
            throw new InvalidOperationException(
                "O comportamento não possui ocorrências caracterizáveis para persistir.");
        }

        if (occurrences.Length !=
            candidate.OccurrenceCount)
        {
            throw new InvalidOperationException(
                "A quantidade de ocorrências do candidato não corresponde às ocorrências caracterizáveis disponíveis.");
        }

        var confidenceByOccurrence =
            identityConfidence.Occurrences
                .ToDictionary(
                    x => (
                        x.SourceSegmentIndex,
                        x.EpisodeIndex));

        foreach (var occurrence in occurrences)
        {
            var key =
                (
                    occurrence.SourceSegmentIndex,
                    occurrence.EpisodeIndex
                );

            if (!confidenceByOccurrence.ContainsKey(
                    key))
            {
                throw new InvalidOperationException(
                    $"Não existe confiança de identidade para a ocorrência {key.SourceSegmentIndex}/{key.EpisodeIndex}.");
            }
        }

        var createdAt =
            NormalizeUtc(
                createdAtUtc ??
                DateTime.UtcNow);

        var totalSamples =
            occurrences.Sum(x =>
                x.UsedSampleCount);

        var behavior =
            new TagComportamentoIa
            {
                ClienteId =
                    clienteId,

                TagName =
                    tagName,

                Codigo =
                    proposal.ProposedIdentity!,

                Status =
                    TagComportamentoIa.StatusKnown,

                Origem =
                    TagComportamentoIa.OrigemAutomatic,

                Classificacao =
                    TagComportamentoIa.ClassificacaoUndefined,

                PrimeiraObservacao =
                    occurrences.Min(x =>
                        x.Inicio),

                UltimaObservacao =
                    occurrences.Max(x =>
                        x.Fim),

                QuantidadeOcorrencias =
                    occurrences.Length,

                TotalAmostras =
                    totalSamples,

                Confianca =
                    ClampConfidence(
                        identityConfidence.Confidence),

                DataCriacao =
                    createdAt,

                DataAtualizacao =
                    null
            };

        var persistedOccurrences =
            occurrences
                .Select(occurrence =>
                {
                    var confidence =
                        confidenceByOccurrence[
                            (
                                occurrence.SourceSegmentIndex,
                                occurrence.EpisodeIndex
                            )];

                    return new TagComportamentoOcorrenciaIa
                    {
                        TagComportamentoIaId =
                            0,

                        DataHoraInicio =
                            occurrence.Inicio,

                        DataHoraFim =
                            occurrence.Fim,

                        QuantidadeAmostras =
                            occurrence.UsedSampleCount,

                        Confianca =
                            ClampConfidence(
                                confidence.Confidence),

                        UtilizadaNoAprendizado =
                            true,

                        DataCriacao =
                            createdAt
                    };
                })
                .ToArray();

        var snapshot =
            BuildSnapshotPreview(
                occurrences,
                snapshotVersion,
                createdAt);

        return new BehaviorMemoryPersistencePlan
        {
            Behavior =
                behavior,

            Occurrences =
                persistedOccurrences,

            Snapshot =
                snapshot
        };
    }

    /// <summary>
    /// Produz um snapshot agregado sem persistir dados.
    ///
    /// É reutilizado tanto pelo plano de persistência quanto pela
    /// reconciliação com memórias já existentes.
    /// </summary>
    public TagComportamentoSnapshotIa BuildSnapshotPreview(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> occurrences,
        int version = 1,
        DateTime? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(
            occurrences);

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A versão do snapshot deve ser maior ou igual a 1.");
        }

        var eligible =
            occurrences
                .Where(x =>
                    x is not null &&
                    x.IsCharacterizable)
                .OrderBy(x =>
                    x.Inicio)
                .ThenBy(x =>
                    x.SourceSegmentIndex)
                .ThenBy(x =>
                    x.EpisodeIndex)
                .ToArray();

        if (eligible.Length == 0)
        {
            throw new InvalidOperationException(
                "O snapshot exige ao menos uma ocorrência caracterizável.");
        }

        return BuildSnapshot(
            eligible,
            version,
            NormalizeUtc(
                createdAtUtc ??
                DateTime.UtcNow));
    }

    private static TagComportamentoSnapshotIa BuildSnapshot(
        IReadOnlyList<BehaviorCandidateEpisodeCharacteristics> occurrences,
        int version,
        DateTime createdAtUtc)
    {
        var totalUsed =
            occurrences.Sum(x =>
                x.UsedSampleCount);

        if (totalUsed <= 0)
        {
            throw new InvalidOperationException(
                "O snapshot exige ao menos uma amostra utilizada.");
        }

        /*
         * Média ponderada pelas amostras efetivamente utilizadas.
         */
        var mean =
            occurrences.Sum(x =>
                x.Media *
                x.UsedSampleCount) /
            totalUsed;

        /*
         * Variância populacional combinada:
         *
         * Var(total) =
         *   soma(n_i * (var_i + (media_i - media_total)^2)) / N
         *
         * Isso preserva a dispersão intraepisódio e também a pequena
         * diferença entre centros das ocorrências do mesmo comportamento.
         */
        var variance =
            occurrences.Sum(x =>
                x.UsedSampleCount *
                (
                    Math.Pow(
                        x.DesvioPadrao,
                        2) +
                    Math.Pow(
                        x.Media -
                        mean,
                        2)
                )) /
            totalUsed;

        var minimum =
            occurrences.Min(x =>
                x.Minimo);

        var maximum =
            occurrences.Max(x =>
                x.Maximo);

        var zeroPercentage =
            occurrences.Sum(x =>
                x.PercentualZeros *
                x.UsedSampleCount) /
            totalUsed;

        /*
         * VariacaoMedia usa apenas transições internas a cada episódio.
         * Não criamos uma transição artificial entre episódios separados.
         */
        var transitionCount =
            occurrences.Sum(x =>
                Math.Max(
                    0,
                    x.UsedSampleCount - 1));

        var averageVariation =
            transitionCount > 0
                ? occurrences.Sum(x =>
                    x.VariacaoMedia *
                    Math.Max(
                        0,
                        x.UsedSampleCount - 1)) /
                  transitionCount
                : 0;

        var deadband =
            DetectorMath.Median(
                occurrences.Select(x =>
                    x.DeadbandRecomendado));

        var deadbandConfidence =
            occurrences.Sum(x =>
                x.ConfiancaDeadband *
                x.UsedSampleCount) /
            totalUsed;

        /*
         * A assinatura rica só é gravada quando TODAS as ocorrências
         * possuem ProfileFeatures completas.
         *
         * Isso mantém compatibilidade com dados/testes legados e evita
         * persistir uma identidade parcialmente preenchida.
         *
         * Para o agregado usamos a mediana entre episódios. É uma escolha
         * deliberadamente robusta: não cria transições artificiais entre
         * ocorrências separadas e não deixa um episódio isolado dominar a
         * identidade persistida.
         */
        var hasCompleteRichIdentity =
            occurrences.All(x =>
                x.ProfileFeatures is not null);

        double? median =
            null;

        double? firstQuartile =
            null;

        double? thirdQuartile =
            null;

        double? interquartileRange =
            null;

        double? robustScale =
            null;

        double? typicalAbsoluteStep =
            null;

        if (hasCompleteRichIdentity)
        {
            median =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.Mediana));

            firstQuartile =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.PrimeiroQuartil));

            thirdQuartile =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.TerceiroQuartil));

            interquartileRange =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.IntervaloInterquartil));

            robustScale =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.ProfileFeatures!.RobustScale));

            typicalAbsoluteStep =
                DetectorMath.Median(
                    occurrences.Select(x =>
                        x.ProfileFeatures!.TypicalAbsoluteStep));
        }

        return new TagComportamentoSnapshotIa
        {
            TagComportamentoIaId =
                0,

            Versao =
                version,

            Media =
                mean,

            DesvioPadrao =
                Math.Sqrt(
                    Math.Max(
                        0,
                        variance)),

            Mediana =
                median,

            PrimeiroQuartil =
                firstQuartile,

            TerceiroQuartil =
                thirdQuartile,

            IntervaloInterquartil =
                interquartileRange,

            RobustScale =
                robustScale,

            TypicalAbsoluteStep =
                typicalAbsoluteStep,

            Minimo =
                minimum,

            Maximo =
                maximum,

            Amplitude =
                maximum -
                minimum,

            PercentualZeros =
                zeroPercentage,

            VariacaoMedia =
                averageVariation,

            QuantidadePicos =
                occurrences.Sum(x =>
                    CountEpisodePeaks(
                        x)),

            TotalRegistrosHistorico =
                occurrences.Sum(x =>
                    x.OriginalSampleCount),

            TotalRegistrosUsados =
                totalUsed,

            TotalOutliersRemovidos =
                occurrences.Sum(x =>
                    x.RemovedOutliers),

            DeadbandRecomendado =
                deadband,

            ConfiancaDeadband =
                Math.Clamp(
                    deadbandConfidence,
                    0,
                    100),

            JustificativaDeadband =
                $"Snapshot agregado de {occurrences.Count} ocorrências do mesmo comportamento. " +
                "O deadband recomendado é a mediana dos deadbands calculados dentro de cada episódio; " +
                "a variação média considera somente transições intraepisódio, evitando saltos artificiais entre ocorrências.",

            PeriodoInicio =
                occurrences.Min(x =>
                    x.Inicio),

            PeriodoFim =
                occurrences.Max(x =>
                    x.Fim),

            DataCriacao =
                createdAtUtc
        };
    }

    /*
     * BehaviorCandidateEpisodeCharacteristics não persiste atualmente
     * QuantidadePicos. Para não inventar um valor impossível de recuperar,
     * contamos picos apenas quando o histórico limpo está disponível,
     * usando a mesma regra média +/- 3 desvios padrão do calculador central.
     */
    private static int CountEpisodePeaks(
        BehaviorCandidateEpisodeCharacteristics occurrence)
    {
        if (occurrence.HistoricoLimpo.Count == 0 ||
            occurrence.DesvioPadrao < 0 ||
            !double.IsFinite(
                occurrence.DesvioPadrao))
        {
            return 0;
        }

        var upper =
            occurrence.Media +
            (3 *
             occurrence.DesvioPadrao);

        var lower =
            occurrence.Media -
            (3 *
             occurrence.DesvioPadrao);

        return occurrence.HistoricoLimpo
            .Select(x =>
                Convert.ToDouble(
                    x.Valor))
            .Where(double.IsFinite)
            .Count(value =>
                value > upper ||
                value < lower);
    }

    private static double ClampConfidence(
        double value)
    {
        if (!double.IsFinite(
                value))
        {
            return 0;
        }

        return Math.Clamp(
            value,
            0,
            100);
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }

    private static void ValidateText(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "O valor é obrigatório.",
                parameterName);
        }
    }
}
