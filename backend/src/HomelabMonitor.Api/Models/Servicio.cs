namespace HomelabMonitor.Api.Models;

/// <summary>
/// Un servicio del servidor que se quiere vigilar (Jellyfin, Immich, una web...).
/// </summary>
public class Servicio
{
    public int Id { get; set; }

    public required string Nombre { get; set; }

    /// <summary>
    /// Para HTTP: URL completa (https://nube.kaitzlab.com).
    /// Para TCP: host:puerto (192.168.1.138:22).
    /// </summary>
    public required string Destino { get; set; }

    public TipoComprobacion Tipo { get; set; } = TipoComprobacion.Http;

    /// <summary>Cada cuántos segundos se comprueba.</summary>
    public int IntervaloSegundos { get; set; } = 60;

    /// <summary>Tiempo máximo de espera de cada comprobación.</summary>
    public int TimeoutSegundos { get; set; } = 10;

    /// <summary>Acepta certificados autofirmados (Portainer en 9443, por ejemplo).</summary>
    public bool IgnorarCertificado { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>Último estado conocido, para detectar cambios y avisar solo entonces.</summary>
    public EstadoServicio EstadoActual { get; set; } = EstadoServicio.Desconocido;

    public DateTime? UltimaComprobacion { get; set; }

    public int? UltimaLatenciaMs { get; set; }

    public string? UltimoError { get; set; }

    /// <summary>Desde cuándo está en el estado actual.</summary>
    public DateTime? EstadoDesde { get; set; }

    public List<Comprobacion> Comprobaciones { get; set; } = [];
}

public enum TipoComprobacion
{
    Http = 0,
    Tcp = 1
}

public enum EstadoServicio
{
    Desconocido = 0,
    Activo = 1,
    Caido = 2
}
