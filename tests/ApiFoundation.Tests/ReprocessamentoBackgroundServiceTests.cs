using System.Net.Http.Json;
using ApiFoundation.Contracts;
using ApiFoundation.Tests.Infra;
using static ApiFoundation.Tests.Infra.Payloads;

namespace ApiFoundation.Tests;

[Collection("postgres")]
public class ReprocessamentoBackgroundServiceTests(PostgresFixture pg)
{
    [Fact]
    public async Task Worker_reprocessa_pendentes_sozinho()
    {
        using var api = new ApiFactory(pg, reprocessamentoAutomatico: true);
        var client = api.CreateClient();

        api.Leomob.DeveFalhar = true;
        await client.PostAsJsonAsync("/api/arquivos-producao",
            new { documentType = "filesToProduction", hasErrors = false, services = new[] { Servico(1012, 21763699) } });
        api.Leomob.DeveFalhar = false;

        // Ninguém chama o endpoint de reprocessar: quem conclui é o worker (intervalo de 1 s nos testes).
        var limite = DateTime.UtcNow.AddSeconds(10);
        string? status = null;
        while (status != "CONCLUIDO" && DateTime.UtcNow < limite)
        {
            await Task.Delay(250);
            var pedido = await client.GetFromJsonAsync<PedidoResponse>("/api/pedidos/1012");
            status = pedido!.Notificacao?.Status;
        }

        Assert.Equal("CONCLUIDO", status);
    }
}
