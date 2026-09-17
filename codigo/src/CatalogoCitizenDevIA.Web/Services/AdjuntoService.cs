using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Services;

/// <summary>Resultado de validar un archivo subido por un usuario.</summary>
public sealed class ResultadoAdjunto
{
    public bool EsValido { get; init; }
    public string Error { get; init; } = string.Empty;
    public byte[] Contenido { get; init; } = Array.Empty<byte>();
    public string NombreArchivo { get; init; } = string.Empty;
    public string TipoMime { get; init; } = string.Empty;

    public static ResultadoAdjunto Invalido(string error) => new() { EsValido = false, Error = error };
}

/// <summary>
/// Validacion de archivos adjuntos. Subir archivos es la superficie de ataque mas
/// peligrosa que se le puede agregar a una App web, asi que todo lo que entra pasa
/// por aqui. Controles aplicados, en orden:
///
/// 1. Tamano maximo por archivo, ademas del limite de la peticion en el controlador.
/// 2. Nombre saneado con Path.GetFileName, que descarta cualquier componente de
///    directorio: es la defensa contra rutas del tipo "..\\..\\web.config". El nombre
///    solo se usa para mostrar y para el encabezado de descarga, nunca para construir
///    una ruta en disco.
/// 3. Lista BLANCA de extensiones. Se eligio lista blanca y no lista negra a
///    proposito: con lista negra siempre queda una extension peligrosa afuera.
///    Quedan excluidos ejecutables, y tambien .html y .svg, que son ejecutables de
///    facto dentro de un navegador.
/// 4. Verificacion de la firma binaria del archivo contra la extension declarada:
///    renombrar un .exe a .pdf no alcanza para pasar.
///
/// El quinto control no vive aqui sino en la descarga: el contenido siempre se
/// devuelve como application/octet-stream y con Content-Disposition attachment, de
/// modo que nada de lo subido pueda ejecutarse dentro del dominio de la App.
/// </summary>
public class AdjuntoService
{
    public const int TamanoMaximoBytes = 10 * 1024 * 1024;
    public const int MaximoPorSolucion = 5;

    private static readonly Dictionary<string, string> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };

    /// <summary>Para el texto de ayuda de la vista.</summary>
    public static string ExtensionesPermitidas => string.Join(" ", TiposPermitidos.Keys);

    /// <summary>Para el atributo accept del input de archivo.</summary>
    public static string AceptaHtml => string.Join(",", TiposPermitidos.Keys);

    public static int TamanoMaximoMb => TamanoMaximoBytes / (1024 * 1024);

    public ResultadoAdjunto Validar(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return ResultadoAdjunto.Invalido("No se recibió ningún archivo.");
        }

        if (archivo.Length > TamanoMaximoBytes)
        {
            return ResultadoAdjunto.Invalido($"El archivo supera el máximo permitido de {TamanoMaximoMb} MB.");
        }

        var nombre = Path.GetFileName(archivo.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return ResultadoAdjunto.Invalido("El archivo no tiene un nombre válido.");
        }

        if (nombre.Length > 300)
        {
            nombre = nombre.Substring(nombre.Length - 300);
        }

        var extension = Path.GetExtension(nombre);
        if (string.IsNullOrWhiteSpace(extension) || !TiposPermitidos.TryGetValue(extension, out var tipoMime))
        {
            return ResultadoAdjunto.Invalido(
                $"Tipo de archivo no permitido. Se aceptan únicamente: {ExtensionesPermitidas}");
        }

        byte[] contenido;
        using (var memoria = new MemoryStream())
        using (var origen = archivo.OpenReadStream())
        {
            origen.CopyTo(memoria);
            contenido = memoria.ToArray();
        }

        if (!FirmaCoincideConExtension(contenido, extension))
        {
            return ResultadoAdjunto.Invalido(
                "El contenido del archivo no corresponde a su extensión. No se guardó por seguridad.");
        }

        return new ResultadoAdjunto
        {
            EsValido = true,
            Contenido = contenido,
            NombreArchivo = nombre,
            TipoMime = tipoMime
        };
    }

    /// <summary>Normaliza el tipo elegido contra la lista valida, que ademas tiene un CHECK en la base.</summary>
    public static string TipoAdjuntoValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Adjunto.TiposValidos.Contains(tipo) ? tipo : "Otro documento";

    private static bool FirmaCoincideConExtension(byte[] datos, string extension)
    {
        if (datos.Length < 8)
        {
            return false;
        }

        bool Empieza(params byte[] firma)
        {
            if (datos.Length < firma.Length)
            {
                return false;
            }

            for (var i = 0; i < firma.Length; i++)
            {
                if (datos[i] != firma[i])
                {
                    return false;
                }
            }

            return true;
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => Empieza(0x25, 0x50, 0x44, 0x46),
            ".png" => Empieza(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".jpg" or ".jpeg" => Empieza(0xFF, 0xD8, 0xFF),
            // Office moderno (OOXML) es un ZIP; Office legado usa el contenedor OLE2.
            ".docx" or ".xlsx" or ".pptx" => Empieza(0x50, 0x4B, 0x03, 0x04),
            ".doc" or ".xls" or ".ppt" => Empieza(0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1),
            _ => false
        };
    }
}
