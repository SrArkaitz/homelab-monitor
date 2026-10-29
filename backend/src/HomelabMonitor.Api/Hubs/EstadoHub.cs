using HomelabMonitor.Api.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace HomelabMonitor.Api.Hubs;

/// <summary>
/// Métodos que el servidor invoca en el navegador.
/// Con un hub tipado el compilador comprueba los nombres y los parámetros.
/// </summary>
public interface IEstadoCliente
{
    Task ServicioActualizado(ServicioDto servicio);

    Task ServicioEliminado(int id);
}

/// <summary>
/// El panel de Angular se conecta aquí para recibir los cambios en tiempo real
/// sin tener que estar preguntando a la API cada pocos segundos.
/// </summary>
public class EstadoHub : Hub<IEstadoCliente>
{
}
