namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Recorta o histórico de uma entrada para o último trecho contíguo,
/// depois do último gap crítico (maior que 5 × a cadência mediana).
///
/// Extraído sem mudança de
/// <c>RuntimeFunctionalDecisionContextProvider.CreateContiguousRuntimeInput</c>
/// (que agora delega para cá) para ser reaproveitado pelo
/// <see cref="ValorCongeladoDetector"/> (BACKLOG 33, passo 2): o histórico do
/// baseline por regime é concatenado e quase sempre tem gap crítico, e o
/// <c>FrozenSensorEvidenceCalculator</c> recusa entrada com gap crítico.
///
/// Sem gap crítico, sem cadência válida ou com menos de 10 pontos no último
/// trecho, devolve a própria entrada.
/// </summary>
public static class DetectorInputContiguousTail
{
    public static DetectorInput Create(
        DetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var history =
            input.Historico;

        if (history.Count < 2 ||
            !input.QualidadeHistorico.HasCriticalGap)
        {
            return input;
        }

        var cadence =
            input.QualidadeHistorico.MedianIntervalMinutes;

        if (!double.IsFinite(cadence) ||
            cadence <= 0)
        {
            return input;
        }

        var criticalGapMinutes =
            cadence * 5.0;

        var startIndex = 0;

        for (var index = 1;
             index < history.Count;
             index++)
        {
            var gapMinutes =
                (history[index].DataHora -
                 history[index - 1].DataHora)
                .TotalMinutes;

            if (double.IsFinite(gapMinutes) &&
                gapMinutes > criticalGapMinutes)
            {
                startIndex = index;
            }
        }

        if (startIndex <= 0)
        {
            return input;
        }

        var contiguousHistory =
            history
                .Skip(startIndex)
                .Select(point =>
                    new HistoricoDetectorPoint
                    {
                        DataHora = point.DataHora,
                        Valor = point.Valor
                    })
                .ToArray();

        if (contiguousHistory.Length < 10)
        {
            return input;
        }

        return new DetectorInput
        {
            ClienteId = input.ClienteId,
            TagName = input.TagName,
            TipoTag = input.TipoTag,
            ValorAtual = input.ValorAtual,
            DataHoraAtual = input.DataHoraAtual,
            MediaHistorica = input.MediaHistorica,
            DesvioPadraoHistorico = input.DesvioPadraoHistorico,

            /*
             * B3.24.1
             *
             * A reconstrução do histórico após gap crítico
             * não pode apagar os metadados de proteção da baseline.
             */
            VariacaoMinimaRelevante =
                input.VariacaoMinimaRelevante,

            ConfiancaVariacaoMinima =
                input.ConfiancaVariacaoMinima,

            VariacaoMinimaDefinidaPeloCliente =
                input.VariacaoMinimaDefinidaPeloCliente,

            Historico = contiguousHistory
        };
    }
}
