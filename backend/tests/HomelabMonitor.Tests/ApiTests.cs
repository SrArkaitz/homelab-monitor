using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomelabMonitor.Api.Dtos;
using HomelabMonitor.Api.Models;
using HomelabMonitor.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HomelabMonitor.Tests;

/// <summary>
/// Levanta la API entera en memoria con una BD temporal y prueba los endpoints de verdad.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string rutaBd = Path.Combine(Path.GetTempPath(), $"monitor-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Monitor", $"Data Source={rutaBd}");

        builder.ConfigureServices(services =>
        {
            // Sin tareas en segundo plano ni peticiones de red reales.
            var worker = services.Single(d => d.ImplementationType == typeof(MonitorWorker));
            services.Remove(worker);

            services.RemoveAll<IComprobador>();
            services.AddSingleton<IComprobador>(new ComprobadorFalso());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(rutaBd); } catch (IOException) { }
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient cliente = factory.CreateClient();

    [Fact]
    public async Task Health_responde_ok()
    {
        var respuesta = await cliente.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Arranca_con_los_servicios_del_servidor()
    {
        var servicios = await cliente.GetFromJsonAsync<List<ServicioDto>>("/api/servicios", Json);

        Assert.NotNull(servicios);
        Assert.Contains(servicios, s => s.Nombre == "Jellyfin");
        Assert.Contains(servicios, s => s.Nombre == "SSH" && s.Tipo == TipoComprobacion.Tcp);
    }

    [Fact]
    public async Task Crear_un_servicio_lo_comprueba_al_momento()
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/servicios", new GuardarServicioDto
        {
            Nombre = "Web de prueba",
            Destino = "https://prueba.kaitzlab.com"
        }, Json);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await respuesta.Content.ReadFromJsonAsync<ServicioDto>(Json);
        Assert.Equal(EstadoServicio.Activo, creado!.Estado);
        Assert.NotNull(respuesta.Headers.Location);
    }

    [Fact]
    public async Task Rechaza_una_url_mal_escrita()
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/servicios", new GuardarServicioDto
        {
            Nombre = "Mal",
            Destino = "esto no es una url"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_nombres_duplicados()
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/servicios", new GuardarServicioDto
        {
            Nombre = "jellyfin",
            Destino = "http://otro.test"
        }, Json);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Servicio_inexistente_da_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/servicios/9999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.DeleteAsync("/api/servicios/9999")).StatusCode);
    }

    [Fact]
    public async Task Borrar_un_servicio_lo_quita_de_la_lista()
    {
        var creado = await (await cliente.PostAsJsonAsync("/api/servicios", new GuardarServicioDto
        {
            Nombre = "Temporal",
            Destino = "http://temporal.test"
        }, Json)).Content.ReadFromJsonAsync<ServicioDto>(Json);

        var borrado = await cliente.DeleteAsync($"/api/servicios/{creado!.Id}");
        var servicios = await cliente.GetFromJsonAsync<List<ServicioDto>>("/api/servicios", Json);

        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);
        Assert.DoesNotContain(servicios!, s => s.Id == creado.Id);
    }
}
