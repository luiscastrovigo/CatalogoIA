using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using CatalogoCitizenDevIA.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogoCitizenDevIA.Web.Controllers;

/// <summary>
/// Módulo "Revisión y aprobación" — 03-alcance-funcional.md. Abierto a
/// Administrador o Aprobador (punto 6 de retroalimentacion de usuarios,
/// 2026-09) via la policy "PuedeAprobar" (Program.cs).
/// </summary>
[Authorize(Policy = "PuedeAprobar")]
public class RevisionController : Controller
{
    private readonly ISolucionRepository _soluciones;
    private readonly SolucionService _solucionService;

    public RevisionController(ISolucionRepository soluciones, SolucionService solucionService)
    {
        _soluciones = soluciones;
        _solucionService = solucionService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var pendientes = _soluciones.ObtenerTodas()
            .Where(s => s.Estado is "Registrado" or "En revisión")
            .OrderBy(s => s.FechaRegistro)
            .ToList();

        return View(pendientes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CambiarEstado(int solucionId, string nuevoEstado, string revisadoPor, string? comentario)
    {
        if (!Solucion.EstadosValidos.Contains(nuevoEstado) || !Solucion.RevisadoPorValidos.Contains(revisadoPor))
        {
            return BadRequest("Estado o revisor no válido.");
        }

        _solucionService.CambiarEstado(solucionId, nuevoEstado, revisadoPor, User.GetUsuarioId(), comentario);
        TempData["Mensaje"] = "Estado actualizado.";
        return RedirectToAction(nameof(Index));
    }
}
