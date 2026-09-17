using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using CatalogoCitizenDevIA.Web.Models.ViewModels;
using CatalogoCitizenDevIA.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogoCitizenDevIA.Web.Controllers;

/// <summary>Módulo "Administración de plataformas" (solo Administrador) — 03-alcance-funcional.md.</summary>
[Authorize(Policy = "EsAdministrador")]
public class PlataformaController : Controller
{
    private readonly IPlataformaRepository _plataformas;
    private readonly ISolucionRepository _soluciones;

    public PlataformaController(IPlataformaRepository plataformas, ISolucionRepository soluciones)
    {
        _plataformas = plataformas;
        _soluciones = soluciones;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var plataformas = _plataformas.ObtenerTodas();

        // Retroalimentacion de usuarios, 2026-09-08: la vista deshabilita "Eliminar"
        // para las plataformas que ya tienen soluciones registradas en el catalogo
        // (el controlador vuelve a validar esto mismo en Eliminar() por si llega un
        // POST directo).
        ViewBag.PlataformasConSoluciones = plataformas
            .Where(p => _soluciones.ExistenSolucionesConPlataforma(p.PlataformaId))
            .Select(p => p.PlataformaId)
            .ToHashSet();

        return View(plataformas);
    }

    [HttpGet]
    public IActionResult Create() => View(new CrearPlataformaInput());

    /// <summary>
    /// Recibe <see cref="CrearPlataformaInput"/> en vez de la entidad
    /// <see cref="Plataforma"/> directamente (auditoría de seguridad,
    /// 2026-09-10 — ver el comentario en el DTO): así un POST armado a mano
    /// no puede fijar PlataformaId/Activo/CreadoPorId/FechaCreacion aunque el
    /// formulario no los exponga. Esos campos se siguen calculando aquí,
    /// igual que antes.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CrearPlataformaInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Nombre))
        {
            ModelState.AddModelError(nameof(CrearPlataformaInput.Nombre), "El nombre es obligatorio.");
        }

        if (!Plataforma.EstadosValidos.Contains(input.Estado))
        {
            ModelState.AddModelError(nameof(CrearPlataformaInput.Estado), "Estado no válido.");
        }

        if (!ModelState.IsValid) return View(input);

        var plataforma = new Plataforma
        {
            Nombre = input.Nombre.Trim(),
            Categoria = string.IsNullOrWhiteSpace(input.Categoria) ? null : input.Categoria.Trim(),
            Estado = input.Estado,
            Comentario = string.IsNullOrWhiteSpace(input.Comentario) ? null : input.Comentario.Trim(),
            Activo = input.Estado == Plataforma.EstadoAprobada,
            CreadoPorId = User.GetUsuarioId()
        };
        _plataformas.Crear(plataforma);

        TempData["Mensaje"] = $"Plataforma '{plataforma.Nombre}' creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Editar(int id)
    {
        var plataforma = _plataformas.ObtenerPorId(id);
        if (plataforma is null) return NotFound();

        return View(new EditarPlataformaInput
        {
            PlataformaId = plataforma.PlataformaId,
            Nombre = plataforma.Nombre,
            Categoria = plataforma.Categoria
        });
    }

    /// <summary>
    /// Permite cambiar Nombre y Categoria (Categoria ampliado 2026-09-08 a
    /// pedido de usuarios). Estado/Comentario no se tocan desde aqui, ni
    /// siquiera si alguien arma un POST a mano, porque EditarPlataformaInput
    /// no trae esos campos.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(EditarPlataformaInput input)
    {
        var plataforma = _plataformas.ObtenerPorId(input.PlataformaId);
        if (plataforma is null) return NotFound();

        if (string.IsNullOrWhiteSpace(input.Nombre))
        {
            ModelState.AddModelError(nameof(EditarPlataformaInput.Nombre), "El nombre es obligatorio.");
        }

        if (!ModelState.IsValid) return View(input);

        plataforma.Nombre = input.Nombre.Trim();
        plataforma.Categoria = string.IsNullOrWhiteSpace(input.Categoria) ? null : input.Categoria.Trim();
        _plataformas.Actualizar(plataforma);

        TempData["Mensaje"] = "Plataforma actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CambiarEstado(int id, string estado)
    {
        var plataforma = _plataformas.ObtenerPorId(id);
        if (plataforma is null) return NotFound();

        plataforma.Estado = estado;
        plataforma.Activo = estado == Plataforma.EstadoAprobada;
        _plataformas.Actualizar(plataforma);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Elimina una Plataforma -- exclusivo de Administrador (ya cubierto por el
    /// [Authorize] de clase) y SOLO si ningun registro del catalogo la usa
    /// (retroalimentacion de usuarios, 2026-09-08). Esta validacion se repite aqui
    /// aunque la vista ya deshabilite el boton, por si llega un POST directo.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Eliminar(int id)
    {
        var plataforma = _plataformas.ObtenerPorId(id);
        if (plataforma is null) return NotFound();

        if (_soluciones.ExistenSolucionesConPlataforma(id))
        {
            TempData["Mensaje"] = $"No se puede eliminar '{plataforma.Nombre}': ya existen soluciones registradas en el catálogo con esta plataforma.";
            return RedirectToAction(nameof(Index));
        }

        _plataformas.Eliminar(id);
        TempData["Mensaje"] = $"Plataforma '{plataforma.Nombre}' eliminada.";
        return RedirectToAction(nameof(Index));
    }
}
