using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogoCitizenDevIA.Web.Controllers;

/// <summary>
/// "Dashboard ejecutivo (solo Administrador)" — 03-alcance-funcional.md,
/// módulo 6. Equivalente en vivo a la hoja "Resumen" del Excel
/// (01-diccionario-datos-excel.md), más cruces adicionales.
/// </summary>
[Authorize(Policy = "EsAdministrador")]
public class DashboardController : Controller
{
    private readonly ISolucionRepository _soluciones;
    private readonly IPlataformaRepository _plataformas;

    public DashboardController(ISolucionRepository soluciones, IPlataformaRepository plataformas)
    {
        _soluciones = soluciones;
        _plataformas = plataformas;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var todas = _soluciones.ObtenerTodas();
        var nombrePlataforma = _plataformas.ObtenerTodas().ToDictionary(p => p.PlataformaId, p => p.Nombre);
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var vm = new DashboardViewModel
        {
            TotalSoluciones = todas.Count,
            PorNivelRiesgo = todas.GroupBy(s => s.NivelRiesgo).ToDictionary(g => g.Key, g => g.Count()),
            PorEstado = todas.GroupBy(s => s.Estado).ToDictionary(g => g.Key, g => g.Count()),
            PorPais = todas.GroupBy(s => s.Pais).ToDictionary(g => g.Key, g => g.Count()),
            PorArea = todas.GroupBy(s => s.Area).ToDictionary(g => g.Key, g => g.Count()),
            PorTipo = todas.GroupBy(s => s.TipoSolucion).ToDictionary(g => g.Key, g => g.Count()),
            PorPlataforma = todas.GroupBy(s => nombrePlataforma.GetValueOrDefault(s.PlataformaId, "—"))
                                 .ToDictionary(g => g.Key, g => g.Count()),
            ProximasARevision30Dias = todas.Count(s => s.ProximaRevision.HasValue &&
                                                        s.ProximaRevision.Value <= hoy.AddDays(30))
        };

        return View(vm);
    }
}
