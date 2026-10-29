namespace HomelabMonitor.Api.Services;

/// <summary>
/// Tarea en segundo plano que arranca con la aplicación.
/// Cada pocos segundos mira qué servicios toca comprobar, y una vez por hora limpia el histórico.
/// </summary>
public class MonitorWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider reloj,
    ILogger<MonitorWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Ciclo = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CicloLimpieza = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Monitor iniciado");

        var ultimaLimpieza = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(Ciclo, reloj);

        do
        {
            try
            {
                // El worker es singleton y el DbContext es scoped: abrimos un scope por ciclo.
                await using var scope = scopeFactory.CreateAsyncScope();
                var monitor = scope.ServiceProvider.GetRequiredService<MonitorService>();

                await monitor.ComprobarPendientesAsync(stoppingToken);

                if (reloj.GetUtcNow() - ultimaLimpieza >= CicloLimpieza)
                {
                    var borradas = await monitor.LimpiarAntiguasAsync(stoppingToken);
                    if (borradas > 0)
                    {
                        logger.LogInformation("Borradas {Cantidad} comprobaciones antiguas", borradas);
                    }
                    ultimaLimpieza = reloj.GetUtcNow();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un error en un ciclo no debe parar la monitorización.
                logger.LogError(ex, "Error en el ciclo de monitorización");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
