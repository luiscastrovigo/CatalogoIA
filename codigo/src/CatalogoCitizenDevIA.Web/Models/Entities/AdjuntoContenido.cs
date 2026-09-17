namespace CatalogoCitizenDevIA.Web.Models.Entities;

/// <summary>
/// Contenido binario de un Adjunto, en una tabla 1 a 1 aparte y a proposito.
///
/// Por decision del Arquitecto de Aplicacion (2026-09-10) los archivos se guardan
/// dentro de SQL Server y no en el sistema de archivos. Separar el binario de los
/// metadatos evita el problema clasico de esa decision: si el VARBINARY(MAX)
/// viviera en dbo.Adjunto, cualquier consulta que solo quiera listar los adjuntos
/// de una solucion (nombre, tipo, tamano, quien lo subio) arrastraria megabytes de
/// contenido sin necesitarlos. Con esta separacion, el binario solo se lee cuando
/// alguien descarga el archivo. Ver sql/migracion_adjuntos.sql.
/// </summary>
public class AdjuntoContenido
{
    public int AdjuntoId { get; set; }
    public byte[] Contenido { get; set; } = Array.Empty<byte>();
}
