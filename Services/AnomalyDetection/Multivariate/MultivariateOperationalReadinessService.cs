using IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// B3.24.2 / MV07.2
///
/// Compõe o readiness operacional real de um grupo multivariado:
/// configuração operacional ativa + modelo persistido + MV07.1 evaluator.
///
/// Esta classe NÃO:
/// - altera RuntimeDecision;
/// - altera PredictionService;
/// - habilita promoção;
/// - registra configuração no DI;
/// - modifica o modelo persistido.
/// </summary>
public sealed class MultivariateOperationalReadinessService
{
    private readonly IGrupoAnaliseProvider _grupoProvider;
    private readonly MultivariateModelPersistenceService _modelPersistence;
    private readonly MultivariateOperationalReadinessEvaluator _evaluator;

    public MultivariateOperationalReadinessService(
        IGrupoAnaliseProvider grupoProvider,
        MultivariateModelPersistenceService modelPersistence,
        MultivariateOperationalReadinessEvaluator evaluator)
    {
        _grupoProvider =
            grupoProvider ??
            throw new ArgumentNullException(
                nameof(grupoProvider));

        _modelPersistence =
            modelPersistence ??
            throw new ArgumentNullException(
                nameof(modelPersistence));

        _evaluator =
            evaluator ??
            throw new ArgumentNullException(
                nameof(evaluator));
    }

    public async Task<MultivariateOperationalReadinessResult>
        AvaliarAsync(
            string clienteId,
            string groupName,
            MultivariateOperationalReadinessOptions options,
            DateTime? nowUtc = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            clienteId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            groupName);

        ArgumentNullException.ThrowIfNull(
            options);

        cancellationToken.ThrowIfCancellationRequested();

        var clienteNormalizado =
            clienteId.Trim();

        var grupoNormalizado =
            groupName.Trim();

        List<GrupoAnaliseDto> grupos;

        try
        {
            grupos =
                await _grupoProvider
                    .CarregarGruposAsync(
                        clienteNormalizado);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return CriarFalhaFechada(
                clienteNormalizado,
                grupoNormalizado,
                options,
                "FalhaAoCarregarConfiguracaoDoGrupo",
                modelPresent:
                    false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var grupo =
            grupos.FirstOrDefault(
                item =>
                    string.Equals(
                        item.Nome?.Trim(),
                        grupoNormalizado,
                        StringComparison.OrdinalIgnoreCase));

        if (grupo is null)
        {
            /*
             * O provider operacional expõe apenas grupos ativos.
             * Portanto ausência aqui significa que o grupo solicitado
             * não está disponível para autoridade operacional.
             */
            return _evaluator.Evaluate(
                new MultivariateOperationalReadinessContext
                {
                    ClienteId =
                        clienteNormalizado,

                    GroupIsActive =
                        true,

                    Group =
                        null,

                    Model =
                        null,

                    NowUtc =
                        ResolverNowUtc(nowUtc)
                },
                options);
        }

        MultivariateGroupModel? model;

        try
        {
            model =
                await _modelPersistence
                    .CarregarAsync(
                        clienteNormalizado,
                        grupo.Nome,
                        cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return CriarFalhaFechada(
                clienteNormalizado,
                grupo.Nome,
                options,
                "FalhaAoCarregarModelo",
                modelPresent:
                    _modelPersistence.ExisteModelo(
                        clienteNormalizado,
                        grupo.Nome));
        }

        var snapshot =
            model is null
                ? null
                : CriarSnapshot(model);

        return _evaluator.Evaluate(
            new MultivariateOperationalReadinessContext
            {
                ClienteId =
                    clienteNormalizado,

                /*
                 * O provider operacional atual já filtra apenas
                 * configurações Ativo=true.
                 */
                GroupIsActive =
                    true,

                Group =
                    grupo,

                Model =
                    snapshot,

                NowUtc =
                    ResolverNowUtc(nowUtc)
            },
            options);
    }

    internal static MultivariateOperationalModelSnapshot
        CriarSnapshot(
            MultivariateGroupModel model)
    {
        ArgumentNullException.ThrowIfNull(
            model);

        return new MultivariateOperationalModelSnapshot
        {
            ClienteId =
                model.ClienteId,

            GroupName =
                model.GroupName,

            TrainedAtUtc =
                model.TrainedAtUtc,

            SampleCount =
                model.SampleCount,

            MinimumAvailableTags =
                model.MinimumAvailableTags,

            MaxLagMinutes =
                model.MaxLagMinutes,

            Tags =
                model.Tags.ToList(),

            Means =
                model.Means.ToList(),

            StandardDeviations =
                model.StandardDeviations.ToList(),

            CovarianceMatrix =
                model.CovarianceMatrix
                    .Select(row =>
                        row.ToList())
                    .ToList(),

            CorrelationMatrix =
                model.CorrelationMatrix
                    .Select(row =>
                        row.ToList())
                    .ToList()
        };
    }

    private static DateTime ResolverNowUtc(
        DateTime? nowUtc)
    {
        if (!nowUtc.HasValue)
        {
            return DateTime.UtcNow;
        }

        if (nowUtc.Value.Kind ==
            DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(
                nowUtc.Value,
                DateTimeKind.Utc);
        }

        return nowUtc.Value.ToUniversalTime();
    }

    private static MultivariateOperationalReadinessResult
        CriarFalhaFechada(
            string clienteId,
            string groupName,
            MultivariateOperationalReadinessOptions options,
            string blocker,
            bool modelPresent)
    {
        return new MultivariateOperationalReadinessResult
        {
            ClienteId =
                clienteId,

            GroupName =
                groupName,

            ReadyForOperationalAuthority =
                false,

            ExplicitlyApproved =
                EstaExplicitamenteAprovado(
                    clienteId,
                    groupName,
                    options),

            ModelPresent =
                modelPresent,

            ModelSampleCount =
                0,

            ModelTrainedAtUtc =
                null,

            MaximumAbsoluteLinearCorrelation =
                null,

            Blockers =
                [blocker],

            Warnings =
                Array.Empty<string>()
        };
    }

    private static bool EstaExplicitamenteAprovado(
        string clienteId,
        string groupName,
        MultivariateOperationalReadinessOptions options)
    {
        if (!options.RequireExplicitApproval)
        {
            return true;
        }

        var expected =
            $"{clienteId.Trim()}::{groupName.Trim()}";

        return options.ApprovedGroups.Any(
            item =>
                !string.IsNullOrWhiteSpace(item) &&
                string.Equals(
                    item.Trim(),
                    expected,
                    StringComparison.OrdinalIgnoreCase));
    }
}