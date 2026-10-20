using ApiFoundation.Contracts;

namespace ApiFoundation.Tests.Infra;

public static class Payloads
{
    /// <summary>Um serviço como o do enunciado. Sem <paramref name="links"/>, usa os 4 links do exemplo.</summary>
    public static ServicoArquivosRequest Servico(int pedidoSVL, long serviceId, LinksArquivosRequest? links = null) => new()
    {
        PedidoSVL = pedidoSVL,
        ServiceId = serviceId,
        Links = links ?? LinksDoEnunciado(pedidoSVL)
    };

    public static LinksArquivosRequest LinksDoEnunciado(int pedidoSVL) => new()
    {
        Corte = new CorteLinksRequest
        {
            PdfSumario = [$"https://arquivos.exemplo.com/{pedidoSVL}/sumario.pdf"],
            PdfDetalhado = [$"https://arquivos.exemplo.com/{pedidoSVL}/detalhado.pdf"]
        },
        Etiquetas = [$"https://arquivos.exemplo.com/{pedidoSVL}/etiqueta-1.png"],
        Usinagem = [$"https://arquivos.exemplo.com/{pedidoSVL}/usinagem.xml"]
    };
}
