namespace CatalogoCitizenDevIA.Web.Models.Entities;

/// <summary>
/// Maestro editable de plataformas de IA aprobadas (reemplaza la hoja
/// "Catalogo de plataformas" del Excel). Ver 06-diseno-bd-sql-server.md.
/// Por decision del solicitante, este maestro puede arrancar vacio y
/// completarse progresivamente (05-documento-cero-prerequisitos.md, seccion 4).
/// </summary>
public class Plataforma
{
    public const string EstadoAprobada = "Aprobada";
    public const string EstadoEnEvaluacion = "En evaluación";
    public const string EstadoDescontinuada = "Descontinuada";

    public static readonly string[] EstadosValidos = { EstadoAprobada, EstadoEnEvaluacion, EstadoDescontinuada };

    public int PlataformaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string Estado { get; set; } = EstadoEnEvaluacion;
    public string? Comentario { get; set; }
    public bool Activo { get; set; } = true;
    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
