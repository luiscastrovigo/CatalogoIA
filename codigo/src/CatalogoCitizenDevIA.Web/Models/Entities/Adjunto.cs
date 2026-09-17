namespace CatalogoCitizenDevIA.Web.Models.Entities;

/// <summary>Evidencia asociada a una solucion (no existe en el Excel). Ver 06-diseno-bd-sql-server.md.</summary>
public class Adjunto
{
    public static readonly string[] TiposValidos = { "Captura de pantalla", "Acta de comité", "Otro documento" };

    public int AdjuntoId { get; set; }
    public int SolucionId { get; set; }

    /// <summary>Nombre original con el que el usuario subio el archivo. Solo para mostrar y descargar.</summary>
    public string NombreArchivo { get; set; } = string.Empty;

    public string TipoAdjunto { get; set; } = string.Empty;

    /// <summary>
    /// En desuso desde 2026-09-10. El diseno original guardaba el archivo en disco y
    /// solo la ruta en esta columna; ahora el contenido vive en AdjuntoContenido,
    /// dentro de la base. La columna se conserva (ya anulable) para no perder la
    /// trazabilidad del diseno aprobado en 06-diseno-bd-sql-server.md.
    /// </summary>
    public string? RutaAlmacenamiento { get; set; }

    public string TipoMime { get; set; } = string.Empty;
    public int TamanoBytes { get; set; }

    public int SubidoPorId { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    /// <summary>Tamano legible para la interfaz, sin logica de presentacion en la vista.</summary>
    public string TamanoLegible =>
        TamanoBytes >= 1024 * 1024
            ? $"{TamanoBytes / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, TamanoBytes / 1024)} KB";
}
