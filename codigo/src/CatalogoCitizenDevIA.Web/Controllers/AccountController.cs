using Microsoft.AspNetCore.Mvc;

namespace CatalogoCitizenDevIA.Web.Controllers;

/// <summary>
/// El login/logout real ahora lo maneja Microsoft.Identity.Web.UI (rutas
/// /MicrosoftIdentity/Account/SignIn y /MicrosoftIdentity/Account/SignOut,
/// registradas por AddMicrosoftIdentityUI() en Program.cs) — no hace falta
/// reimplementarlas aquí. Este controlador solo conserva la pantalla de
/// "Acceso denegado" (AccessDeniedPath de la política de cookies).
///
/// Las acciones Login/Logout que existían aquí (login de desarrollo, ver
/// 09-estado-implementacion-pruebas.md) se retiraron junto con
/// RoleResolutionService — su lógica de alta automática y resolución de rol
/// quedó fusionada en RoleClaimsTransformation, que ahora corre igual con el
/// login real de Entra ID.
/// </summary>
public class AccountController : Controller
{
    [HttpGet]
    public IActionResult AccesoDenegado() => View();
}
