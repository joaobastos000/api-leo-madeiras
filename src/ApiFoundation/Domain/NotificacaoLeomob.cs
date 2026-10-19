namespace ApiFoundation.Domain;

public class NotificacaoLeomob
{
    /// <summary>Quantidade de chamadas com falha (contando a do recebimento) até a notificação ir para FALHA.</summary>
    public const int MaxTentativas = 3;

    public int Id { get; set; }
    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public StatusNotificacao Status { get; set; }

    /// <summary>Quantas chamadas à LeomobAPI falharam até agora.</summary>
    public int Tentativas { get; set; }

    public string? UltimoErro { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
