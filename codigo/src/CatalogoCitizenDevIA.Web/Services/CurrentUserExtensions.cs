using System.Security.Claims;

namespace CatalogoCitizenDevIA.Web.Services;

public static class CurrentUserExtensions
{
    public static int GetUsuarioId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(AppClaimTypes.UsuarioId);
        return int.TryParse(raw, out var id) ? id : 0;
    }

    public static bool GetEsAdministrador(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(AppClaimTypes.EsAdministrador);
        return bool.TryParse(raw, out var esAdmin) && esAdmin;
    }

    public static bool GetEsAprobador(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(AppClaimTypes.EsAprobador);
        return bool.TryParse(raw, out var esAprobador) && esAprobador;
    }

    public static string? GetCorreo(this ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.Email);

    public static string? GetNombre(this ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.Name);
}
