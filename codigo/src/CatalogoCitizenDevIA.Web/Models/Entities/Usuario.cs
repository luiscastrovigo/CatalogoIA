namespace CatalogoCitizenDevIA.Web.Models.Entities;

/// <summary>
/// Cache local de identidades resueltas desde Azure AD. Ver 06-diseno-bd-sql-server.md
/// (tabla dbo.Usuario) y 07-arquitectura-despliegue-componentes.md para la resolucion
/// de rol: Azure AD solo autentica (identidad); EsAdministrador vive aqui, en la App.
/// </summary>
public class Usuario
{
    public int UsuarioId { get; set; }
    public Guid AzureAdObjectId { get; set; }
    public string CorreoCorporativo { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? Pais { get; set; }
    public bool EsAdministrador { get; set; }

    /// <summary>
    /// Rol "Aprobador" (punto 6 de retroalimentacion de usuarios, 2026-09): acceso
    /// a Revision y aprobación / cambios de estado, sin los demas privilegios de
    /// Administrador (Plataformas, Dashboard ejecutivo, Gestión de Usuarios).
    /// </summary>
    public bool EsAprobador { get; set; }

    public DateTime? FechaUltimoAcceso { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
