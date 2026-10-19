namespace ApiFoundation.Integrations;

/// <summary>Armazenamento interno da Leo para onde os arquivos de produção são copiados.</summary>
public interface IArmazenamentoArquivos
{
    Task CopiarAsync(int pedidoSVL, string categoria, string url, CancellationToken ct);
}
