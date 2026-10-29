using HomelabMonitor.Api.Data;
using HomelabMonitor.Api.Dtos;
using HomelabMonitor.Api.Models;
using HomelabMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HomelabMonitor.Api.Controllers;

[ApiController]
[Route("api/servicios")]
[Produces("application/json")]
public class ServiciosController(
    MonitorDbContext db,
    ServiciosConsulta consulta,
    MonitorService monitor,
    IPublicadorTiempoReal publicador) : ControllerBase
{
    /// <summary>Lista todos los servicios con su estado actual.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ServicioDto>>> Listar(CancellationToken ct) =>
        await consulta.ObtenerTodosAsync(ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType<ServicioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServicioDto>> Obtener(int id, CancellationToken ct)
    {
        var servicio = await consulta.ObtenerAsync(id, ct);
        return servicio is null ? NotFound() : servicio;
    }

    /// <summary>Histórico de comprobaciones de las últimas horas (máximo 7 días).</summary>
    [HttpGet("{id:int}/comprobaciones")]
    [ProducesResponseType<List<ComprobacionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ComprobacionDto>>> Historial(
        int id,
        [FromQuery] int horas = 24,
        CancellationToken ct = default)
    {
        if (!await db.Servicios.AnyAsync(s => s.Id == id, ct))
        {
            return NotFound();
        }

        return await consulta.HistorialAsync(id, Math.Clamp(horas, 1, 168), ct);
    }

    [HttpPost]
    [ProducesResponseType<ServicioDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServicioDto>> Crear(GuardarServicioDto datos, CancellationToken ct)
    {
        if (await NombreEnUsoAsync(datos.Nombre, idExcluido: null, ct))
        {
            return NombreDuplicado(datos.Nombre);
        }

        var servicio = new Servicio { Nombre = datos.Nombre.Trim(), Destino = datos.Destino.Trim() };
        Aplicar(servicio, datos);

        db.Servicios.Add(servicio);
        await db.SaveChangesAsync(ct);

        // Lo comprobamos ya para que el panel no lo muestre como "desconocido" un minuto.
        if (servicio.Activo)
        {
            await monitor.ComprobarAhoraAsync(servicio.Id, ct);
        }

        var dto = await consulta.ObtenerAsync(servicio.Id, ct);
        return CreatedAtAction(nameof(Obtener), new { id = servicio.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ServicioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServicioDto>> Editar(int id, GuardarServicioDto datos, CancellationToken ct)
    {
        var servicio = await db.Servicios.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (servicio is null)
        {
            return NotFound();
        }

        if (await NombreEnUsoAsync(datos.Nombre, idExcluido: id, ct))
        {
            return NombreDuplicado(datos.Nombre);
        }

        var cambiaDestino = servicio.Destino != datos.Destino.Trim() || servicio.Tipo != datos.Tipo;

        servicio.Nombre = datos.Nombre.Trim();
        servicio.Destino = datos.Destino.Trim();
        Aplicar(servicio, datos);

        if (!servicio.Activo || cambiaDestino)
        {
            // Si cambia a dónde apuntamos, el estado anterior ya no vale.
            servicio.EstadoActual = EstadoServicio.Desconocido;
            servicio.EstadoDesde = null;
            servicio.UltimaComprobacion = null;
            servicio.UltimaLatenciaMs = null;
            servicio.UltimoError = null;
        }

        await db.SaveChangesAsync(ct);

        if (servicio.Activo && cambiaDestino)
        {
            await monitor.ComprobarAhoraAsync(id, ct);
        }

        var dto = await consulta.ObtenerAsync(id, ct);
        await publicador.ServicioActualizadoAsync(dto!, ct);
        return dto!;
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var borrados = await db.Servicios.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        if (borrados == 0)
        {
            return NotFound();
        }

        await publicador.ServicioEliminadoAsync(id, ct);
        return NoContent();
    }

    /// <summary>Lanza una comprobación inmediata, sin esperar al intervalo.</summary>
    [HttpPost("{id:int}/comprobar")]
    [ProducesResponseType<ServicioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServicioDto>> ComprobarAhora(int id, CancellationToken ct)
    {
        if (!await monitor.ComprobarAhoraAsync(id, ct))
        {
            return NotFound();
        }

        return (await consulta.ObtenerAsync(id, ct))!;
    }

    private static void Aplicar(Servicio servicio, GuardarServicioDto datos)
    {
        servicio.Tipo = datos.Tipo;
        servicio.IntervaloSegundos = datos.IntervaloSegundos;
        servicio.TimeoutSegundos = datos.TimeoutSegundos;
        servicio.IgnorarCertificado = datos.IgnorarCertificado;
        servicio.Activo = datos.Activo;
    }

    private Task<bool> NombreEnUsoAsync(string nombre, int? idExcluido, CancellationToken ct)
    {
        var limpio = nombre.Trim().ToLower();
        return db.Servicios.AnyAsync(s => s.Nombre.ToLower() == limpio && s.Id != idExcluido, ct);
    }

    private ObjectResult NombreDuplicado(string nombre) =>
        Problem(
            title: "Nombre duplicado",
            detail: $"Ya existe un servicio llamado \"{nombre.Trim()}\"",
            statusCode: StatusCodes.Status409Conflict);
}
