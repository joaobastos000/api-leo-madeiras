namespace ApiFoundation.Contracts;

/// <summary>O pedido, seus links agrupados por categoria e a notificação mais recente (se houver).</summary>
public record PedidoResponse(
    int PedidoSVL,
    long ServiceId,
    string TipoPedido,
    string Status,
    Dictionary<string, List<string>> Links,
    NotificacaoResponse? Notificacao);

public record NotificacaoResponse(string Status, int Tentativas, DateTime DataAtualizacao);
