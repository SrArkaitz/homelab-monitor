using System.Diagnostics;
using System.Net.Sockets;
using HomelabMonitor.Api.Models;

namespace HomelabMonitor.Api.Services;

public record ResultadoComprobacion(bool Ok, int? CodigoHttp, int LatenciaMs, string? Error);

public interface IComprobador
{
    Task<ResultadoComprobacion> ComprobarAsync(Servicio servicio, CancellationToken ct);
}

/// <summary>
/// Hace la comprobación real contra el servicio: una petición HTTP o una conexión TCP.
/// </summary>
public class Comprobador(IHttpClientFactory httpClientFactory, ILogger<Comprobador> logger) : IComprobador
{
    public const string ClienteNormal = "monitor";
    public const string ClienteSinValidarCertificado = "monitor-inseguro";

    public async Task<ResultadoComprobacion> ComprobarAsync(Servicio servicio, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(servicio.TimeoutSegundos));

        var cronometro = Stopwatch.StartNew();
        try
        {
            return servicio.Tipo switch
            {
                TipoComprobacion.Http => await ComprobarHttpAsync(servicio, cronometro, timeout.Token),
                TipoComprobacion.Tcp => await ComprobarTcpAsync(servicio, cronometro, timeout.Token),
                _ => new ResultadoComprobacion(false, null, 0, $"Tipo no soportado: {servicio.Tipo}")
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fallo(cronometro, $"Sin respuesta en {servicio.TimeoutSegundos} s");
        }
        catch (HttpRequestException ex)
        {
            return Fallo(cronometro, ex.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "No se pudo resolver el nombre",
                HttpRequestError.ConnectionError => "No se pudo conectar",
                HttpRequestError.SecureConnectionError => "Error de certificado o TLS",
                _ => ex.Message
            });
        }
        catch (SocketException ex)
        {
            return Fallo(cronometro, $"Conexión rechazada ({ex.SocketErrorCode})");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Error inesperado comprobando {Servicio}", servicio.Nombre);
            return Fallo(cronometro, ex.Message);
        }
    }

    private async Task<ResultadoComprobacion> ComprobarHttpAsync(Servicio servicio, Stopwatch cronometro, CancellationToken ct)
    {
        var cliente = httpClientFactory.CreateClient(
            servicio.IgnorarCertificado ? ClienteSinValidarCertificado : ClienteNormal);

        // Solo nos interesan las cabeceras: no descargamos el cuerpo entero.
        using var respuesta = await cliente.GetAsync(servicio.Destino, HttpCompletionOption.ResponseHeadersRead, ct);
        var latencia = (int)cronometro.ElapsedMilliseconds;
        var codigo = (int)respuesta.StatusCode;

        // 2xx y 3xx cuentan como que el servicio está vivo; 401/403 también,
        // porque significa que responde aunque pida iniciar sesión.
        var ok = codigo is >= 200 and < 400 or 401 or 403;

        return new ResultadoComprobacion(ok, codigo, latencia, ok ? null : $"HTTP {codigo} {respuesta.ReasonPhrase}");
    }

    private static async Task<ResultadoComprobacion> ComprobarTcpAsync(Servicio servicio, Stopwatch cronometro, CancellationToken ct)
    {
        if (!DestinoParser.TryParseTcp(servicio.Destino, out var host, out var puerto))
        {
            return new ResultadoComprobacion(false, null, 0, "Destino TCP no válido");
        }

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, puerto, ct);

        return new ResultadoComprobacion(true, null, (int)cronometro.ElapsedMilliseconds, null);
    }

    private static ResultadoComprobacion Fallo(Stopwatch cronometro, string error) =>
        new(false, null, (int)cronometro.ElapsedMilliseconds, error);
}
