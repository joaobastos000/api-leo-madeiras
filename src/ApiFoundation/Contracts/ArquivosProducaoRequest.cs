using System.ComponentModel.DataAnnotations;

namespace ApiFoundation.Contracts;

/// <summary>Corpo enviado pela Digibee com os links dos arquivos gerados pelo CorteCloud.</summary>
public class ArquivosProducaoRequest
{
    [Required]
    [RegularExpression("^filesToProduction$", ErrorMessage = "documentType deve ser 'filesToProduction'.")]
    public string? DocumentType { get; init; }

    public bool HasErrors { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "services deve ter pelo menos um serviço.")]
    public List<ServicoArquivosRequest>? Services { get; init; }
}

public class ServicoArquivosRequest
{
    // Nullable + [Required]: sem isso, um campo ausente viraria 0 silenciosamente em vez de dar 400.
    [Required]
    public long? ServiceId { get; init; }

    [Required]
    public int? PedidoSVL { get; init; }

    [Required]
    public LinksArquivosRequest? Links { get; init; }
}

public class LinksArquivosRequest
{
    public CorteLinksRequest? Corte { get; init; }
    public List<string>? Etiquetas { get; init; }
    public List<string>? Usinagem { get; init; }

    /// <summary>
    /// Transforma a estrutura aninhada numa lista de (categoria, url),
    /// ignorando listas ausentes e URLs vazias, sem URLs repetidas.
    /// </summary>
    public IReadOnlyList<LinkRecebido> Achatar()
    {
        (string Categoria, List<string>? Urls)[] categorias =
        [
            ("corte.pdfSumario", Corte?.PdfSumario),
            ("corte.pdfDetalhado", Corte?.PdfDetalhado),
            ("etiquetas", Etiquetas),
            ("usinagem", Usinagem)
        ];

        return categorias
            .SelectMany(c => (c.Urls ?? []).Select(url => new LinkRecebido(c.Categoria, url)))
            .Where(link => !string.IsNullOrWhiteSpace(link.Url))
            .DistinctBy(link => link.Url)
            .ToList();
    }
}

public class CorteLinksRequest
{
    public List<string>? PdfSumario { get; init; }
    public List<string>? PdfDetalhado { get; init; }
}

public readonly record struct LinkRecebido(string Categoria, string Url);
