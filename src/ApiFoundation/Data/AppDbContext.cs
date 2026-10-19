using ApiFoundation.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiFoundation.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Data fixa: com DateTime.UtcNow, cada nova migration enxergaria uma "mudança" no seed.
    private static readonly DateTime DataSeed = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<LinkArquivo> Links => Set<LinkArquivo>();
    public DbSet<NotificacaoLeomob> Notificacoes => Set<NotificacaoLeomob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>(pedido =>
        {
            pedido.HasIndex(p => p.PedidoSVL).IsUnique();
            pedido.Property(p => p.TipoPedido).HasConversion<string>().HasMaxLength(4);
            pedido.Property(p => p.Status).HasMaxLength(20);

            // O seed grava Ids explícitos; o identity começa depois deles para não colidir em novos inserts.
            pedido.Property(p => p.Id).HasIdentityOptions(startValue: 1000);

            pedido.HasData(
                new Pedido { Id = 1, PedidoSVL = 1007, ServiceId = 21763653, TipoPedido = TipoPedido.ZLPL, Status = "EM_PRODUCAO", DataCriacao = DataSeed },
                new Pedido { Id = 2, PedidoSVL = 1008, ServiceId = 21763654, TipoPedido = TipoPedido.ZCVS, Status = "EM_PRODUCAO", DataCriacao = DataSeed },
                new Pedido { Id = 3, PedidoSVL = 1012, ServiceId = 21763699, TipoPedido = TipoPedido.ZMOB, Status = "EM_PRODUCAO", DataCriacao = DataSeed });
        });

        modelBuilder.Entity<LinkArquivo>(link =>
        {
            // Base da idempotência: o mesmo link nunca é gravado duas vezes para o mesmo pedido.
            link.HasIndex(l => new { l.PedidoId, l.Url }).IsUnique();
            link.Property(l => l.Categoria).HasMaxLength(40);
        });

        modelBuilder.Entity<NotificacaoLeomob>(notificacao =>
        {
            // O reprocessamento busca por status (PENDENTE).
            notificacao.HasIndex(n => n.Status);
            notificacao.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
