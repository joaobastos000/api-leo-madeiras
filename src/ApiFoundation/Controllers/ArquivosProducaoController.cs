using ApiFoundation.Contracts;
using ApiFoundation.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Controllers;

[ApiController]
[Route("api/arquivos-producao")]
public class ArquivosProducaoController(ProcessadorArquivosProducao processador) : ControllerBase
{
    /// <summary>Recebe da Digibee os links dos arquivos de produção gerados pelo CorteCloud.</summary>
    [HttpPost]
    [ProducesResponseType<ResultadoProcessamento>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Receber(ArquivosProducaoRequest request, CancellationToken ct)
    {
        // Regra 1: o corpo é válido, mas o próprio CorteCloud avisou que houve erro, então nada é processado.
        if (request.HasErrors)
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Envio rejeitado",
                detail: "O CorteCloud sinalizou erros na geração dos arquivos (hasErrors = true). Nenhum serviço foi processado.");

        return Ok(await processador.ProcessarAsync(request.Services!, ct));
    }
}
