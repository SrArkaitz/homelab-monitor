using HomelabMonitor.Api.Data;
using HomelabMonitor.Api.Dtos;
using HomelabMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HomelabMonitor.Api.Services;

/// <summary>
/// Lecturas de servicios ya combinadas con sus estadísticas de las últimas 24 h.
/// </summary>
public class ServiciosConsulta(MonitorDbContext db, TimeProvider reloj)
{
    private static readonly TimeSpan Periodo = TimeSpan.FromHours(24);

    public async Task<List<ServicioDto>> ObtenerTodosAsync(CancellationToken ct)
    {
        var servicios = await db.Servicios.AsNoTracking().OrderBy(s => s.Nombre).ToListAsync(ct);
        var estadisticas = await EstadisticasAsync(servicioId: null, ct);

        return servicios.Select(s => ADto(s, estadisticas.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<ServicioDto?> ObtenerAsync(int id, CancellationToken ct)
    {
        var servicio = await db.Servicios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (servicio is null)
        {
            return null;
        }

        var estadisticas = await EstadisticasAsync(id, ct);
        return ADto(servicio, estadisticas.GetValueOrDefault(id));
    }

    public async Task<ResumenDto> ResumenAsync(CancellationToken ct)
    {
        var estados = await db.Servicios.AsNoTracking()
            .Where(s => s.Activo)
            .Select(s => s.EstadoActual)
            .ToListAsync(ct);

        var desde = Ahora() - Periodo;
        var recientes = db.Comprobaciones.AsNoTracking()
            .Where(c => c.Fecha >= desde && c.Servicio!.Activo);
        var total = await recientes.CountAsync(ct);
        var correctas = await recientes.CountAsync(c => c.Ok, ct);

        return new ResumenDto(
            Total: estados.Count,
            Activos: estados.Count(e => e == EstadoServicio.Activo),
            Caidos: estados.Count(e => e == EstadoServicio.Caido),
            Desconocidos: estados.Count(e => e == EstadoServicio.Desconocido),
            Uptime24h: CalculadoraEstadisticas.Uptime(total, correctas));
    }

    public async Task<List<ComprobacionDto>> HistorialAsync(int servicioId, int horas, CancellationToken ct)
    {
        var desde = Ahora() - TimeSpan.FromHours(horas);

        return await db.Comprobaciones.AsNoTracking()
            .Where(c => c.ServicioId == servicioId && c.Fecha >= desde)
            .OrderBy(c => c.Fecha)
            .Select(c => new ComprobacionDto(c.Fecha, c.Ok, c.CodigoHttp, c.LatenciaMs, c.Error))
            .ToListAsync(ct);
    }

    private async Task<Dictionary<int, Estadisticas>> EstadisticasAsync(int? servicioId, CancellationToken ct)
    {
        var desde = Ahora() - Periodo;

        var consulta = db.Comprobaciones.AsNoTracking().Where(c => c.Fecha >= desde);
        if (servicioId is not null)
        {
            consulta = consulta.Where(c => c.ServicioId == servicioId);
        }

        // Una sola consulta agrupada en la BD: total, aciertos y latencia media de los aciertos.
        return await consulta
            .GroupBy(c => c.ServicioId)
            .Select(g => new Estadisticas(
                g.Key,
                g.Count(),
                g.Count(c => c.Ok),
                g.Average(c => c.Ok ? (double?)c.LatenciaMs : null)))
            .ToDictionaryAsync(e => e.ServicioId, ct);
    }

    private DateTime Ahora() => reloj.GetUtcNow().UtcDateTime;

    internal static ServicioDto ADto(Servicio s, Estadisticas? e) => new(
        s.Id,
        s.Nombre,
        s.Destino,
        s.Tipo,
        s.IntervaloSegundos,
        s.TimeoutSegundos,
        s.IgnorarCertificado,
        s.Activo,
        s.EstadoActual,
        s.UltimaComprobacion,
        s.EstadoDesde,
        e is null ? null : CalculadoraEstadisticas.Uptime(e.Total, e.Ok),
        e?.LatenciaMedia is null ? null : (int)Math.Round(e.LatenciaMedia.Value),
        s.UltimaLatenciaMs,
        s.UltimoError);

    internal record Estadisticas(int ServicioId, int Total, int Ok, double? LatenciaMedia);
}

public static class CalculadoraEstadisticas
{
    /// <summary>Porcentaje de comprobaciones correctas, con dos decimales. Null si no hay datos.</summary>
    public static double? Uptime(int total, int ok)
    {
        if (total <= 0)
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(ok, total);
        ArgumentOutOfRangeException.ThrowIfNegative(ok);

        return Math.Round(ok * 100.0 / total, 2);
    }
}
