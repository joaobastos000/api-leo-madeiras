using ApiFoundation.Services;
using Microsoft.Extensions.Options;

namespace ApiFoundation.Infrastructure;

/// <summary>
/// Worker que, a cada intervalo, reprocessa as notificações PENDENTE sozinho.
/// Usa o mesmo ReprocessadorNotificacoes do endpoint manual.
/// </summary>
public class ReprocessamentoBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReprocessamentoOptions> options,
    ILogger<ReprocessamentoBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuracao = options.Value;
        if (!configuracao.Habilitado)
        {
            logger.LogInformation("Reprocessamento automático desabilitado");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configuracao.IntervaloSegundos));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // O worker é Singleton e o reprocessador (com o DbContext) é Scoped: um escopo novo por ciclo.
                using var scope = scopeFactory.CreateScope();
                var reprocessador = scope.ServiceProvider.GetRequiredService<ReprocessadorNotificacoes>();

                var resultado = await reprocessador.ReprocessarPendentesAsync(stoppingToken);
                if (resultado.Reprocessadas > 0)
                    logger.LogInformation("Reprocessamento automático: {Resultado}", resultado);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Um ciclo com erro (ex.: banco fora do ar) não derruba o worker; tenta de novo no próximo.
                logger.LogError(ex, "Erro no reprocessamento automático");
            }
        }
    }
}
