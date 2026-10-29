using HomelabMonitor.Api.Data;
using HomelabMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomelabMonitor.Api.Services;

public class OpcionesMonitor
{
    public const string Seccion = "Monitor";

    /// <summary>Fallos seguidos necesarios para dar un servicio por caído.</summary>
    public int FallosParaCaido { get; set; } = 2;

    /// <summary>Días que se guardan las comprobaciones antes de borrarlas.</summary>
    public int DiasRetencion { get; set; } = 30;
}

/// <summary>
/// Lógica principal: comprueba los servicios, guarda el resultado,
/// actualiza su estado y avisa cuando cambia.
/// </summary>
public class MonitorService(
    MonitorDbContext db,
    IComprobador comprobador,
    IEnumerable<INotificador> notificadores,
    IPublicadorTiempoReal publicador,
    ServiciosConsulta consulta,
    TimeProvider reloj,
    IOptions<OpcionesMonitor> opciones,
    ILogger<MonitorService> logger)
{
    /// <summary>
    /// Comprueba los servicios activos a los que ya les toca según su intervalo.
    /// Devuelve cuántos se han comprobado.
    /// </summary>
    public async Task<int> ComprobarPendientesAsync(CancellationToken ct)
    {
        var ahora = Ahora();
        var activos = await db.Servicios.Where(s => s.Activo).ToListAsync(ct);

        var pendientes = activos
            .Where(s => s.UltimaComprobacion is null
                        || s.UltimaComprobacion.Value.AddSeconds(s.IntervaloSegundos) <= ahora)
            .ToList();

        if (pendientes.Count == 0)
        {
            return 0;
        }

        // Las peticiones de red van en paralelo (cada una tarda lo suyo)...
        var resultados = await Task.WhenAll(pendientes.Select(s => comprobador.ComprobarAsync(s, ct)));

        // ...pero el DbContext no es thread-safe, así que guardamos de uno en uno.
        for (var i = 0; i < pendientes.Count; i++)
        {
            await RegistrarAsync(pendientes[i], resultados[i], ct);
        }

        return pendientes.Count;
    }

    /// <summary>Comprueba un servicio en el momento, aunque no le toque.</summary>
    public async Task<bool> ComprobarAhoraAsync(int servicioId, CancellationToken ct)
    {
        var servicio = await db.Servicios.FirstOrDefaultAsync(s => s.Id == servicioId, ct);
        if (servicio is null)
        {
            return false;
        }

        var resultado = await comprobador.ComprobarAsync(servicio, ct);
        await RegistrarAsync(servicio, resultado, ct);
        return true;
    }

    /// <summary>Borra las comprobaciones más antiguas que el periodo de retención.</summary>
    public Task<int> LimpiarAntiguasAsync(CancellationToken ct)
    {
        var limite = Ahora().AddDays(-opciones.Value.DiasRetencion);
        return db.Comprobaciones.Where(c => c.Fecha < limite).ExecuteDeleteAsync(ct);
    }

    private async Task RegistrarAsync(Servicio servicio, ResultadoComprobacion resultado, CancellationToken ct)
    {
        var ahora = Ahora();

        db.Comprobaciones.Add(new Comprobacion
        {
            ServicioId = servicio.Id,
            Fecha = ahora,
            Ok = resultado.Ok,
            CodigoHttp = resultado.CodigoHttp,
            LatenciaMs = resultado.LatenciaMs,
            Error = Recortar(resultado.Error, 500)
        });

        // Últimos resultados (el nuevo primero) para decidir el estado.
        var fallosParaCaido = opciones.Value.FallosParaCaido;
        var anteriores = await db.Comprobaciones
            .Where(c => c.ServicioId == servicio.Id)
            .OrderByDescending(c => c.Fecha)
            .Select(c => c.Ok)
            .Take(fallosParaCaido - 1)
            .ToListAsync(ct);

        var ultimos = new List<bool> { resultado.Ok };
        ultimos.AddRange(anteriores);

        var estadoAnterior = servicio.EstadoActual;
        var estadoNuevo = CalculadoraEstado.Calcular(estadoAnterior, ultimos, fallosParaCaido);

        servicio.UltimaComprobacion = ahora;
        servicio.UltimaLatenciaMs = resultado.Ok ? resultado.LatenciaMs : null;
        servicio.UltimoError = Recortar(resultado.Error, 500);

        if (estadoNuevo != estadoAnterior)
        {
            servicio.EstadoActual = estadoNuevo;
            servicio.EstadoDesde = ahora;
        }

        await db.SaveChangesAsync(ct);

        if (estadoNuevo != estadoAnterior)
        {
            await NotificarAsync(new CambioEstado(servicio, estadoAnterior, estadoNuevo, resultado.Error), ct);
        }

        await PublicarAsync(servicio, ct);
    }

    private async Task NotificarAsync(CambioEstado cambio, CancellationToken ct)
    {
        foreach (var notificador in notificadores)
        {
            try
            {
                await notificador.NotificarAsync(cambio, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Falló el notificador {Notificador}", notificador.GetType().Name);
            }
        }
    }

    private async Task PublicarAsync(Servicio servicio, CancellationToken ct)
    {
        try
        {
            var dto = await consulta.ObtenerAsync(servicio.Id, ct);
            if (dto is not null)
            {
                await publicador.ServicioActualizadoAsync(dto, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo publicar el estado de {Servicio}", servicio.Nombre);
        }
    }

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;

    private static string? Recortar(string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo];
}
