using System.Globalization;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/tags/historico")]
public class TagHistoricoController : ControllerBase
{
    private readonly IHistoricoService _historicoService;
    private readonly IConfiguration _configuration;

    public TagHistoricoController(
        IHistoricoService historicoService,
        IConfiguration configuration)
    {
        _historicoService = historicoService;
        _configuration = configuration;
    }

    [HttpGet("{clienteId}/{tagName}")]
    public IActionResult BuscarHistorico(
        string clienteId,
        string tagName,
        [FromQuery] int horas = 6)
    {
        try
        {
            /*
             * 34.3 (P42): primeiro, só o período pedido (ex.: 2 h em vez
             * das 24 h de Bridge:HorasHistorico). Se não houver o que
             * mostrar, segue pelo caminho de antes, abaixo, sem mudança.
             */
            var leituras =
                BuscarSoOPeriodoPedido(
                    clienteId,
                    tagName,
                    horas);

            if (leituras is null)
            {
                const string tipoTag = "perfil-comportamental";

                var historico = _historicoService
                    .BuscarHistorico(
                        clienteId,
                        tagName,
                        tipoTag)
                    .Where(x =>
                        !float.IsNaN(x.Valor) &&
                        !float.IsInfinity(x.Valor))
                    .OrderBy(x => x.DataHora)
                    .ToList();

                if (historico.Count == 0)
                {
                    return NotFound(new
                    {
                        erro = "Nenhum histórico encontrado para esta tag."
                    });
                }

                var inicio =
                    DateTime.Now.AddHours(-horas);

                leituras = historico
                    .Where(x =>
                        x.DataHora >= inicio)
                    .ToList();

                // Caso não existam leituras nas últimas horas,
                // devolve as últimas 20 disponíveis.
                if (leituras.Count == 0)
                {
                    leituras = historico
                        .TakeLast(20)
                        .ToList();
                }
            }

            var resposta = leituras
                .Select(x => new
                {
                    clienteId,
                    tagName,
                    dataHora = x.DataHora,
                    valor = x.Valor
                })
                .ToList();

            return Ok(new
            {
                clienteId,
                tagName,
                horas,
                quantidadeLeituras = resposta.Count,
                historico = resposta
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                erro = ex.Message
            });
        }
    }

    /// <summary>
    /// 34.3 (P42): busca no Bridge só as últimas <paramref name="horas"/>,
    /// com <see cref="IHistoricoService.BuscarHistoricoPorPeriodo"/>.
    ///
    /// Devolve <c>null</c> para o chamador seguir pelo caminho de antes
    /// (24 h de <c>Bridge:HorasHistorico</c>), com o mesmo resultado de antes,
    /// quando:
    /// - <paramref name="horas"/> ≤ 0 ou maior que <c>Bridge:HorasHistorico</c>
    ///   (o caminho de antes nunca mostrava mais do que isso);
    /// - o período vem vazio (o Bridge lança exceção para trecho vazio) ou
    ///   falha por outro motivo que não o transporte;
    /// - nenhuma leitura sobra depois do mesmo corte de antes.
    ///
    /// Falha de transporte (tempo limite ou Bridge inacessível) não cai no
    /// caminho de 24 h: ele usaria o mesmo transporte e só dobraria a espera.
    /// A exceção sobe e vira o mesmo 400 de antes, com a mesma mensagem.
    /// </summary>
    private List<LeituraBruta>? BuscarSoOPeriodoPedido(
        string clienteId,
        string tagName,
        int horas)
    {
        if (horas <= 0 || horas > HorasDoCaminhoAnterior())
        {
            return null;
        }

        /*
         * Hora local sem fuso (Kind Unspecified), a mesma convenção das
         * datas que vêm do Bridge e que o OperationalRegimeBaselineProvider
         * devolve ao BuscarHistoricoPorPeriodo. Com Kind Local, o formato "O"
         * acrescentaria o offset (ex.: -03:00) à URL.
         */
        var fim =
            DateTime.SpecifyKind(
                DateTime.Now,
                DateTimeKind.Unspecified);

        List<LeituraBruta> periodo;

        try
        {
            periodo =
                _historicoService
                    .BuscarHistoricoPorPeriodo(
                        clienteId,
                        tagName,
                        fim.AddHours(-horas),
                        fim);
        }
        catch (Exception ex) when (!FalhaDeTransporte(ex))
        {
            return null;
        }

        /*
         * O mesmo corte de antes, calculado do mesmo jeito: depois da busca.
         * Antes, a janela começava em "agora − horas" no fim da chamada.
         */
        var inicio =
            DateTime.Now.AddHours(-horas);

        var leituras = periodo
            .Where(x =>
                !float.IsNaN(x.Valor) &&
                !float.IsInfinity(x.Valor))
            .OrderBy(x => x.DataHora)
            .Where(x =>
                x.DataHora >= inicio)
            .ToList();

        return leituras.Count == 0
            ? null
            : leituras;
    }

    /// <summary>
    /// Horas que o caminho de antes pedia ao Bridge: a mesma leitura de
    /// <c>Bridge:HorasHistorico</c> feita por
    /// <see cref="BridgeHistoricoService.BuscarHistorico"/> (padrão 24).
    /// </summary>
    private int HorasDoCaminhoAnterior()
    {
        var horasTexto =
            _configuration["Bridge:HorasHistorico"]
            ?? "24";

        if (
            !int.TryParse(
                horasTexto,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var horas
            )
            || horas <= 0
        )
        {
            horas = 24;
        }

        return horas;
    }

    /// <summary>
    /// Tempo limite ou conexão recusada, como o <c>BridgeHistoricoService</c>
    /// os embrulha (exceção interna) ou como outra implementação os lançaria.
    /// </summary>
    private static bool FalhaDeTransporte(Exception ex)
        => ex is OperationCanceledException or HttpRequestException
            || ex.InnerException is OperationCanceledException or HttpRequestException;
}
