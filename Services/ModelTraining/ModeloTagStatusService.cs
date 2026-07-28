using IAApi.Services.AnomalyDetection;
using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.ModelTraining;

public class ModeloTagStatusService
{
    private readonly AppDbContext _context;

    public ModeloTagStatusService(
        AppDbContext context)
    {
        _context = context
            ?? throw new ArgumentNullException(
                nameof(context)
            );
    }

    public async Task MarcarTodosComoDesatualizados(
        string clienteId,
        string tagName)
    {
        ValidarParametros(
            clienteId,
            tagName
        );

        var tiposModelo = new[]
        {
            "IsolationForest",
            "Forecast",
            "Autoencoder"
        };

        var modelosExistentes =
            await _context.ModelosTagsIaStatus
                .Where(x =>
                    x.ClienteId == clienteId &&
                    x.TagName == tagName)
                .ToListAsync();

        foreach (var tipoModelo in tiposModelo)
        {
            var modelo = modelosExistentes
                .FirstOrDefault(x =>
                    x.TipoModelo == tipoModelo
                );

            if (modelo == null)
            {
                modelo = new ModeloTagIaStatus
                {
                    ClienteId = clienteId,
                    TagName = tagName,
                    TipoModelo = tipoModelo,
                    Status = StatusModeloIa.Desatualizado,
                    Versao = 0,
                    DataMarcadoDesatualizado =
                        DateTime.Now
                };

                _context.ModelosTagsIaStatus.Add(
                    modelo
                );
            }
            else
            {
                modelo.Status =
                    StatusModeloIa.Desatualizado;

                modelo.DataMarcadoDesatualizado =
                    DateTime.Now;

                modelo.UltimoErro =
                    null;
            }
        }

        await _context.SaveChangesAsync();

        IsolationForestDetector.RemoverDoCache(
            clienteId,
            tagName
        );
    }

    public async Task MarcarTreinandoAsync(
        string clienteId,
        string tagName,
        string tipoModelo)
    {
        var modelo =
            await ObterOuCriarAsync(
                clienteId,
                tagName,
                tipoModelo
            );

        modelo.Status =
            StatusModeloIa.Treinando;

        modelo.DataInicioTreinamento =
            DateTime.Now;

        modelo.DataFimTreinamento =
            null;

        modelo.UltimoErro =
            null;

        await _context.SaveChangesAsync();
    }

    public async Task MarcarAtualAsync(
        string clienteId,
        string tagName,
        string tipoModelo,
        int quantidadePontos,
        string hashJanelas)
    {
        var modelo =
            await ObterOuCriarAsync(
                clienteId,
                tagName,
                tipoModelo
            );

        modelo.Status =
            StatusModeloIa.Atual;

        modelo.Versao =
            modelo.Versao <= 0
                ? 1
                : modelo.Versao + 1;

        modelo.DataUltimoTreinamento =
            DateTime.Now;

        modelo.DataFimTreinamento =
            DateTime.Now;

        modelo.QuantidadePontosTreinamento =
            quantidadePontos;

        modelo.HashJanelasTreinamento =
            hashJanelas;

        modelo.UltimoErro =
            null;

        await _context.SaveChangesAsync();
    }

    public async Task MarcarErroAsync(
        string clienteId,
        string tagName,
        string tipoModelo,
        string erro)
    {
        var modelo =
            await ObterOuCriarAsync(
                clienteId,
                tagName,
                tipoModelo
            );

        modelo.Status =
            StatusModeloIa.Erro;

        modelo.DataFimTreinamento =
            DateTime.Now;

        modelo.UltimoErro =
            erro;

        await _context.SaveChangesAsync();
    }

    public async Task<ModeloTagIaStatus?> BuscarAsync(
        string clienteId,
        string tagName,
        string tipoModelo)
    {
        return await _context.ModelosTagsIaStatus
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ClienteId == clienteId &&
                x.TagName == tagName &&
                x.TipoModelo == tipoModelo
            );
    }

    private async Task<ModeloTagIaStatus> ObterOuCriarAsync(
        string clienteId,
        string tagName,
        string tipoModelo)
    {
        ValidarParametros(
            clienteId,
            tagName
        );

        if (string.IsNullOrWhiteSpace(tipoModelo))
        {
            throw new ArgumentException(
                "O tipo do modelo é obrigatório.",
                nameof(tipoModelo)
            );
        }

        var modelo =
            await _context.ModelosTagsIaStatus
                .FirstOrDefaultAsync(x =>
                    x.ClienteId == clienteId &&
                    x.TagName == tagName &&
                    x.TipoModelo == tipoModelo
                );

        if (modelo != null)
        {
            return modelo;
        }

        modelo = new ModeloTagIaStatus
        {
            ClienteId = clienteId,
            TagName = tagName,
            TipoModelo = tipoModelo,
            Status = StatusModeloIa.NaoTreinado,
            Versao = 0
        };

        _context.ModelosTagsIaStatus.Add(
            modelo
        );

        await _context.SaveChangesAsync();

        return modelo;
    }

    private static void ValidarParametros(
        string clienteId,
        string tagName)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
        {
            throw new ArgumentException(
                "O identificador do cliente é obrigatório.",
                nameof(clienteId)
            );
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException(
                "O nome da tag é obrigatório.",
                nameof(tagName)
            );
        }
    }
}