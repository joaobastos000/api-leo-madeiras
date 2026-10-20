using ApiFoundation.Contracts;
using ApiFoundation.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Controllers;

[ApiController]
[Route("api/notificacoes")]
public class NotificacoesController(ReprocessadorNotificacoes reprocessador) : ControllerBase
{
    /// <summary>Simula o worker: tenta de novo todas as notificações PENDENTE para a LeomobAPI.</summary>
    [HttpPost("reprocessar")]
    [ProducesResponseType<ResultadoReprocessamento>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reprocessar(CancellationToken ct) =>
        Ok(await reprocessador.ReprocessarPendentesAsync(ct));
}
