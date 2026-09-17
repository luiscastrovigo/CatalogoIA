using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using CatalogoCitizenDevIA.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CatalogoCitizenDevIA.Web.Controllers;

/// <summary>
/// Módulo "Gestión de Usuarios" (antes "Gestión de administradores" —
/// renombrado en el punto 6 de retroalimentacion de usuarios, 2026-09, al
/// agregar el rol Aprobador junto al de Administrador). Reemplaza la
/// dependencia de grupos de Azure AD: el control de quién administra el
/// catálogo y quién aprueba cambios de estado vive aquí — ver
/// 05-documento-cero-prerequisitos.md sección 3 y
/// 07-arquitectura-despliegue-componentes.md. Solo un Administrador puede
/// entrar a este módulo (otorgar el rol Aprobador es en si un privilegio
/// administrativo).
/// </summary>
[Authorize(Policy = "EsAdministrador")]
public class UsuarioController : Controller
{
    private readonly IUsuarioRepository _usuarios;

    public UsuarioController(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    [HttpGet]
    public IActionResult Index() => View(_usuarios.ObtenerTodos());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleAdmin(int usuarioId)
    {
        var usuario = _usuarios.ObtenerPorId(usuarioId);
        if (usuario is null) return NotFound();

        var esElUnicoAdminActivo = usuario.EsAdministrador &&
            _usuarios.ObtenerTodos().Count(u => u.EsAdministrador) == 1;

        if (esElUnicoAdminActivo)
        {
            TempData["Error"] = "No puedes retirar el rol de Administrador: es el único activo. Otorga el rol a otra persona primero.";
            return RedirectToAction(nameof(Index));
        }

        usuario.EsAdministrador = !usuario.EsAdministrador;
        _usuarios.Actualizar(usuario);

        TempData["Mensaje"] = usuario.EsAdministrador
            ? $"{usuario.NombreCompleto} ahora es Administrador."
            : $"Se retiró el rol de Administrador a {usuario.NombreCompleto}.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Alterna el rol Aprobador. A diferencia de ToggleAdmin, no hay resguardo
    /// de "único activo": Aprobador no es indispensable para operar el módulo
    /// de Gestión de Usuarios (eso requiere Administrador), así que puede
    /// quedar en cero sin bloquear a nadie.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleAprobador(int usuarioId)
    {
        var usuario = _usuarios.ObtenerPorId(usuarioId);
        if (usuario is null) return NotFound();

        usuario.EsAprobador = !usuario.EsAprobador;
        _usuarios.Actualizar(usuario);

        TempData["Mensaje"] = usuario.EsAprobador
            ? $"{usuario.NombreCompleto} ahora es Aprobador."
            : $"Se retiró el rol de Aprobador a {usuario.NombreCompleto}.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Invita a un Administrador y/o Aprobador nuevo por correo, ANTES de que esa
    /// persona haya iniciado sesión alguna vez en la App. Sin esto, "Gestión de
    /// Usuarios" solo puede alternar el rol de gente que ya aparece en
    /// dbo.Usuario (es decir, que ya inició sesión al menos una vez) — lo cual
    /// bloquea agregar al primer admin/aprobador de un área nueva. El registro
    /// se precrea con un AzureAdObjectId PLACEHOLDER ALEATORIO (Guid.NewGuid(),
    /// nunca Guid.Empty — dbo.Usuario tiene UQ_Usuario_AzureAdObjectId, así que
    /// dos invitaciones pendientes no pueden compartir el mismo valor o el
    /// segundo INSERT viola la restricción única). RoleClaimsTransformation
    /// (Entra ID real) completa el vínculo con el Object ID real automáticamente
    /// la primera vez que esa persona inicia sesión, sin perder los roles
    /// precargados aquí — no importa que el placeholder original haya sido
    /// aleatorio, la lógica ahí solo compara si el AzureAdObjectId guardado
    /// difiere del real y lo sobreescribe.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    // Limite de tasa (auditoria de seguridad, hallazgo H-11): 30 envios por
    // minuto por usuario. Muy por encima de cualquier uso humano normal.
    [EnableRateLimiting("OperacionesSensibles")]
    public IActionResult Invitar(string correoCorporativo, bool comoAdministrador, bool comoAprobador)
    {
        correoCorporativo = correoCorporativo?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(correoCorporativo) || !correoCorporativo.Contains('@'))
        {
            TempData["Error"] = "Ingresa un correo corporativo válido para invitar.";
            return RedirectToAction(nameof(Index));
        }

        if (!DominiosCorporativos.EsCorporativo(correoCorporativo))
        {
            TempData["Error"] = $"Solo se puede invitar con un correo corporativo ({DominiosCorporativos.ListaLegible}).";
            return RedirectToAction(nameof(Index));
        }

        if (!comoAdministrador && !comoAprobador)
        {
            TempData["Error"] = "Selecciona al menos un rol (Administrador y/o Aprobador) para invitar.";
            return RedirectToAction(nameof(Index));
        }

        var existente = _usuarios.ObtenerPorCorreo(correoCorporativo);

        if (existente is not null)
        {
            var cambios = new List<string>();
            if (comoAdministrador && !existente.EsAdministrador) { existente.EsAdministrador = true; cambios.Add("Administrador"); }
            if (comoAprobador && !existente.EsAprobador) { existente.EsAprobador = true; cambios.Add("Aprobador"); }

            if (cambios.Count == 0)
            {
                TempData["Error"] = $"{existente.CorreoCorporativo} ya tiene los roles seleccionados.";
            }
            else
            {
                _usuarios.Actualizar(existente);
                TempData["Mensaje"] = $"{existente.CorreoCorporativo} ahora es {string.Join(" y ", cambios)}.";
            }

            return RedirectToAction(nameof(Index));
        }

        _usuarios.Crear(new Usuario
        {
            AzureAdObjectId = Guid.NewGuid(),
            CorreoCorporativo = correoCorporativo,
            NombreCompleto = "(Pendiente de primer ingreso)",
            EsAdministrador = comoAdministrador,
            EsAprobador = comoAprobador
        });

        var roles = string.Join(" y ", new[] { comoAdministrador ? "Administrador" : null, comoAprobador ? "Aprobador" : null }.Where(r => r is not null));
        TempData["Mensaje"] = $"Se invitó a {correoCorporativo} como {roles} — el rol se activa apenas esa persona inicie sesión por primera vez en la App.";
        return RedirectToAction(nameof(Index));
    }
}
