using ApiFoundation.Integrations;

namespace ApiFoundation.Tests.Fakes;

/// <summary>Conta as chamadas à LeomobAPI e falha quando o teste mandar.</summary>
public class LeomobEspiao : ILeomobApiClient
{
    public bool DeveFalhar { get; set; }
    public int Chamadas { get; private set; }

    public Task NotificarAsync(int pedidoSVL, long serviceId, IReadOnlyList<string> links, CancellationToken ct)
    {
        Chamadas++;

        if (DeveFalhar)
            throw new HttpRequestException("Falha simulada no teste");

        return Task.CompletedTask;
    }
}
