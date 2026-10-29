using System.Text.Json.Serialization;
using HomelabMonitor.Api.Data;
using HomelabMonitor.Api.Hubs;
using HomelabMonitor.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Base de datos -------------------------------------------------------
builder.Services.AddDbContext<MonitorDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Monitor") ?? "Data Source=monitor.db"));

// --- Configuración -------------------------------------------------------
builder.Services.Configure<OpcionesMonitor>(builder.Configuration.GetSection(OpcionesMonitor.Seccion));
builder.Services.Configure<OpcionesTelegram>(builder.Configuration.GetSection(OpcionesTelegram.Seccion));

// --- Clientes HTTP para las comprobaciones -------------------------------
builder.Services.AddHttpClient(Comprobador.ClienteNormal, ConfigurarCliente);
builder.Services.AddHttpClient(Comprobador.ClienteSinValidarCertificado, ConfigurarCliente)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Solo para servicios marcados a mano, como Portainer con su certificado autofirmado.
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// --- Servicios de la aplicación ------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IComprobador, Comprobador>();
builder.Services.AddScoped<ServiciosConsulta>();
builder.Services.AddScoped<MonitorService>();
builder.Services.AddSingleton<IPublicadorTiempoReal, PublicadorSignalR>();
builder.Services.AddHostedService<MonitorWorker>();

// Avisos: el log siempre; Telegram solo si está configurado.
builder.Services.AddSingleton<INotificador, NotificadorLog>();
var telegram = builder.Configuration.GetSection(OpcionesTelegram.Seccion).Get<OpcionesTelegram>();
if (telegram?.Configurado == true)
{
    builder.Services.AddHttpClient<INotificador, NotificadorTelegram>();
}

// --- API -----------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// En desarrollo Angular corre en el puerto 4200.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Crea la BD y carga los servicios del servidor la primera vez.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MonitorDbContext>();
    db.Database.EnsureCreated();
    DatosIniciales.Cargar(db);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

app.MapOpenApi();
app.MapScalarApiReference(); // Documentación interactiva en /scalar

app.MapControllers();
app.MapHub<EstadoHub>("/hubs/estado");
app.MapGet("/api/health", () => Results.Ok(new { estado = "ok" }));

app.Run();

static void ConfigurarCliente(HttpClient cliente)
{
    cliente.Timeout = TimeSpan.FromSeconds(60);
    cliente.DefaultRequestHeaders.UserAgent.ParseAdd("HomelabMonitor/1.0");
}

// Necesario para que WebApplicationFactory encuentre el punto de entrada en los tests.
public partial class Program { }
