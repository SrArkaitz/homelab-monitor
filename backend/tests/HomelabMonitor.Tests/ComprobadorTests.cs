using System.Net;
using System.Net.Sockets;
using HomelabMonitor.Api.Models;
using HomelabMonitor.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomelabMonitor.Tests;

public class ComprobadorTests
{
    private static Servicio ServicioHttp() => new() { Nombre = "Prueba", Destino = "http://servidor.test/health" };

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Considera_vivo_un_servicio_que_responde(HttpStatusCode codigo)
    {
        var comprobador = new Comprobador(HttpClientFactoryFalso.Respondiendo(codigo), NullLogger<Comprobador>.Instance);

        var resultado = await comprobador.ComprobarAsync(ServicioHttp(), CancellationToken.None);

        Assert.True(resultado.Ok);
        Assert.Equal((int?)codigo, resultado.CodigoHttp);
        Assert.Null(resultado.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Considera_caido_un_error_http(HttpStatusCode codigo)
    {
        var comprobador = new Comprobador(HttpClientFactoryFalso.Respondiendo(codigo), NullLogger<Comprobador>.Instance);

        var resultado = await comprobador.ComprobarAsync(ServicioHttp(), CancellationToken.None);

        Assert.False(resultado.Ok);
        Assert.Equal((int?)codigo, resultado.CodigoHttp);
        Assert.StartsWith($"HTTP {(int)codigo}", resultado.Error);
    }

    [Fact]
    public async Task Traduce_un_error_de_conexion()
    {
        var handler = new HandlerFalso(_ => throw new HttpRequestException(
            HttpRequestError.ConnectionError, "Connection refused", new SocketException()));
        var comprobador = new Comprobador(new HttpClientFactoryFalso(handler), NullLogger<Comprobador>.Instance);

        var resultado = await comprobador.ComprobarAsync(ServicioHttp(), CancellationToken.None);

        Assert.False(resultado.Ok);
        Assert.Null(resultado.CodigoHttp);
        Assert.Equal("No se pudo conectar", resultado.Error);
    }

    [Fact]
    public async Task Tcp_a_un_puerto_abierto_funciona()
    {
        // Abrimos un puerto local de verdad para probar la conexión TCP.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
            var servicio = new Servicio { Nombre = "Local", Destino = $"127.0.0.1:{puerto}", Tipo = TipoComprobacion.Tcp };
            var comprobador = new Comprobador(HttpClientFactoryFalso.Respondiendo(HttpStatusCode.OK), NullLogger<Comprobador>.Instance);

            var resultado = await comprobador.ComprobarAsync(servicio, CancellationToken.None);

            Assert.True(resultado.Ok);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Tcp_a_un_puerto_cerrado_falla()
    {
        // Pedimos un puerto libre y lo cerramos para que nadie escuche en él.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var servicio = new Servicio { Nombre = "Cerrado", Destino = $"127.0.0.1:{puerto}", Tipo = TipoComprobacion.Tcp, TimeoutSegundos = 3 };
        var comprobador = new Comprobador(HttpClientFactoryFalso.Respondiendo(HttpStatusCode.OK), NullLogger<Comprobador>.Instance);

        var resultado = await comprobador.ComprobarAsync(servicio, CancellationToken.None);

        Assert.False(resultado.Ok);
        Assert.NotNull(resultado.Error);
    }
}
