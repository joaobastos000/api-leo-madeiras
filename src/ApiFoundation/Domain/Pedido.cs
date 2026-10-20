namespace ApiFoundation.Domain;

public class Pedido
{
    public int Id { get; set; }
    public int PedidoSVL { get; set; }
    public long ServiceId { get; set; }
    public TipoPedido TipoPedido { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }

    public List<LinkArquivo> Links { get; set; } = [];
    public List<NotificacaoLeomob> Notificacoes { get; set; } = [];
}
