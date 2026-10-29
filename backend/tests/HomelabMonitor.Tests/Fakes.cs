using System.Net;
using HomelabMonitor.Api.Dtos;
using HomelabMonitor.Api.Models;
using HomelabMonitor.Api.Services;

namespace HomelabMonitor.Tests;

/// <summary>Reloj que controlamos desde el test.</summary>
public class RelojFalso(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset ahora = inicio;

    public override DateTimeOffset GetUtcNow() => ahora;

    public void Avanzar(TimeSpan tiempo) => ahora += tiempo;
}

/// <summary>Comprobador que devuelve lo que le digamos, sin tocar la red.</summary>
public class ComprobadorFalso : IComprobador
{
    public Queue<bool> Resultados { get; } = new();

    public List<string> Comprobados { get; } = [];

    public bool PorDefecto { get; set; } = true;

    public Task<ResultadoComprobacion> ComprobarAsync(Servicio servicio, CancellationToken ct)
    {
        Comprobados.Add(servicio.Nombre);
        var ok = Resultados.Count > 0 ? Resultados.Dequeue() : PorDefecto;
        return Task.FromResult(new ResultadoComprobacion(ok, ok ? 200 : 503, 42, ok ? null : "HTTP 503"));
    }
}

public class NotificadorFalso : INotificador
{
    public List<CambioEstado> Cambios { get; } = [];

    public Task NotificarAsync(CambioEstado cambio, CancellationToken ct)
    {
        Cambios.Add(cambio);
        return Task.CompletedTask;
    }
}

public class PublicadorFalso : IPublicadorTiempoReal
{
    public List<ServicioDto> Publicados { get; } = [];

    public List<int> Eliminados { get; } = [];

    public Task ServicioActualizadoAsync(ServicioDto servicio, CancellationToken ct)
    {
        Publicados.Add(servicio);
        return Task.CompletedTask;
    }

    public Task ServicioEliminadoAsync(int id, CancellationToken ct)
    {
        Eliminados.Add(id);
        return Task.CompletedTask;
    }
}

/// <summary>Handler HTTP que responde siempre lo mismo, o lanza una excepción.</summary>
public class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));
}

public class HttpClientFactoryFalso(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);

    public static HttpClientFactoryFalso Respondiendo(HttpStatusCode codigo) =>
        new(new HandlerFalso(_ => new HttpResponseMessage(codigo)));
}
