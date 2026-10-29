using HomelabMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HomelabMonitor.Api.Data;

public class MonitorDbContext(DbContextOptions<MonitorDbContext> options) : DbContext(options)
{
    public DbSet<Servicio> Servicios => Set<Servicio>();

    public DbSet<Comprobacion> Comprobaciones => Set<Comprobacion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite no guarda la zona horaria: todas las fechas se guardan en UTC
        // y al leerlas las marcamos como UTC para que el JSON lleve la "Z" final.
        configurationBuilder.Properties<DateTime>().HaveConversion<ConversorUtc>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Servicio>(e =>
        {
            e.Property(s => s.Nombre).HasMaxLength(100).IsRequired();
            e.Property(s => s.Destino).HasMaxLength(500).IsRequired();
            e.HasIndex(s => s.Nombre).IsUnique();

            // Guardamos los enums como texto para que la BD sea legible.
            e.Property(s => s.Tipo).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.EstadoActual).HasConversion<string>().HasMaxLength(15);

            e.HasMany(s => s.Comprobaciones)
                .WithOne(c => c.Servicio)
                .HasForeignKey(c => c.ServicioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comprobacion>(e =>
        {
            e.Property(c => c.Error).HasMaxLength(500);
            // Casi todas las consultas son "comprobaciones de un servicio en un rango de fechas".
            e.HasIndex(c => new { c.ServicioId, c.Fecha });
        });
    }
}

internal class ConversorUtc() : ValueConverter<DateTime, DateTime>(
    alGuardar => alGuardar.ToUniversalTime(),
    alLeer => DateTime.SpecifyKind(alLeer, DateTimeKind.Utc));
