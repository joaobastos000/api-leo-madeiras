namespace ApiFoundation.Domain;

public enum TipoPedido
{
    /// <summary>Corte feito na própria loja.</summary>
    ZLPL,

    /// <summary>Corte feito na central de serviços compartilhada.</summary>
    ZCVS,

    /// <summary>Pedido de outro canal (móveis 3D), atendido pela LeomobAPI.</summary>
    ZMOB
}
