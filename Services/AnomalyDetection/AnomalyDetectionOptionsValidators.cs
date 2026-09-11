using Microsoft.Extensions.Options;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Validação central das opções dos detectores.
/// Impede a aplicação de iniciar/usar detectores com configuração
/// matematicamente inválida.
/// </summary>
public sealed class AnomalyDetectionOptionsValidator :
    IValidateOptions<AnomalyDetectionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        AnomalyDetectionOptions options)
    {
        var errors = new List<string>();

        Positive(options.EnsembleThreshold, nameof(options.EnsembleThreshold), errors);
        if (options.EnsembleThreshold > 100)
            errors.Add("EnsembleThreshold deve estar no intervalo (0, 100].");

        if (options.MinHistoryPoints < 3)
            errors.Add("MinHistoryPoints deve ser >= 3.");

        Weight(options.ZScoreWeight, nameof(options.ZScoreWeight), errors);
        Weight(options.MadWeight, nameof(options.MadWeight), errors);
        Weight(options.EwmaWeight, nameof(options.EwmaWeight), errors);
        Weight(options.RateOfChangeWeight, nameof(options.RateOfChangeWeight), errors);
        Weight(options.PersistenceWeight, nameof(options.PersistenceWeight), errors);
        Weight(options.LevelShiftWeight, nameof(options.LevelShiftWeight), errors);

        var weightSum =
            options.ZScoreWeight + options.MadWeight + options.EwmaWeight +
            options.RateOfChangeWeight + options.PersistenceWeight +
            options.LevelShiftWeight;

        if (!double.IsFinite(weightSum) || weightSum <= 0)
            errors.Add("A soma dos pesos dos detectores deve ser > 0.");

        Positive(options.ZScoreThreshold, nameof(options.ZScoreThreshold), errors);
        Positive(options.MadThreshold, nameof(options.MadThreshold), errors);
        Positive(options.EwmaSigmaLimit, nameof(options.EwmaSigmaLimit), errors);
        Positive(options.RateOfChangeThreshold, nameof(options.RateOfChangeThreshold), errors);
        Positive(options.LevelShiftThreshold, nameof(options.LevelShiftThreshold), errors);

        if (!double.IsFinite(options.EwmaLambda) ||
            options.EwmaLambda <= 0 || options.EwmaLambda > 1)
            errors.Add("EwmaLambda deve estar no intervalo (0, 1].");

        if (options.PersistenceMinimumPoints < 2)
            errors.Add("PersistenceMinimumPoints deve ser >= 2.");

        if (!double.IsFinite(options.PersistenceMinimumMinutes) ||
            options.PersistenceMinimumMinutes < 0)
            errors.Add("PersistenceMinimumMinutes deve ser >= 0.");

        if (options.LevelShiftPreviousWindow < 1)
            errors.Add("LevelShiftPreviousWindow deve ser >= 1 minuto.");

        if (options.LevelShiftRecentWindow < 1)
            errors.Add("LevelShiftRecentWindow deve ser >= 1 minuto.");

        if (!double.IsFinite(options.LevelShiftMinimumConsistency) ||
            options.LevelShiftMinimumConsistency <= 0 ||
            options.LevelShiftMinimumConsistency > 1)
            errors.Add("LevelShiftMinimumConsistency deve estar no intervalo (0, 1].");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void Positive(double value, string name, List<string> errors)
    {
        if (!double.IsFinite(value) || value <= 0)
            errors.Add($"{name} deve ser finito e > 0.");
    }

    private static void Weight(double value, string name, List<string> errors)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
            errors.Add($"{name} deve estar no intervalo [0, 1].");
    }
}

public sealed class IsolationForestOptionsValidator :
    IValidateOptions<IsolationForestOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        IsolationForestOptions options)
    {
        var errors = new List<string>();

        if (!double.IsFinite(options.Weight) ||
            options.Weight < 0 || options.Weight > 1)
            errors.Add("IsolationForest.Weight deve estar no intervalo [0, 1].");

        if (options.TreeCount < 10)
            errors.Add("IsolationForest.TreeCount deve ser >= 10.");

        if (options.SampleSize < 2)
            errors.Add("IsolationForest.SampleSize deve ser >= 2.");

        if (options.MinimumHistoryPoints < 10)
            errors.Add("IsolationForest.MinimumHistoryPoints deve ser >= 10.");

        if (options.FeatureWindowSize < 3)
            errors.Add("IsolationForest.FeatureWindowSize deve ser >= 3.");

        if (options.MinimumHistoryPoints <= options.FeatureWindowSize)
            errors.Add(
                "IsolationForest.MinimumHistoryPoints deve ser maior que FeatureWindowSize.");

        if (!double.IsFinite(options.AnomalyThreshold) ||
            options.AnomalyThreshold <= 0 ||
            options.AnomalyThreshold >= 1)
            errors.Add(
                "IsolationForest.AnomalyThreshold deve estar no intervalo (0, 1).");

        if (!double.IsFinite(options.NormalizationSlope) ||
            options.NormalizationSlope <= 0 ||
            options.NormalizationSlope > 100)
            errors.Add(
                "IsolationForest.NormalizationSlope deve estar no intervalo (0, 100].");

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
