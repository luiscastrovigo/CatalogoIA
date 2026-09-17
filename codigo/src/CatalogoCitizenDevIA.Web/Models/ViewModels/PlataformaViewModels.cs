using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Models.ViewModels;

/// <summary>
/// Input del formulario "Editar" de Plataforma. Trae PlataformaId + Nombre +
/// Categoria (ampliado 2026-09-08 a pedido de usuarios: Categoria también
/// debe poder editarse desde aquí). A propósito NO trae Estado/Comentario/
/// Activo, que se siguen editando desde Index (Estado) o al crear — así se
/// evita sobre-postear esos campos desde esta pantalla.
/// </summary>
public class EditarPlataformaInput
{
    public int PlataformaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
}

/// <summary>
/// Input del formulario "Nueva plataforma" (auditoría de seguridad, 2026-09-10):
/// antes PlataformaController.Create bindeaba directamente la entidad
/// <c>Plataforma</c> desde el POST (sobre-posteo / mass assignment) — aunque
/// Create ya solo es accesible para Administrador (no es una escalada de
/// privilegio hoy), un POST armado a mano contra ese action podía fijar
/// PlataformaId, Activo, CreadoPorId o FechaCreacion aunque el formulario no
/// los expusiera, porque el model binder de MVC lee cualquier campo del
/// formulario que calce con una propiedad pública de la entidad, no solo los
/// que la vista realmente pinta. Este DTO solo trae los 4 campos que la
/// pantalla "Nueva plataforma" realmente pide — Activo/CreadoPorId/
/// FechaCreacion se siguen fijando en el controlador, nunca desde el POST.
/// </summary>
public class CrearPlataformaInput
{
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string Estado { get; set; } = Plataforma.EstadoEnEvaluacion;
    public string? Comentario { get; set; }
}
