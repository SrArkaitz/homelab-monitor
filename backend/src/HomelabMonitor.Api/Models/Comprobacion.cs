namespace HomelabMonitor.Api.Models;

/// <summary>
/// Resultado de una comprobación puntual de un servicio.
/// </summary>
public class Comprobacion
{
    public long Id { get; set; }

    public int ServicioId { get; set; }

    public Servicio? Servicio { get; set; }

    public DateTime Fecha { get; set; }

    public bool Ok { get; set; }

    /// <summary>Código HTTP devuelto (null en comprobaciones TCP o si no hubo respuesta).</summary>
    public int? CodigoHttp { get; set; }

    public int LatenciaMs { get; set; }

    public string? Error { get; set; }
}
