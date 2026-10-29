using HomelabMonitor.Api.Dtos;
using HomelabMonitor.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace HomelabMonitor.Api.Services;

/// <summary>
/// Envía los cambios a los navegadores conectados.
/// Está detrás de una interfaz para que la lógica de monitorización no dependa de SignalR
/// y se pueda probar sin él.
/// </summary>
public interface IPublicadorTiempoReal
{
    Task ServicioActualizadoAsync(ServicioDto servicio, CancellationToken ct);

    Task ServicioEliminadoAsync(int id, CancellationToken ct);
}

public class PublicadorSignalR(IHubContext<EstadoHub, IEstadoCliente> hub) : IPublicadorTiempoReal
{
    public Task ServicioActualizadoAsync(ServicioDto servicio, CancellationToken ct) =>
        hub.Clients.All.ServicioActualizado(servicio);

    public Task ServicioEliminadoAsync(int id, CancellationToken ct) =>
        hub.Clients.All.ServicioEliminado(id);
}
