using HomelabMonitor.Api.Models;
using HomelabMonitor.Api.Services;

namespace HomelabMonitor.Tests;

public class CalculadoraEstadoTests
{
    [Fact]
    public void Un_acierto_marca_el_servicio_como_activo()
    {
        var estado = CalculadoraEstado.Calcular(EstadoServicio.Caido, [true, false, false], fallosParaCaido: 2);

        Assert.Equal(EstadoServicio.Activo, estado);
    }

    [Fact]
    public void Un_solo_fallo_no_lo_tira_si_hacen_falta_dos()
    {
        var estado = CalculadoraEstado.Calcular(EstadoServicio.Activo, [false, true], fallosParaCaido: 2);

        Assert.Equal(EstadoServicio.Activo, estado);
    }

    [Fact]
    public void Dos_fallos_seguidos_lo_marcan_como_caido()
    {
        var estado = CalculadoraEstado.Calcular(EstadoServicio.Activo, [false, false, true], fallosParaCaido: 2);

        Assert.Equal(EstadoServicio.Caido, estado);
    }

    [Fact]
    public void Con_umbral_uno_cae_al_primer_fallo()
    {
        var estado = CalculadoraEstado.Calcular(EstadoServicio.Activo, [false], fallosParaCaido: 1);

        Assert.Equal(EstadoServicio.Caido, estado);
    }

    [Fact]
    public void Primer_fallo_de_un_servicio_nuevo_sigue_desconocido()
    {
        var estado = CalculadoraEstado.Calcular(EstadoServicio.Desconocido, [false], fallosParaCaido: 2);

        Assert.Equal(EstadoServicio.Desconocido, estado);
    }

    [Fact]
    public void Sin_resultados_no_cambia_nada()
    {
        var estado = CalculadoraEstado.Calcular(EstadoServicio.Caido, [], fallosParaCaido: 2);

        Assert.Equal(EstadoServicio.Caido, estado);
    }

    [Fact]
    public void Umbral_menor_que_uno_es_un_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CalculadoraEstado.Calcular(EstadoServicio.Activo, [false], fallosParaCaido: 0));
    }
}

public class CalculadoraEstadisticasTests
{
    [Theory]
    [InlineData(100, 100, 100.0)]
    [InlineData(4, 3, 75.0)]
    [InlineData(3, 2, 66.67)]
    [InlineData(10, 0, 0.0)]
    public void Calcula_el_porcentaje_redondeado(int total, int ok, double esperado)
    {
        Assert.Equal((double?)esperado, CalculadoraEstadisticas.Uptime(total, ok));
    }

    [Fact]
    public void Sin_comprobaciones_no_hay_uptime()
    {
        Assert.Null(CalculadoraEstadisticas.Uptime(0, 0));
    }

    [Fact]
    public void Mas_aciertos_que_total_es_un_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculadoraEstadisticas.Uptime(2, 3));
    }
}

public class DestinoParserTests
{
    [Theory]
    [InlineData("192.168.1.138:22", "192.168.1.138", 22)]
    [InlineData("pc-de-bill:8096", "pc-de-bill", 8096)]
    [InlineData("nube.kaitzlab.com:443", "nube.kaitzlab.com", 443)]
    public void Lee_destinos_tcp_validos(string destino, string host, int puerto)
    {
        Assert.True(DestinoParser.TryParseTcp(destino, out var h, out var p));
        Assert.Equal(host, h);
        Assert.Equal(puerto, p);
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.138")]
    [InlineData(":22")]
    [InlineData("servidor:")]
    [InlineData("servidor:abc")]
    [InlineData("servidor:70000")]
    [InlineData("mi servidor:22")]
    public void Rechaza_destinos_tcp_no_validos(string destino)
    {
        Assert.False(DestinoParser.TryParseTcp(destino, out _, out _));
    }

    [Theory]
    [InlineData("https://nube.kaitzlab.com", true)]
    [InlineData("http://192.168.1.138:8096/health", true)]
    [InlineData("ftp://servidor", false)]
    [InlineData("nube.kaitzlab.com", false)]
    public void Valida_urls_http(string destino, bool esperado)
    {
        Assert.Equal(esperado, DestinoParser.EsUrlHttpValida(destino));
    }
}
