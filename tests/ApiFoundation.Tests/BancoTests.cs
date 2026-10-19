using ApiFoundation.Domain;
using ApiFoundation.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApiFoundation.Tests;

[Collection("postgres")]
public class BancoTests(PostgresFixture pg)
{
    [Fact]
    public async Task Seed_cria_os_tres_pedidos_do_enunciado()
    {
        await using var db = await pg.CriarBancoAsync();

        var pedidos = await db.Pedidos
            .OrderBy(p => p.PedidoSVL)
            .Select(p => new { p.PedidoSVL, p.ServiceId, p.TipoPedido })
            .ToListAsync();

        Assert.Equal(new[]
        {
            new { PedidoSVL = 1007, ServiceId = 21763653L, TipoPedido = TipoPedido.ZLPL },
            new { PedidoSVL = 1008, ServiceId = 21763654L, TipoPedido = TipoPedido.ZCVS },
            new { PedidoSVL = 1012, ServiceId = 21763699L, TipoPedido = TipoPedido.ZMOB },
        }, pedidos);
    }

    [Fact]
    public async Task PedidoSVL_duplicado_e_rejeitado_pelo_banco()
    {
        await using var db = await pg.CriarBancoAsync();
        db.Pedidos.Add(new Pedido
        {
            PedidoSVL = 1007,
            ServiceId = 1,
            TipoPedido = TipoPedido.ZLPL,
            Status = "CRIADO",
            DataCriacao = DateTime.UtcNow
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // Garante que quem barrou foi o índice único de PedidoSVL, e não outra restrição.
        var pgEx = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pgEx.SqlState);
        Assert.Equal("IX_Pedidos_PedidoSVL", pgEx.ConstraintName);
    }
}
