using ApiFoundation.Contracts;
using ApiFoundation.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Services;

/// <summary>Monta a visão do pedido para o GET, mantendo o controller só com HTTP.</summary>
public class ConsultaPedidos(AppDbContext db)
{
    public async Task<PedidoResponse?> BuscarAsync(int pedidoSVL, CancellationToken ct = default)
    {
        var pedido = await db.Pedidos
            .AsNoTracking()
            .Include(p => p.Links)
            .Include(p => p.Notificacoes)
            .SingleOrDefaultAsync(p => p.PedidoSVL == pedidoSVL, ct);

        if (pedido is null)
            return null;

        var links = pedido.Links
            .OrderBy(l => l.Id)
            .GroupBy(l => l.Categoria)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(l => l.Url).ToList());

        var ultimaNotificacao = pedido.Notificacoes.MaxBy(n => n.Id);

        return new PedidoResponse(
            pedido.PedidoSVL,
            pedido.ServiceId,
            pedido.TipoPedido.ToString(),
            pedido.Status,
            links,
            ultimaNotificacao is null
                ? null
                : new NotificacaoResponse(
                    ultimaNotificacao.Status.ToString(),
                    ultimaNotificacao.Tentativas,
                    ultimaNotificacao.DataAtualizacao));
    }
}
