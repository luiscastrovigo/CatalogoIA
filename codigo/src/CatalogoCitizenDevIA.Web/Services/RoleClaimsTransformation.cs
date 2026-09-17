using System.Linq;
using System.Security.Claims;
using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;

namespace CatalogoCitizenDevIA.Web.Services;

/// <summary>
/// Se ejecuta en CADA request autenticado (no solo en el login) y hace dos
/// cosas — ver 07-arquitectura-despliegue-componentes.md:
///
/// 1. Alta automática: si el AzureAdObjectId del token OIDC no existe todavía
///    en dbo.Usuario, lo crea (EsAdministrador = false). Si existe un registro
///    con el mismo correo pero sin AzureAdObjectId vinculado todavía (una
///    invitación precargada desde "Gestión de administradores" — ver
///    AdministradorController.Invitar), completa el vínculo sin tocar
///    EsAdministrador, para no perder el rol precargado ahí.
/// 2. Resolución de rol EN VIVO: agrega el claim interno EsAdministrador
///    leyendo dbo.Usuario en cada request — así un cambio de rol hecho por un
///    Administrador en "Gestión de administradores" aplica de inmediato, sin
///    pedir reingresar. El rol NUNCA sale de Azure AD/grupos de AD (decisión
///    confirmada, 05-documento-cero-prerequisitos.md sección 3).
///
/// Antes de la integración real esta clase solo leía el Object ID ya resuelto
/// por el login de desarrollo (RoleResolutionService hacía el alta). Ahora que
/// la autenticación es Microsoft.Identity.Web/Entra ID, esta clase es el único
/// punto de enganche post-login — RoleResolutionService queda retirado.
/// </summary>
public class RoleClaimsTransformation : IClaimsTransformation
{
    private readonly IUsuarioRepository _usuarios;

    public RoleClaimsTransformation(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true } || principal.Identity is not ClaimsIdentity)
        {
            return Task.FromResult(principal);
        }

        if (!Guid.TryParse(principal.GetObjectId(), out var azureAdObjectId))
        {
            // Principal autenticado pero sin un Object ID de Azure AD válido
            // (por ejemplo, un esquema de autenticación distinto) — no hay
            // como resolver el usuario ni el rol en este caso.
            return Task.FromResult(principal);
        }

        var correo = principal.FindFirstValue(ClaimTypes.Upn)
                     ?? principal.FindFirstValue("preferred_username")
                     ?? principal.FindFirstValue(ClaimTypes.Email)
                     ?? string.Empty;
        var nombre = ResolverNombre(principal, correo);

        var usuario = _usuarios.ObtenerPorAzureAdObjectId(azureAdObjectId);

        if (usuario is null && !string.IsNullOrWhiteSpace(correo))
        {
            usuario = _usuarios.ObtenerPorCorreo(correo);
        }

        if (usuario is null)
        {
            usuario = _usuarios.Crear(new Usuario
            {
                AzureAdObjectId = azureAdObjectId,
                CorreoCorporativo = correo,
                NombreCompleto = nombre,
                EsAdministrador = false
            });
        }
        else
        {
            if (usuario.AzureAdObjectId != azureAdObjectId)
            {
                usuario.AzureAdObjectId = azureAdObjectId;
            }

            // Retroalimentacion de usuarios, 2026-09-08: antes esto solo se
            // corregia si el nombre estaba en blanco o era el placeholder de
            // invitacion — un usuario cuya "alta automatica" (primer ingreso
            // sin invitacion previa) hubiera guardado el correo como nombre
            // (por no traer el claim "name" ese dia) se quedaba asi para
            // siempre. Ahora se reintenta en CADA login mientras el nombre
            // guardado siga viendose como un correo o sea el placeholder, asi
            // se autocorrige solo apenas el token traiga (o ResolverNombre
            // pueda derivar) algo mejor que el correo.
            if (EsNombrePendienteOCorreo(usuario.NombreCompleto) && !EsNombrePendienteOCorreo(nombre))
            {
                usuario.NombreCompleto = nombre;
            }
        }

        usuario.FechaUltimoAcceso = DateTime.UtcNow;
        _usuarios.Actualizar(usuario);

        var identidadBase = new ClaimsIdentity(
            principal.Claims.Where(c =>
                c.Type != AppClaimTypes.EsAdministrador &&
                c.Type != AppClaimTypes.EsAprobador &&
                c.Type != AppClaimTypes.UsuarioId),
            principal.Identity.AuthenticationType);

        identidadBase.AddClaim(new Claim(AppClaimTypes.UsuarioId, usuario.UsuarioId.ToString()));
        identidadBase.AddClaim(new Claim(AppClaimTypes.EsAdministrador, usuario.EsAdministrador.ToString()));
        identidadBase.AddClaim(new Claim(AppClaimTypes.EsAprobador, usuario.EsAprobador.ToString()));

        return Task.FromResult(new ClaimsPrincipal(identidadBase));
    }

    /// <summary>
    /// True si "nombre" no sirve como nombre para mostrar: vacío, el
    /// placeholder de invitación, o un correo (contiene "@") — este último es
    /// el caso reportado en producción (2026-09-08): el Dueño de una solución
    /// se mostraba con el correo en vez del nombre para usuarios cuyo primer
    /// login no trajo el claim "name".
    /// </summary>
    private static bool EsNombrePendienteOCorreo(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ||
        nombre == "(Pendiente de primer ingreso)" ||
        nombre.Contains('@');

    /// <summary>
    /// Resuelve el nombre para mostrar del usuario a partir del token OIDC,
    /// con una cadena de respaldo (retroalimentacion de usuarios, 2026-09-08)
    /// porque el claim "name" no llega para todas las cuentas de Azure AD:
    /// 1. Claim "name" (caso normal).
    /// 2. Nombre + apellido (given_name/family_name) si "name" no vino pero
    ///    esos sí.
    /// 3. Como último recurso, un nombre "legible" derivado de la parte
    ///    local del correo (ej. "Francisco.Frez@yanbal.com" -> "Francisco
    ///    Frez"), en vez de mostrar el correo completo tal cual en el
    ///    catálogo (columna "Dueño").
    /// </summary>
    private static string ResolverNombre(ClaimsPrincipal principal, string correo)
    {
        var nombreClaim = principal.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(nombreClaim) && !nombreClaim.Contains('@'))
        {
            return nombreClaim;
        }

        var nombres = principal.FindFirstValue(ClaimTypes.GivenName);
        var apellidos = principal.FindFirstValue(ClaimTypes.Surname);
        if (!string.IsNullOrWhiteSpace(nombres) || !string.IsNullOrWhiteSpace(apellidos))
        {
            return $"{nombres} {apellidos}".Trim();
        }

        return EmbellecerCorreo(correo);
    }

    private static string EmbellecerCorreo(string correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return correo;

        var parteLocal = correo.Split('@')[0];
        var partes = parteLocal.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..]);

        var resultado = string.Join(' ', partes);
        return string.IsNullOrWhiteSpace(resultado) ? correo : resultado;
    }
}
