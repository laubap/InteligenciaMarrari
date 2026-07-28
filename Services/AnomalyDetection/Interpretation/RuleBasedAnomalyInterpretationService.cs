namespace IAApi.Services.AnomalyDetection.Interpretation;

public class RuleBasedAnomalyInterpretationService
    : IAnomalyInterpretationService
{
    public Task<AnomalyInterpretationResult> InterpretarAsync(
        AnomalyInterpretationContext contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        cancellationToken.ThrowIfCancellationRequested();

        var detectoresComAlerta =
            contexto.Detectores
                .Where(detector =>
                    detector.Aplicavel &&
                    detector.VotouAnomalia)
                .ToList();

        var evidencias =
            CriarEvidencias(
                contexto,
                detectoresComAlerta);

        var identificacaoTag =
            !string.IsNullOrWhiteSpace(contexto.DescricaoTag)
                ? contexto.DescricaoTag
                : contexto.TagName;

        var resumo =
            contexto.EhAnomalia
                ? $"Foi detectada uma anomalia na tag " +
                  $"'{identificacaoTag}', com score " +
                  $"{contexto.ScoreNormalizado:F2}/100 e severidade " +
                  $"'{contexto.Severidade}'."
                : $"Não foi detectada uma anomalia na tag " +
                  $"'{identificacaoTag}'. O score calculado foi " +
                  $"{contexto.ScoreNormalizado:F2}/100.";

        var interpretacaoTecnica =
            contexto.EhAnomalia
                ? CriarInterpretacaoAnomala(
                    contexto,
                    detectoresComAlerta)
                : CriarInterpretacaoNormal(contexto);

        var possiveisCausas =
            CriarPossiveisCausas(contexto);

        var recomendacoes =
            CriarRecomendacoes(contexto);

        return Task.FromResult(
            new AnomalyInterpretationResult
            {
                Resumo = resumo,

                InterpretacaoTecnica = interpretacaoTecnica,

                Evidencias = evidencias,

                PossiveisCausas = possiveisCausas,

                Recomendacoes = recomendacoes,

                Severidade = contexto.Severidade,

                GeradoPorLlm = false,

                ModeloUtilizado =
                    "Interpretação baseada em regras",

                DataGeracao = DateTime.UtcNow
            });
    }

    private static List<string> CriarEvidencias(
        AnomalyInterpretationContext contexto,
        IReadOnlyCollection<DetectorDebugItem> detectoresComAlerta)
    {
        var evidencias =
            detectoresComAlerta
                .Select(detector =>
                    $"{detector.Nome}: {detector.Explicacao}")
                .Where(evidencia =>
                    !string.IsNullOrWhiteSpace(evidencia))
                .ToList();

        if (!string.IsNullOrWhiteSpace(
                contexto.ExplicacaoEnsemble))
        {
            evidencias.Insert(
                0,
                $"Ensemble: {contexto.ExplicacaoEnsemble}");
        }

        if (contexto.ContextoDisponivel &&
            !string.IsNullOrWhiteSpace(contexto.Criticidade))
        {
            evidencias.Add(
                $"A criticidade cadastrada para a tag é " +
                $"'{contexto.Criticidade}'.");
        }

        if (contexto.Impactos.Count > 0)
        {
            evidencias.Add(
                $"Impactos cadastrados: " +
                $"{string.Join(", ", contexto.Impactos)}.");
        }

        return evidencias;
    }

    private static string CriarInterpretacaoAnomala(
        AnomalyInterpretationContext contexto,
        IReadOnlyCollection<DetectorDebugItem> detectores)
    {
        var nomesDetectores =
            detectores.Count > 0
                ? string.Join(
                    ", ",
                    detectores.Select(detector =>
                        detector.Nome))
                : "o ensemble";

        var partes =
            new List<string>
            {
                "O comportamento atual foi classificado como anômalo.",
                $"O valor atual é {contexto.ValorAtual:F4} e a média " +
                $"histórica é {contexto.MediaHistorica:F4}.",
                $"Os detectores que apresentaram evidências de " +
                $"anomalia foram: {nomesDetectores}."
            };

        if (!string.IsNullOrWhiteSpace(
                contexto.TipoRepresentacao))
        {
            partes.Add(
                $"A tag representa uma variável do tipo " +
                $"'{contexto.TipoRepresentacao}'.");
        }

        if (!string.IsNullOrWhiteSpace(
                contexto.Equipamento))
        {
            partes.Add(
                $"O equipamento associado é " +
                $"'{contexto.Equipamento}'.");
        }

        if (!string.IsNullOrWhiteSpace(contexto.Area))
        {
            partes.Add(
                $"A área associada é '{contexto.Area}'.");
        }

        if (!string.IsNullOrWhiteSpace(
                contexto.ObservacaoModoOperacao))
        {
            partes.Add(
                $"Observação operacional cadastrada: " +
                $"{contexto.ObservacaoModoOperacao}");
        }

        if (contexto.Impactos.Count > 0)
        {
            partes.Add(
                $"Caso a condição persista, os impactos cadastrados " +
                $"incluem: {string.Join(", ", contexto.Impactos)}.");
        }

        return string.Join(" ", partes);
    }

    private static string CriarInterpretacaoNormal(
        AnomalyInterpretationContext contexto)
    {
        var partes =
            new List<string>
            {
                "O comportamento atual permanece dentro do padrão " +
                "considerado normal pelo ensemble.",
                $"O valor atual é {contexto.ValorAtual:F4} e a média " +
                $"histórica é {contexto.MediaHistorica:F4}."
            };

        if (!string.IsNullOrWhiteSpace(
                contexto.ObservacaoModoOperacao))
        {
            partes.Add(
                $"Observação operacional cadastrada: " +
                $"{contexto.ObservacaoModoOperacao}");
        }

        return string.Join(" ", partes);
    }

    private static List<string> CriarPossiveisCausas(
        AnomalyInterpretationContext contexto)
    {
        if (!contexto.EhAnomalia)
        {
            return new List<string>();
        }

        if (contexto.CausasProvaveis.Count > 0)
        {
            return contexto.CausasProvaveis
                .Where(causa =>
                    !string.IsNullOrWhiteSpace(causa))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>
        {
            "Alteração recente no processo.",
            "Mudança no modo de operação.",
            "Falha, desgaste ou condição anormal no equipamento.",
            "Problema de leitura ou calibração do sensor."
        };
    }

    private static List<string> CriarRecomendacoes(
        AnomalyInterpretationContext contexto)
    {
        if (!contexto.EhAnomalia)
        {
            return new List<string>
            {
                "Continuar monitorando o comportamento da tag."
            };
        }

        var recomendacoes =
            contexto.AcoesRecomendadas
                .Where(acao =>
                    !string.IsNullOrWhiteSpace(acao))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (recomendacoes.Count == 0)
        {
            recomendacoes.AddRange(
                new[]
                {
                    "Verificar se houve alteração recente no processo.",
                    "Comparar o comportamento com sensores relacionados.",
                    "Avaliar se é necessária uma inspeção do equipamento."
                });
        }

        if (!string.IsNullOrWhiteSpace(
                contexto.ObservacaoModoOperacao))
        {
            recomendacoes.Add(
                $"Considerar a observação do modo de operação: " +
                $"{contexto.ObservacaoModoOperacao}");
        }

        if (!contexto.ContextoDisponivel)
        {
            recomendacoes.Add(
                "Cadastrar o contexto operacional da tag para obter " +
                "interpretações mais específicas.");
        }
        else if (!contexto.ContextoCompleto)
        {
            recomendacoes.Add(
                "Completar o cadastro do contexto da tag para melhorar " +
                "a qualidade das interpretações.");
        }

        return recomendacoes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}