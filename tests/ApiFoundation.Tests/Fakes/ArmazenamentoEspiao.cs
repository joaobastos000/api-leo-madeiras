using ApiFoundation.Integrations;

namespace ApiFoundation.Tests.Fakes;

/// <summary>Registra cada cópia pedida, para os testes conferirem quantas e quais foram.</summary>
public class ArmazenamentoEspiao : IArmazenamentoArquivos
{
    public List<string> Urls { get; } = [];

    public Task CopiarAsync(int pedidoSVL, string categoria, string url, CancellationToken ct)
    {
        Urls.Add(url);
        return Task.CompletedTask;
    }
}
