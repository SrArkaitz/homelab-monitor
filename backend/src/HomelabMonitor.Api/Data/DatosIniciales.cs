using HomelabMonitor.Api.Models;

namespace HomelabMonitor.Api.Data;

/// <summary>
/// Carga los servicios del servidor la primera vez que arranca la aplicación.
/// </summary>
public static class DatosIniciales
{
    public static void Cargar(MonitorDbContext db)
    {
        if (db.Servicios.Any())
        {
            return;
        }

        db.Servicios.AddRange(
            new Servicio { Nombre = "OpenCloud", Destino = "https://nube.kaitzlab.com", IntervaloSegundos = 60 },
            new Servicio { Nombre = "Immich", Destino = "http://192.168.1.138:2283", IntervaloSegundos = 60 },
            new Servicio { Nombre = "Jellyfin", Destino = "http://192.168.1.138:8096/health", IntervaloSegundos = 60 },
            new Servicio { Nombre = "Portainer", Destino = "https://192.168.1.138:9443", IgnorarCertificado = true, IntervaloSegundos = 120 },
            new Servicio { Nombre = "Gestión hogar", Destino = "http://192.168.1.138:8085/api/health", IntervaloSegundos = 60 },
            new Servicio { Nombre = "SSH", Destino = "192.168.1.138:22", Tipo = TipoComprobacion.Tcp, IntervaloSegundos = 120 }
        );

        db.SaveChanges();
    }
}
