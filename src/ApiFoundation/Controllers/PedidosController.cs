using ApiFoundation.Contracts;
using ApiFoundation.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiFoundation.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController(ConsultaPedidos consulta) : ControllerBase
{
    /// <summary>Retorna o pedido, seus links e o status da notificação (se houver).</summary>
    [HttpGet("{pedidoSVL:int}")]
    [ProducesResponseType<PedidoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Buscar(int pedidoSVL, CancellationToken ct)
    {
        var pedido = await consulta.BuscarAsync(pedidoSVL, ct);
        return pedido is null ? NotFound() : Ok(pedido);
    }
}
