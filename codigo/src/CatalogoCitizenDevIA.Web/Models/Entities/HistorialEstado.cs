namespace CatalogoCitizenDevIA.Web.Models.Entities;

/// <summary>Auditoria de cambios de estado (no existe en el Excel). Ver 06-diseno-bd-sql-server.md.</summary>
public class HistorialEstado
{
    public int HistorialEstadoId { get; set; }
    public int SolucionId { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;
    public string? Comentario { get; set; }
}
