namespace HomelabMonitor.Api.Services;

/// <summary>Validación y lectura de los destinos de los servicios.</summary>
public static class DestinoParser
{
    public static bool EsUrlHttpValida(string? destino) =>
        Uri.TryCreate(destino, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Lee un destino con formato host:puerto.</summary>
    public static bool TryParseTcp(string? destino, out string host, out int puerto)
    {
        host = "";
        puerto = 0;

        if (string.IsNullOrWhiteSpace(destino))
        {
            return false;
        }

        var separador = destino.LastIndexOf(':');
        if (separador <= 0 || separador == destino.Length - 1)
        {
            return false;
        }

        host = destino[..separador].Trim();
        var textoPuerto = destino[(separador + 1)..].Trim();

        return host.Length > 0
            && !host.Contains(' ')
            && int.TryParse(textoPuerto, out puerto)
            && puerto is > 0 and <= 65_535;
    }
}
