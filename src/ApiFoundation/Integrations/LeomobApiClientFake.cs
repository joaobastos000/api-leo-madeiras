using Microsoft.Extensions.Options;

namespace ApiFoundation.Integrations;

/// <summary>Simula a LeomobAPI, falhando conforme o modo configurado em Leomob:ModoFalha.</summary>
public class LeomobApiClientFake(IOptions<LeomobOptions> options, ILogger<LeomobApiClientFake> logger) : ILeomobApiClient
{
    private int _chamadas;

    public Task NotificarAsync(int pedidoSVL, long serviceId, IReadOnlyList<string> links, CancellationToken ct)
    {
        // Singleton: Interlocked garante a contagem certa com requisições simultâneas.
        var chamada = Interlocked.Increment(ref _chamadas);

        var falhar = options.Value.ModoFalha switch
        {
            ModoFalhaLeomob.Sempre => true,
            ModoFalhaLeomob.Alternado => chamada % 2 == 1,
            _ => false
        };

        if (falhar)
            throw new HttpRequestException("LeomobAPI indisponível (simulado)");

        logger.LogInformation("LeomobAPI notificada: pedido {PedidoSVL}, serviço {ServiceId}, {QuantidadeLinks} links",
            pedidoSVL, serviceId, links.Count);
        return Task.CompletedTask;
    }
}
