using ApiFoundation.Contracts;
using ApiFoundation.Data;
using ApiFoundation.Domain;
using ApiFoundation.Integrations;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Services;

/// <summary>
/// Processa os links de arquivos de produção recebidos da Digibee.
/// Idempotência: só links ainda não gravados geram efeitos (gravação, cópia, notificação).
/// </summary>
public class ProcessadorArquivosProducao(
    AppDbContext db,
    IArmazenamentoArquivos armazenamento,
    ILeomobApiClient leomob,
    ILogger<ProcessadorArquivosProducao> logger)
{
    public async Task<ResultadoProcessamento> ProcessarAsync(
        IReadOnlyList<ServicoArquivosRequest> servicos, CancellationToken ct = default)
    {
        var resultados = new List<ResultadoServico>();

        // Um serviço por vez, cada um com seu SaveChanges: o erro de um não desfaz os outros.
        foreach (var servico in servicos)
            resultados.Add(await ProcessarServicoAsync(servico, ct));

        return new ResultadoProcessamento(
            Processados: resultados.Count(r => r.Sucesso),
            Falhas: resultados.Count(r => !r.Sucesso),
            Resultados: resultados);
    }

    private async Task<ResultadoServico> ProcessarServicoAsync(ServicoArquivosRequest servico, CancellationToken ct)
    {
        // Os valores já foram validados como obrigatórios pelo [ApiController].
        var pedidoSVL = servico.PedidoSVL!.Value;
        var serviceId = servico.ServiceId!.Value;

        // Todo log dentro deste serviço sai com PedidoSVL e ServiceId, para rastrear o pedido.
        using var _ = logger.BeginScope("Pedido {PedidoSVL}, serviço {ServiceId}", pedidoSVL, serviceId);

        var pedido = await db.Pedidos
            .Include(p => p.Links)
            .SingleOrDefaultAsync(p => p.PedidoSVL == pedidoSVL, ct);

        if (pedido is null)
            return Falha(pedidoSVL, serviceId, $"Pedido {pedidoSVL} não encontrado.");

        if (pedido.ServiceId != serviceId)
            return Falha(pedidoSVL, serviceId,
                $"ServiceId {serviceId} não corresponde ao pedido {pedidoSVL} (esperado {pedido.ServiceId}).");

        var urlsGravadas = pedido.Links.Select(l => l.Url).ToHashSet();
        var linksNovos = servico.Links!.Achatar()
            .Where(link => !urlsGravadas.Contains(link.Url))
            .ToList();

        if (linksNovos.Count == 0)
        {
            logger.LogInformation("Nenhum link novo: envio repetido, nada a fazer");
            return new ResultadoServico(pedidoSVL, serviceId, Sucesso: true, LinksNovos: 0, Erro: null);
        }

        if (pedido.TipoPedido == TipoPedido.ZMOB)
            await GravarENotificarLeomobAsync(pedido, linksNovos, ct);
        else
            await CopiarEGravarAsync(pedido, linksNovos, ct);

        return new ResultadoServico(pedidoSVL, serviceId, Sucesso: true, LinksNovos: linksNovos.Count, Erro: null);
    }

    /// <summary>ZLPL e ZCVS: copia cada arquivo novo para o armazenamento interno e grava os links.</summary>
    private async Task CopiarEGravarAsync(Pedido pedido, List<LinkRecebido> linksNovos, CancellationToken ct)
    {
        // Copia antes de gravar: se a gravação falhar, o reenvio copia de novo (copiar o mesmo arquivo é inofensivo).
        foreach (var link in linksNovos)
            await armazenamento.CopiarAsync(pedido.PedidoSVL, link.Categoria, link.Url, ct);

        AdicionarLinks(pedido, linksNovos);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("{Quantidade} arquivos copiados e links gravados", linksNovos.Count);
    }

    /// <summary>ZMOB: não copia nada; grava os links e avisa a LeomobAPI com controle de status.</summary>
    private async Task GravarENotificarLeomobAsync(Pedido pedido, List<LinkRecebido> linksNovos, CancellationToken ct)
    {
        AdicionarLinks(pedido, linksNovos);

        var agora = DateTime.UtcNow;
        var notificacao = new NotificacaoLeomob
        {
            Status = StatusNotificacao.INICIAL,
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        pedido.Notificacoes.Add(notificacao);

        // Grava links + INICIAL antes de chamar a LeomobAPI: a intenção de notificar fica registrada mesmo se a chamada cair.
        await db.SaveChangesAsync(ct);

        try
        {
            var todosOsLinks = pedido.Links.Select(l => l.Url).ToList();
            await leomob.NotificarAsync(pedido.PedidoSVL, pedido.ServiceId, todosOsLinks, ct);
            notificacao.RegistrarSucesso();
            logger.LogInformation("LeomobAPI notificada");
        }
        catch (Exception ex)
        {
            // A falha não derruba o envio: a notificação fica PENDENTE para o reprocessamento.
            notificacao.RegistrarFalha(ex.Message);
            logger.LogWarning(ex, "Falha ao notificar a LeomobAPI; notificação ficou {Status}", notificacao.Status);
        }

        await db.SaveChangesAsync(ct);
    }

    private static void AdicionarLinks(Pedido pedido, List<LinkRecebido> linksNovos)
    {
        var agora = DateTime.UtcNow;
        pedido.Links.AddRange(linksNovos.Select(link => new LinkArquivo
        {
            Categoria = link.Categoria,
            Url = link.Url,
            DataCriacao = agora
        }));
    }

    private ResultadoServico Falha(int pedidoSVL, long serviceId, string erro)
    {
        logger.LogWarning("Serviço rejeitado: {Erro}", erro);
        return new ResultadoServico(pedidoSVL, serviceId, Sucesso: false, LinksNovos: 0, Erro: erro);
    }
}
