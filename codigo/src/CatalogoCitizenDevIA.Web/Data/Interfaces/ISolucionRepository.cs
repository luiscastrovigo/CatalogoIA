using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Data.Interfaces;

public interface ISolucionRepository
{
    IReadOnlyList<Solucion> ObtenerTodas();
    Solucion? ObtenerPorId(int solucionId);
    Solucion Crear(Solucion solucion);
    void Actualizar(Solucion solucion);

    /// <summary>
    /// Elimina la Solucion y todos sus registros dependientes (HistorialEstado,
    /// NotificacionEnviada, Adjunto). Necesario porque las FK hacia Solucion usan
    /// DeleteBehavior.Restrict (06-diseno-bd-sql-server.md) — un DELETE directo
    /// sobre dbo.Solucion falla si quedan hijos, así que el repositorio real
    /// borra los hijos primero, dentro de una transacción. Solo Administradores
    /// pueden invocar esto (punto 4 de retroalimentacion de usuarios, 2026-09) —
    /// la restricción de autorización vive en SolucionController.
    /// </summary>
    void Eliminar(int solucionId);

    /// <summary>Replica sp_GenerarCodigoSolucion: correlativo CD-AAAA-### por bloqueo, sin duplicados.</summary>
    string GenerarCodigoSolucion(int anio);

    void AgregarHistorial(HistorialEstado historial);
    IReadOnlyList<HistorialEstado> ObtenerHistorial(int solucionId);

    /// <summary>
    /// Guarda el metadato y el contenido del adjunto de forma atomica. El binario va
    /// a AdjuntoContenido, no a Adjunto, para que listar adjuntos nunca arrastre
    /// megabytes — ver Models/Entities/AdjuntoContenido.cs.
    /// </summary>
    void AgregarAdjunto(Adjunto adjunto, byte[] contenido);

    /// <summary>Metadatos de los adjuntos de una solucion, SIN el contenido binario.</summary>
    IReadOnlyList<Adjunto> ObtenerAdjuntos(int solucionId);

    Adjunto? ObtenerAdjunto(int adjuntoId);

    /// <summary>Contenido binario; se lee solo al descargar.</summary>
    byte[]? ObtenerContenidoAdjunto(int adjuntoId);

    void EliminarAdjunto(int adjuntoId);

    void RegistrarNotificacion(NotificacionEnviada notificacion);
    IReadOnlyList<NotificacionEnviada> ObtenerNotificaciones(int solucionId);

    /// <summary>
    /// True si al menos una Solucion del catalogo usa esta Plataforma. Se usa para
    /// bloquear la eliminacion de una Plataforma desde PlataformaController --
    /// retroalimentacion de usuarios, 2026-09-08: no se debe poder eliminar una
    /// plataforma que ya tiene soluciones registradas con ella.
    /// </summary>
    bool ExistenSolucionesConPlataforma(int plataformaId);
}
