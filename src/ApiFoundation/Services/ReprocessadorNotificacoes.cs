using ApiFoundation.Contracts;
using ApiFoundation.Data;
using ApiFoundation.Domain;
using ApiFoundation.Integrations;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Services;

/// <summary>
/// Tenta de novo as notificações PENDENTE para a LeomobAPI.
/// Usado pelo endpoint manual e pelo reprocessamento automático em segundo plano.
/// </summary>
public class ReprocessadorNotificacoes(
    AppDbContext db,
    ILeomobApiClient leomob,
    ILogger<ReprocessadorNotificacoes> logger)
{
    public async Task<ResultadoReprocessamento> ReprocessarPendentesAsync(CancellationToken ct = default)
    {
        var pendentes = await db.Notificacoes
            .Include(n => n.Pedido)
            .ThenInclude(p => p.Links)
            .Where(n => n.Status == StatusNotificacao.PENDENTE)
            .OrderBy(n => n.Id)
            .ToListAsync(ct);

        foreach (var notificacao in pendentes)
        {
            var pedido = notificacao.Pedido;
            using var _ = logger.BeginScope("Pedido {PedidoSVL}, serviço {ServiceId}", pedido.PedidoSVL, pedido.ServiceId);

            try
            {
                var links = pedido.Links.Select(l => l.Url).ToList();
                await leomob.NotificarAsync(pedido.PedidoSVL, pedido.ServiceId, links, ct);
                notificacao.RegistrarSucesso();
                logger.LogInformation("Notificação reprocessada com sucesso");
            }
            catch (Exception ex)
            {
                notificacao.RegistrarFalha(ex.Message);
                logger.LogWarning(ex, "Reprocessamento falhou na tentativa {Tentativas}; notificação ficou {Status}",
                    notificacao.Tentativas, notificacao.Status);
            }

            // Grava uma a uma: se o processo cair no meio, as já tentadas não são repetidas.
            await db.SaveChangesAsync(ct);
        }

        return new ResultadoReprocessamento(
            Reprocessadas: pendentes.Count,
            Concluidas: pendentes.Count(n => n.Status == StatusNotificacao.CONCLUIDO),
            Pendentes: pendentes.Count(n => n.Status == StatusNotificacao.PENDENTE),
            Falhas: pendentes.Count(n => n.Status == StatusNotificacao.FALHA));
    }
}
