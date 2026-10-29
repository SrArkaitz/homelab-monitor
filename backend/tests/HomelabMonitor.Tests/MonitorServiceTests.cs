using HomelabMonitor.Api.Data;
using HomelabMonitor.Api.Models;
using HomelabMonitor.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomelabMonitor.Tests;

/// <summary>
/// Tests de la lógica de monitorización contra una base de datos SQLite en memoria,
/// que se comporta igual que la real (a diferencia del proveedor InMemory de EF).
/// </summary>
public sealed class MonitorServiceTests : IDisposable
{
    private readonly SqliteConnection conexion;
    private readonly MonitorDbContext db;
    private readonly RelojFalso reloj = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly ComprobadorFalso comprobador = new();
    private readonly NotificadorFalso notificador = new();
    private readonly PublicadorFalso publicador = new();
    private readonly MonitorService monitor;

    public MonitorServiceTests()
    {
        // La BD en memoria vive mientras la conexión esté abierta.
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();

        db = new MonitorDbContext(new DbContextOptionsBuilder<MonitorDbContext>().UseSqlite(conexion).Options);
        db.Database.EnsureCreated();

        monitor = new MonitorService(
            db,
            comprobador,
            [notificador],
            publicador,
            new ServiciosConsulta(db, reloj),
            reloj,
            Options.Create(new OpcionesMonitor { FallosParaCaido = 2 }),
            NullLogger<MonitorService>.Instance);
    }

    public void Dispose()
    {
        db.Dispose();
        conexion.Dispose();
    }

    private Servicio Crear(string nombre, int intervalo = 60, bool activo = true)
    {
        var servicio = new Servicio
        {
            Nombre = nombre,
            Destino = $"http://{nombre.ToLower()}.test",
            IntervaloSegundos = intervalo,
            Activo = activo
        };
        db.Servicios.Add(servicio);
        db.SaveChanges();
        return servicio;
    }

    [Fact]
    public async Task Comprueba_los_servicios_nuevos_y_guarda_el_resultado()
    {
        var servicio = Crear("Jellyfin");

        var comprobados = await monitor.ComprobarPendientesAsync(CancellationToken.None);

        Assert.Equal(1, comprobados);
        Assert.Equal(EstadoServicio.Activo, servicio.EstadoActual);
        Assert.Equal(42, servicio.UltimaLatenciaMs);
        Assert.Single(await db.Comprobaciones.ToListAsync());
    }

    [Fact]
    public async Task No_comprueba_hasta_que_pasa_el_intervalo()
    {
        Crear("Immich", intervalo: 60);
        await monitor.ComprobarPendientesAsync(CancellationToken.None);

        reloj.Avanzar(TimeSpan.FromSeconds(30));
        Assert.Equal(0, await monitor.ComprobarPendientesAsync(CancellationToken.None));

        reloj.Avanzar(TimeSpan.FromSeconds(30));
        Assert.Equal(1, await monitor.ComprobarPendientesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Ignora_los_servicios_desactivados()
    {
        Crear("Activo");
        Crear("Pausado", activo: false);

        await monitor.ComprobarPendientesAsync(CancellationToken.None);

        Assert.Equal(new[] { "Activo" }, comprobador.Comprobados);
    }

    [Fact]
    public async Task Avisa_solo_cuando_cae_de_verdad_y_cuando_se_recupera()
    {
        var servicio = Crear("OpenCloud");

        // ok → fallo → fallo → fallo → ok
        foreach (var ok in new[] { true, false, false, false, true })
        {
            comprobador.Resultados.Enqueue(ok);
            await monitor.ComprobarPendientesAsync(CancellationToken.None);
            reloj.Avanzar(TimeSpan.FromSeconds(60));
        }

        Assert.Equal(EstadoServicio.Activo, servicio.EstadoActual);
        Assert.Collection(notificador.Cambios,
            c => Assert.Equal((EstadoServicio.Desconocido, EstadoServicio.Activo), (c.Anterior, c.Nuevo)),
            c => Assert.Equal((EstadoServicio.Activo, EstadoServicio.Caido), (c.Anterior, c.Nuevo)),
            c => Assert.Equal((EstadoServicio.Caido, EstadoServicio.Activo), (c.Anterior, c.Nuevo)));
    }

    [Fact]
    public async Task Publica_cada_comprobacion_con_las_estadisticas()
    {
        Crear("Portainer");

        comprobador.Resultados.Enqueue(true);
        await monitor.ComprobarPendientesAsync(CancellationToken.None);
        reloj.Avanzar(TimeSpan.FromMinutes(1));
        comprobador.Resultados.Enqueue(false);
        await monitor.ComprobarPendientesAsync(CancellationToken.None);

        Assert.Equal(2, publicador.Publicados.Count);
        Assert.Equal((double?)50.0, publicador.Publicados[^1].Uptime24h);
        Assert.Equal("HTTP 503", publicador.Publicados[^1].UltimoError);
    }

    [Fact]
    public async Task Comprobar_ahora_ignora_el_intervalo()
    {
        var servicio = Crear("Gestión hogar");
        await monitor.ComprobarPendientesAsync(CancellationToken.None);

        var encontrado = await monitor.ComprobarAhoraAsync(servicio.Id, CancellationToken.None);

        Assert.True(encontrado);
        Assert.Equal(2, await db.Comprobaciones.CountAsync());
    }

    [Fact]
    public async Task Comprobar_ahora_un_servicio_que_no_existe_devuelve_false()
    {
        Assert.False(await monitor.ComprobarAhoraAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task Limpia_las_comprobaciones_antiguas()
    {
        Crear("SSH");
        await monitor.ComprobarPendientesAsync(CancellationToken.None);

        reloj.Avanzar(TimeSpan.FromDays(31));
        await monitor.ComprobarPendientesAsync(CancellationToken.None);

        var borradas = await monitor.LimpiarAntiguasAsync(CancellationToken.None);

        Assert.Equal(1, borradas);
        Assert.Equal(1, await db.Comprobaciones.CountAsync());
    }
}
