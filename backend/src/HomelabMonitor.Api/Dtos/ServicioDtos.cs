using System.ComponentModel.DataAnnotations;
using HomelabMonitor.Api.Models;
using HomelabMonitor.Api.Services;

namespace HomelabMonitor.Api.Dtos;

/// <summary>Servicio con su estado y estadísticas de las últimas 24 horas.</summary>
public record ServicioDto(
    int Id,
    string Nombre,
    string Destino,
    TipoComprobacion Tipo,
    int IntervaloSegundos,
    int TimeoutSegundos,
    bool IgnorarCertificado,
    bool Activo,
    EstadoServicio Estado,
    DateTime? UltimaComprobacion,
    DateTime? EstadoDesde,
    double? Uptime24h,
    int? LatenciaMedia24hMs,
    int? UltimaLatenciaMs,
    string? UltimoError);

/// <summary>Datos para crear o editar un servicio.</summary>
public record GuardarServicioDto : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Nombre { get; init; } = "";

    [Required, StringLength(500)]
    public string Destino { get; init; } = "";

    public TipoComprobacion Tipo { get; init; } = TipoComprobacion.Http;

    [Range(10, 86_400)]
    public int IntervaloSegundos { get; init; } = 60;

    [Range(1, 60)]
    public int TimeoutSegundos { get; init; } = 10;

    public bool IgnorarCertificado { get; init; }

    public bool Activo { get; init; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Tipo == TipoComprobacion.Http && !DestinoParser.EsUrlHttpValida(Destino))
        {
            yield return new ValidationResult(
                "Para HTTP el destino debe ser una URL que empiece por http:// o https://",
                [nameof(Destino)]);
        }

        if (Tipo == TipoComprobacion.Tcp && !DestinoParser.TryParseTcp(Destino, out _, out _))
        {
            yield return new ValidationResult(
                "Para TCP el destino debe tener el formato host:puerto (por ejemplo 192.168.1.138:22)",
                [nameof(Destino)]);
        }

        if (TimeoutSegundos >= IntervaloSegundos)
        {
            yield return new ValidationResult(
                "El timeout tiene que ser menor que el intervalo",
                [nameof(TimeoutSegundos)]);
        }
    }
}

public record ComprobacionDto(DateTime Fecha, bool Ok, int? CodigoHttp, int LatenciaMs, string? Error);

public record ResumenDto(int Total, int Activos, int Caidos, int Desconocidos, double? Uptime24h);
