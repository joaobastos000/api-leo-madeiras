using System.Collections.Concurrent;

namespace ApiFoundation.Integrations;

/// <summary>Não copia nada de verdade: só registra o que seria copiado.</summary>
public class ArmazenamentoArquivosFake(ILogger<ArmazenamentoArquivosFake> logger) : IArmazenamentoArquivos
{
    // Singleton atendendo várias requisições ao mesmo tempo, por isso uma coleção thread-safe.
    private readonly ConcurrentQueue<string> _copias = new();

    public IReadOnlyCollection<string> Copias => _copias;

    public Task CopiarAsync(int pedidoSVL, string categoria, string url, CancellationToken ct)
    {
        logger.LogInformation("Copiaria o arquivo {Categoria} {Url} do pedido {PedidoSVL}", categoria, url, pedidoSVL);
        _copias.Enqueue(url);
        return Task.CompletedTask;
    }
}
