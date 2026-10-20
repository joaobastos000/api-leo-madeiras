namespace ApiFoundation.Domain;

public class LinkArquivo
{
    public int Id { get; set; }
    public int PedidoId { get; set; }

    /// <summary>Origem do link no payload: corte.pdfSumario, corte.pdfDetalhado, etiquetas ou usinagem.</summary>
    public string Categoria { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}
