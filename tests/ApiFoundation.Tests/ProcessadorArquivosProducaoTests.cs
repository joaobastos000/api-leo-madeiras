using ApiFoundation.Contracts;
using ApiFoundation.Data;
using ApiFoundation.Domain;
using ApiFoundation.Services;
using ApiFoundation.Tests.Fakes;
using ApiFoundation.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static ApiFoundation.Tests.Infra.Payloads;

namespace ApiFoundation.Tests;

[Collection("postgres")]
public class ProcessadorArquivosProducaoTests(PostgresFixture pg)
{
    // O xUnit cria uma instância da classe por teste, então cada teste tem espiões novos.
    private readonly ArmazenamentoEspiao _armazenamento = new();
    private readonly LeomobEspiao _leomob = new();

    private ProcessadorArquivosProducao CriarProcessador(AppDbContext db) =>
        new(db, _armazenamento, _leomob, NullLogger<ProcessadorArquivosProducao>.Instance);

    private static Task<int> ContarLinksAsync(AppDbContext db, int pedidoSVL) =>
        db.Links.CountAsync(l => db.Pedidos.Any(p => p.Id == l.PedidoId && p.PedidoSVL == pedidoSVL));

    [Fact]
    public async Task Zmob_nao_aciona_armazenamento()
    {
        await using var db = await pg.CriarBancoAsync();

        await CriarProcessador(db).ProcessarAsync([Servico(1012, 21763699)]);

        Assert.Empty(_armazenamento.Urls);
        Assert.Equal(4, await ContarLinksAsync(db, 1012));
    }

    [Fact]
    public async Task Zlpl_aciona_armazenamento_uma_vez_por_arquivo()
    {
        await using var db = await pg.CriarBancoAsync();

        await CriarProcessador(db).ProcessarAsync([Servico(1007, 21763653)]);

        Assert.Equal(4, _armazenamento.Urls.Count);
        Assert.Equal(4, _armazenamento.Urls.Distinct().Count());
    }

    [Fact]
    public async Task Envio_repetido_nao_duplica_links_copias_nem_notificacoes()
    {
        await using var db = await pg.CriarBancoAsync();
        var processador = CriarProcessador(db);
        ServicoArquivosRequest[] envio = [Servico(1007, 21763653), Servico(1012, 21763699)];

        await processador.ProcessarAsync(envio);
        var segundo = await processador.ProcessarAsync(envio);

        Assert.Equal(8, await db.Links.CountAsync());
        Assert.Equal(4, _armazenamento.Urls.Count);
        Assert.Equal(1, await db.Notificacoes.CountAsync());
        Assert.Equal(1, _leomob.Chamadas);
        Assert.All(segundo.Resultados, r =>
        {
            Assert.True(r.Sucesso);
            Assert.Equal(0, r.LinksNovos);
        });
    }

    [Fact]
    public async Task Pedido_inexistente_nao_impede_os_demais()
    {
        await using var db = await pg.CriarBancoAsync();

        var resultado = await CriarProcessador(db).ProcessarAsync([Servico(9999, 1), Servico(1007, 21763653)]);

        Assert.Equal(1, resultado.Processados);
        Assert.Equal(1, resultado.Falhas);
        Assert.False(resultado.Resultados[0].Sucesso);
        Assert.Equal("Pedido 9999 não encontrado.", resultado.Resultados[0].Erro);
        Assert.True(resultado.Resultados[1].Sucesso);
        Assert.Equal(4, await ContarLinksAsync(db, 1007));
    }

    [Fact]
    public async Task ServiceId_divergente_e_erro_daquele_servico()
    {
        await using var db = await pg.CriarBancoAsync();

        var resultado = await CriarProcessador(db).ProcessarAsync([Servico(1007, 1)]);

        var servico = Assert.Single(resultado.Resultados);
        Assert.False(servico.Sucesso);
        Assert.Contains("esperado 21763653", servico.Erro);
        Assert.Equal(0, await ContarLinksAsync(db, 1007));
        Assert.Empty(_armazenamento.Urls);
    }

    [Fact]
    public async Task Zmob_com_leomob_ok_conclui_notificacao()
    {
        await using var db = await pg.CriarBancoAsync();

        await CriarProcessador(db).ProcessarAsync([Servico(1012, 21763699)]);

        var notificacao = await db.Notificacoes.SingleAsync();
        Assert.Equal(StatusNotificacao.CONCLUIDO, notificacao.Status);
        Assert.Equal(0, notificacao.Tentativas);
    }

    [Fact]
    public async Task Zmob_com_leomob_falhando_fica_pendente_com_uma_tentativa()
    {
        await using var db = await pg.CriarBancoAsync();
        _leomob.DeveFalhar = true;

        var resultado = await CriarProcessador(db).ProcessarAsync([Servico(1012, 21763699)]);

        var notificacao = await db.Notificacoes.SingleAsync();
        Assert.Equal(StatusNotificacao.PENDENTE, notificacao.Status);
        Assert.Equal(1, notificacao.Tentativas);
        Assert.NotNull(notificacao.UltimoErro);
        // A falha da LeomobAPI não é falha do envio: os links foram recebidos e a notificação será reprocessada.
        Assert.True(resultado.Resultados[0].Sucesso);
    }

    [Fact]
    public async Task Categorias_ausentes_sao_ignoradas()
    {
        await using var db = await pg.CriarBancoAsync();
        var links = new LinksArquivosRequest { Etiquetas = ["https://arquivos.exemplo.com/1007/etiqueta-1.png"] };

        await CriarProcessador(db).ProcessarAsync([Servico(1007, 21763653, links)]);

        var link = await db.Links.SingleAsync();
        Assert.Equal("etiquetas", link.Categoria);
        Assert.Single(_armazenamento.Urls);
    }

    [Fact]
    public async Task Url_repetida_no_mesmo_envio_e_gravada_uma_vez()
    {
        await using var db = await pg.CriarBancoAsync();
        const string url = "https://arquivos.exemplo.com/1007/arquivo.pdf";
        var links = new LinksArquivosRequest { Etiquetas = [url], Usinagem = [url] };

        await CriarProcessador(db).ProcessarAsync([Servico(1007, 21763653, links)]);

        Assert.Equal(1, await db.Links.CountAsync());
        Assert.Single(_armazenamento.Urls);
    }

    [Fact]
    public async Task Zmob_com_link_novo_gera_nova_notificacao()
    {
        await using var db = await pg.CriarBancoAsync();
        var processador = CriarProcessador(db);
        var linkNovo = new LinksArquivosRequest { Etiquetas = ["https://arquivos.exemplo.com/1012/etiqueta-2.png"] };

        await processador.ProcessarAsync([Servico(1012, 21763699)]);
        await processador.ProcessarAsync([Servico(1012, 21763699, linkNovo)]);

        Assert.Equal(5, await ContarLinksAsync(db, 1012));
        Assert.Equal(2, await db.Notificacoes.CountAsync());
        Assert.Equal(2, _leomob.Chamadas);
    }
}
