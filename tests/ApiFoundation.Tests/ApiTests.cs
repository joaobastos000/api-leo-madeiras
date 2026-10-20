using System.Net;
using System.Net.Http.Json;
using ApiFoundation.Contracts;
using ApiFoundation.Tests.Infra;
using static ApiFoundation.Tests.Infra.Payloads;

namespace ApiFoundation.Tests;

/// <summary>Testes de ponta a ponta pelo HTTP, com a API e o banco de verdade.</summary>
[Collection("postgres")]
public class ApiTests(PostgresFixture pg)
{
    private static object Envio(bool hasErrors, params object[] services) =>
        new { documentType = "filesToProduction", hasErrors, services };

    private static async Task<PedidoResponse> BuscarPedidoAsync(HttpClient client, int pedidoSVL) =>
        (await client.GetFromJsonAsync<PedidoResponse>($"/api/pedidos/{pedidoSVL}"))!;

    [Fact]
    public async Task HasErrors_true_retorna_422_e_nao_grava()
    {
        using var api = new ApiFactory(pg);
        var client = api.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/arquivos-producao", Envio(true, Servico(1007, 21763653)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Empty((await BuscarPedidoAsync(client, 1007)).Links);
    }

    [Fact]
    public async Task ServiceId_divergente_retorna_200_com_erro_no_servico()
    {
        using var api = new ApiFactory(pg);
        var client = api.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/arquivos-producao", Envio(false, Servico(1007, 1)));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var resultado = await resposta.Content.ReadFromJsonAsync<ResultadoProcessamento>();
        Assert.False(resultado!.Resultados[0].Sucesso);
    }

    [Fact]
    public async Task Services_vazio_retorna_400()
    {
        using var api = new ApiFactory(pg);

        var resposta = await api.CreateClient().PostAsJsonAsync("/api/arquivos-producao", Envio(false));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Servico_sem_serviceId_retorna_400()
    {
        using var api = new ApiFactory(pg);
        var client = api.CreateClient();
        var semServiceId = new { pedidoSVL = 1007, links = LinksDoEnunciado(1007) };

        var resposta = await client.PostAsJsonAsync("/api/arquivos-producao", Envio(false, semServiceId));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Empty((await BuscarPedidoAsync(client, 1007)).Links);
    }

    [Fact]
    public async Task Get_pedido_inexistente_retorna_404()
    {
        using var api = new ApiFactory(pg);

        var resposta = await api.CreateClient().GetAsync("/api/pedidos/9999");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Get_pedido_sem_links_retorna_links_vazio_e_notificacao_nula()
    {
        using var api = new ApiFactory(pg);

        var pedido = await BuscarPedidoAsync(api.CreateClient(), 1008);

        Assert.Equal("ZCVS", pedido.TipoPedido);
        Assert.Empty(pedido.Links);
        Assert.Null(pedido.Notificacao);
    }

    [Fact]
    public async Task Fluxo_zmob_ponta_a_ponta()
    {
        using var api = new ApiFactory(pg);
        var client = api.CreateClient();
        api.Leomob.DeveFalhar = true;

        await client.PostAsJsonAsync("/api/arquivos-producao", Envio(false, Servico(1012, 21763699)));
        var aposRecebimento = await BuscarPedidoAsync(client, 1012);

        Assert.Equal(4, aposRecebimento.Links.Count); // uma entrada por categoria
        Assert.Equal("PENDENTE", aposRecebimento.Notificacao!.Status);
        Assert.Equal(1, aposRecebimento.Notificacao.Tentativas);

        api.Leomob.DeveFalhar = false;
        var reprocessamento = await client.PostAsync("/api/notificacoes/reprocessar", null);
        var aposReprocessar = await BuscarPedidoAsync(client, 1012);

        Assert.Equal(HttpStatusCode.OK, reprocessamento.StatusCode);
        Assert.Equal("CONCLUIDO", aposReprocessar.Notificacao!.Status);
        Assert.Empty(api.Armazenamento.Urls);
    }
}
