using HomelabMonitor.Api.Dtos;
using HomelabMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomelabMonitor.Api.Controllers;

[ApiController]
[Route("api/resumen")]
[Produces("application/json")]
public class ResumenController(ServiciosConsulta consulta) : ControllerBase
{
    /// <summary>Totales para la cabecera del panel: activos, caídos y uptime global de 24 h.</summary>
    [HttpGet]
    public async Task<ActionResult<ResumenDto>> Obtener(CancellationToken ct) =>
        await consulta.ResumenAsync(ct);
}
