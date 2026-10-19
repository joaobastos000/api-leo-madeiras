namespace ApiFoundation.Integrations;

/// <summary>Cliente da LeomobAPI, que atende os pedidos ZMOB.</summary>
public interface ILeomobApiClient
{
    /// <summary>Avisa a LeomobAPI dos links de produção do pedido. Lança exceção se a chamada falhar.</summary>
    Task NotificarAsync(int pedidoSVL, long serviceId, IReadOnlyList<string> links, CancellationToken ct);
}
