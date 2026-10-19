namespace ApiFoundation.Contracts;

/// <summary>Quantas notificações pendentes foram tentadas e como cada uma terminou.</summary>
public record ResultadoReprocessamento(int Reprocessadas, int Concluidas, int Pendentes, int Falhas);
