using System.Diagnostics;
using CatalogoCitizenDevIA.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CatalogoCitizenDevIA.Web.Controllers;

/// <summary>
/// Unico proposito: la accion Error(), usada por el manejador global de
/// excepciones de Program.cs (app.UseExceptionHandler("/Home/Error")). Este
/// controlador no existia en el proyecto — el handler apuntaba a una ruta sin
/// resolver, asi que cualquier excepcion no controlada en produccion caia en una
/// pagina rota en vez de esta pantalla de error (retroalimentacion de usuarios,
/// 2026-09-07). No agregar mas acciones aqui: el resto de la navegacion vive en
/// SolucionController, RevisionController, etc.
/// </summary>
public class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
