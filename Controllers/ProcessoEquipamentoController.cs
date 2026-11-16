using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Controller para vincular equipamentos a processos.
// Ele valida a existência do processo e do equipamento e cria um vínculo
// na tabela de relacionamento ProcessoEquipamentosIa.
[ApiController]
[Route("api/processo-equipamentos")]
public class ProcessoEquipamentoController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProcessoEquipamentoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    // Endpoint: POST /api/processo-equipamentos
    // Recebe ProcessoEquipamentoRequest e vincula o equipamento ao processo,
    // retornando uma mensagem de sucesso ou erro.
    public IActionResult Vincular(ProcessoEquipamentoRequest request)
    {
        var processo = _context.ProcessosIa
            .FirstOrDefault(x => x.Id == request.ProcessoId);

        if (processo == null)
        {
            return NotFound(new
            {
                erro = "Processo não encontrado."
            });
        }

        var equipamento = _context.EquipamentosIa
            .FirstOrDefault(x => x.Id == request.EquipamentoId);

        if (equipamento == null)
        {
            return NotFound(new
            {
                erro = "Equipamento não encontrado."
            });
        }

        // P37: processo e equipamento têm de ser do mesmo cliente.
        if (processo.ClienteId != equipamento.ClienteId)
        {
            return BadRequest(new
            {
                erro = "Processo e equipamento são de clientes diferentes."
            });
        }

        var existe = _context.ProcessoEquipamentosIa
            .Any(x =>
                x.ProcessoIaId == request.ProcessoId &&
                x.EquipamentoIaId == request.EquipamentoId);

        if (existe)
        {
            return BadRequest(new
            {
                erro = "Equipamento já está vinculado a este processo."
            });
        }

        var vinculo = new ProcessoEquipamentoIa
        {
            ProcessoIaId = request.ProcessoId,
            EquipamentoIaId = request.EquipamentoId,
            PapelNoProcesso = request.PapelNoProcesso,
            Observacao = request.Observacao
        };

        _context.ProcessoEquipamentosIa.Add(vinculo);
        _context.SaveChanges();

        return Ok(new
        {
            mensagem = "Equipamento vinculado ao processo com sucesso.",
            vinculo.Id
        });
    }
}

public class ProcessoEquipamentoRequest
{
    // Payload contendo IDs de processo e equipamento a serem vinculados.
    public int ProcessoId { get; set; }

    public int EquipamentoId { get; set; }

    public string? PapelNoProcesso { get; set; }

    public string? Observacao { get; set; }
}