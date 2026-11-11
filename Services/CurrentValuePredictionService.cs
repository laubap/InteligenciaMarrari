public sealed class CurrentValuePredictionService
{
    private readonly BridgeValorAtualService _bridgeValorAtualService;
    private readonly PredictionService _predictionService;

    public CurrentValuePredictionService(
        BridgeValorAtualService bridgeValorAtualService,
        PredictionService predictionService)
    {
        _bridgeValorAtualService =
            bridgeValorAtualService;

        _predictionService =
            predictionService;
    }

    public async Task<AnomaliaResponse> DetectarValorAtual(
        string clienteId,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var valores =
            await _bridgeValorAtualService
                .BuscarValoresAtuais(
                    new List<string>
                    {
                        tagName
                    });

        cancellationToken.ThrowIfCancellationRequested();

        var valorAtual =
            valores.FirstOrDefault(x =>
                string.Equals(
                    x.TagName,
                    tagName,
                    StringComparison.OrdinalIgnoreCase));

        return await DetectarComValorAtual(
            clienteId,
            tagName,
            valorAtual,
            cancellationToken);
    }

    // Usa um valor ja adquirido em lote, sem consultar o Bridge novamente.
    // A validacao e a chamada a PredictionService sao compartilhadas
    // com a rota existente de leitura individual.
    public Task<AnomaliaResponse> DetectarComValorAtual(
        string clienteId,
        string tagName,
        ValorAtualTagResponse? valorAtual,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (valorAtual == null ||
            !valorAtual.Encontrado ||
            !string.Equals(
                valorAtual.TagName,
                tagName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                $"Valor atual da tag {tagName} " +
                "não encontrado no Bridge.");
        }

        var request =
            new AnomaliaRequest
            {
                ClienteId =
                    clienteId,

                TagName =
                    tagName,

                Valor =
                    (float)valorAtual.Valor,

                DataHora =
                    valorAtual.DataHora
            };

        return _predictionService.Detectar(
            request,
            cancellationToken);
    }
}
