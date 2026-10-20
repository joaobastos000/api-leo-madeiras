namespace ApiFoundation.Contracts;

/// <summary>Resposta do POST: um resumo e o resultado de cada serviço recebido.</summary>
public record ResultadoProcessamento(int Processados, int Falhas, IReadOnlyList<ResultadoServico> Resultados);

public record ResultadoServico(int PedidoSVL, long ServiceId, bool Sucesso, int LinksNovos, string? Erro);
