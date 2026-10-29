using HomelabMonitor.Api.Models;

namespace HomelabMonitor.Api.Services;

/// <summary>
/// Decide el estado de un servicio a partir de sus últimas comprobaciones.
/// Un único fallo no lo marca como caído (puede ser un corte puntual de red):
/// hacen falta <c>fallosParaCaido</c> fallos seguidos. Un acierto lo marca como activo.
/// </summary>
public static class CalculadoraEstado
{
    /// <param name="estadoActual">Estado que tenía el servicio antes de esta comprobación.</param>
    /// <param name="ultimosResultados">Resultados más recientes primero, incluyendo la comprobación nueva.</param>
    /// <param name="fallosParaCaido">Fallos seguidos necesarios para considerarlo caído.</param>
    public static EstadoServicio Calcular(
        EstadoServicio estadoActual,
        IReadOnlyList<bool> ultimosResultados,
        int fallosParaCaido)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fallosParaCaido, 1);

        if (ultimosResultados.Count == 0)
        {
            return estadoActual;
        }

        if (ultimosResultados[0])
        {
            return EstadoServicio.Activo;
        }

        var fallosSeguidos = ultimosResultados.TakeWhile(ok => !ok).Count();

        return fallosSeguidos >= fallosParaCaido ? EstadoServicio.Caido : estadoActual;
    }
}
