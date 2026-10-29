using System.Net.Http.Json;
using HomelabMonitor.Api.Models;
using Microsoft.Extensions.Options;

namespace HomelabMonitor.Api.Services;

public record CambioEstado(Servicio Servicio, EstadoServicio Anterior, EstadoServicio Nuevo, string? Error);

/// <summary>Un canal por el que avisar cuando un servicio cae o se recupera.</summary>
public interface INotificador
{
    Task NotificarAsync(CambioEstado cambio, CancellationToken ct);
}

/// <summary>Siempre activo: deja constancia en el log.</summary>
public class NotificadorLog(ILogger<NotificadorLog> logger) : INotificador
{
    public Task NotificarAsync(CambioEstado cambio, CancellationToken ct)
    {
        if (cambio.Nuevo == EstadoServicio.Caido)
        {
            logger.LogWarning("{Servicio} ha caído: {Error}", cambio.Servicio.Nombre, cambio.Error);
        }
        else
        {
            logger.LogInformation("{Servicio} pasa de {Anterior} a {Nuevo}",
                cambio.Servicio.Nombre, cambio.Anterior, cambio.Nuevo);
        }

        return Task.CompletedTask;
    }
}

public class OpcionesTelegram
{
    public const string Seccion = "Avisos:Telegram";

    public string? BotToken { get; set; }

    public string? ChatId { get; set; }

    public bool Configurado => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}

/// <summary>
/// Manda un mensaje por Telegram. Solo se registra si hay token y chat configurados.
/// </summary>
public class NotificadorTelegram(
    HttpClient http,
    IOptions<OpcionesTelegram> opciones,
    ILogger<NotificadorTelegram> logger) : INotificador
{
    public async Task NotificarAsync(CambioEstado cambio, CancellationToken ct)
    {
        // Pasar de "desconocido" a "activo" es solo el arranque: no merece aviso.
        if (cambio.Anterior == EstadoServicio.Desconocido && cambio.Nuevo == EstadoServicio.Activo)
        {
            return;
        }

        var texto = cambio.Nuevo == EstadoServicio.Caido
            ? $"🔴 {cambio.Servicio.Nombre} está caído\n{cambio.Error}"
            : $"🟢 {cambio.Servicio.Nombre} vuelve a funcionar";

        var config = opciones.Value;
        try
        {
            var respuesta = await http.PostAsJsonAsync(
                $"https://api.telegram.org/bot{config.BotToken}/sendMessage",
                new { chat_id = config.ChatId, text = texto },
                ct);

            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning("Telegram respondió {Codigo}", (int)respuesta.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Que falle el aviso no debe romper la monitorización.
            logger.LogWarning(ex, "No se pudo enviar el aviso por Telegram");
        }
    }
}
