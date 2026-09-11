namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Valida invariantes comuns antes da execução do ensemble.
///
/// O validador não decide se um detector específico possui histórico
/// suficiente; essa continua sendo uma responsabilidade do detector.
/// Ele protege apenas condições que tornam a entrada tecnicamente inválida.
/// </summary>
public static class DetectionInputValidator
{
    public static DetectionInputValidationResult Validate(
        DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors =
            new List<string>();

        var warnings =
            new List<string>();

        if (string.IsNullOrWhiteSpace(
                input.ClienteId))
        {
            errors.Add(
                "O identificador do cliente é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(
                input.TagName))
        {
            errors.Add(
                "O nome da tag é obrigatório.");
        }

        if (!double.IsFinite(
                input.ValorAtual))
        {
            errors.Add(
                "O valor atual da tag não é numérico/finito.");
        }

        if (input.DataHoraAtual == default ||
            input.DataHoraAtual <= DateTime.MinValue ||
            input.DataHoraAtual >= DateTime.MaxValue)
        {
            errors.Add(
                "O timestamp da leitura atual é inválido.");
        }

        if (!double.IsFinite(
                input.MediaHistorica))
        {
            errors.Add(
                "A média histórica não é numérica/finita.");
        }

        if (!double.IsFinite(
                input.DesvioPadraoHistorico) ||
            input.DesvioPadraoHistorico < 0)
        {
            errors.Add(
                "O desvio-padrão histórico deve ser finito e não negativo.");
        }

        if (input.Historico.Count == 0)
        {
            errors.Add(
                "Nenhum ponto histórico válido permaneceu após a sanitização.");
        }

        if (input.Historico.Count > 0)
        {
            var lastTimestamp =
                input.Historico[^1].DataHora;

            if (input.DataHoraAtual <
                lastTimestamp)
            {
                errors.Add(
                    "A leitura atual é anterior ao último ponto do histórico. " +
                    "Isso pode causar vazamento temporal.");
            }
            else if (input.DataHoraAtual ==
                     lastTimestamp)
            {
                warnings.Add(
                    "A leitura atual possui o mesmo timestamp do último " +
                    "ponto histórico.");
            }
        }

        var quality =
            input.QualidadeHistorico;

        if (quality.InvalidValues > 0)
        {
            warnings.Add(
                $"{quality.InvalidValues} ponto(s) com valor inválido " +
                "foram removidos.");
        }

        if (quality.InvalidTimestamps > 0)
        {
            warnings.Add(
                $"{quality.InvalidTimestamps} ponto(s) com timestamp " +
                "inválido foram removidos.");
        }

        if (quality.DuplicateTimestamps > 0)
        {
            warnings.Add(
                $"{quality.DuplicateTimestamps} timestamp(s) duplicado(s) " +
                "foram consolidados.");
        }

        if (quality.OutOfOrderTransitions > 0)
        {
            warnings.Add(
                "O histórico chegou fora de ordem e foi normalizado.");
        }

        if (quality.HasCriticalGap)
        {
            warnings.Add(
                $"Foi identificado gap crítico de " +
                $"{quality.LargestGapMinutes:F3} min. " +
                $"Cadência mediana: " +
                $"{quality.MedianIntervalMinutes:F3} min.");
        }

        return new DetectionInputValidationResult(
            errors.Count == 0,
            errors.AsReadOnly(),
            warnings.AsReadOnly());
    }
}

public sealed record DetectionInputValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
