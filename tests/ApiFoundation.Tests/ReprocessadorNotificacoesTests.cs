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
public class ReprocessadorNotificacoesTests(PostgresFixture pg)
{
    private readonly LeomobEspiao _leomob = new();

    private ReprocessadorNotificacoes CriarReprocessador(AppDbContext db) =>
        new(db, _leomob, NullLogger<ReprocessadorNotificacoes>.Instance);

    /// <summary>Recebe os arquivos do pedido ZMOB 1012, o que cria a notificação.</summary>
    private Task ReceberZmobAsync(AppDbContext db) =>
        new ProcessadorArquivosProducao(db, new ArmazenamentoEspiao(), _leomob, NullLogger<ProcessadorArquivosProducao>.Instance)
            .ProcessarAsync([Servico(1012, 21763699)]);

    // AsNoTracking: lê do banco, não da memória do EF, para provar que o status foi gravado.
    private static Task<NotificacaoLeomob> LerNotificacaoAsync(AppDbContext db) =>
        db.Notificacoes.AsNoTracking().SingleAsync();

    [Fact]
    public async Task Notificacao_vai_para_falha_apos_terceira_tentativa()
    {
        await using var db = await pg.CriarBancoAsync();
        var reprocessador = CriarReprocessador(db);
        _leomob.DeveFalhar = true;

        await ReceberZmobAsync(db);
        var notificacao = await LerNotificacaoAsync(db);
        Assert.Equal(StatusNotificacao.PENDENTE, notificacao.Status);
        Assert.Equal(1, notificacao.Tentativas);

        await reprocessador.ReprocessarPendentesAsync();
        notificacao = await LerNotificacaoAsync(db);
        Assert.Equal(StatusNotificacao.PENDENTE, notificacao.Status);
        Assert.Equal(2, notificacao.Tentativas);

        await reprocessador.ReprocessarPendentesAsync();
        notificacao = await LerNotificacaoAsync(db);
        Assert.Equal(StatusNotificacao.FALHA, notificacao.Status);
        Assert.Equal(3, notificacao.Tentativas);

        // Em FALHA, não é mais tentada.
        var depoisDaFalha = await reprocessador.ReprocessarPendentesAsync();
        Assert.Equal(0, depoisDaFalha.Reprocessadas);
        Assert.Equal(3, _leomob.Chamadas);
    }

    [Fact]
    public async Task Reprocessamento_com_sucesso_conclui()
    {
        await using var db = await pg.CriarBancoAsync();
        _leomob.DeveFalhar = true;
        await ReceberZmobAsync(db);

        _leomob.DeveFalhar = false;
        var resultado = await CriarReprocessador(db).ReprocessarPendentesAsync();

        Assert.Equal(1, resultado.Concluidas);
        var notificacao = await LerNotificacaoAsync(db);
        Assert.Equal(StatusNotificacao.CONCLUIDO, notificacao.Status);
        Assert.Equal(1, notificacao.Tentativas);
    }

    [Fact]
    public async Task Sem_pendentes_nao_chama_leomob()
    {
        await using var db = await pg.CriarBancoAsync();

        var resultado = await CriarReprocessador(db).ReprocessarPendentesAsync();

        Assert.Equal(new ResultadoReprocessamento(0, 0, 0, 0), resultado);
        Assert.Equal(0, _leomob.Chamadas);
    }
}
