using System.Data;
using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CatalogoCitizenDevIA.Web.Data.EfCore;

/// <summary>
/// Implementacion real de ISolucionRepository contra dbo.Solucion (PERMS02),
/// vía EF Core. Reemplaza a InMemorySolucionRepository. Registrar como Scoped.
///
/// GenerarCodigoSolucion llama al procedimiento almacenado real
/// dbo.sp_GenerarCodigoSolucion (bloqueo UPDLOCK/HOLDLOCK sobre
/// dbo.SolucionContador, ver 06-diseno-bd-sql-server.md sección 3) en vez de
/// reimplementar el correlativo con un lock de C# — un lock de proceso .NET
/// NO es seguro contra duplicados cuando IIS corre varios workers o el sitio
/// se recicla, mientras que el bloqueo a nivel de fila en SQL Server sí lo es
/// entre cualquier número de instancias de la App.
/// </summary>
public class EfSolucionRepository : ISolucionRepository
{
    private readonly AppDbContext _db;

    public EfSolucionRepository(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Solucion> ObtenerTodas() =>
        _db.Soluciones.OrderByDescending(s => s.FechaRegistro).ToList();

    public Solucion? ObtenerPorId(int solucionId) =>
        _db.Soluciones.FirstOrDefault(s => s.SolucionId == solucionId);

    public Solucion Crear(Solucion solucion)
    {
        _db.Soluciones.Add(solucion);
        _db.SaveChanges();
        return solucion;
    }

    public void Actualizar(Solucion solucion)
    {
        solucion.FechaModificacion = DateTime.UtcNow;
        _db.Soluciones.Update(solucion);
        _db.SaveChanges();
    }

    /// <summary>
    /// Borra HistorialEstado/NotificacionEnviada/Adjunto de la Solucion antes de
    /// borrar la Solucion misma — todas esas FK son DeleteBehavior.Restrict, así
    /// que un DELETE directo sobre dbo.Solucion con hijos vivos falla. Todo dentro
    /// de una transacción para que quede atómico (o se borra todo, o no se borra nada).
    /// </summary>
    public void Eliminar(int solucionId)
    {
        using var transaction = _db.Database.BeginTransaction();
        try
        {
            _db.HistorialEstados.RemoveRange(_db.HistorialEstados.Where(h => h.SolucionId == solucionId));
            _db.NotificacionesEnviadas.RemoveRange(_db.NotificacionesEnviadas.Where(n => n.SolucionId == solucionId));
            var idsAdjuntos = _db.Adjuntos.Where(a => a.SolucionId == solucionId).Select(a => a.AdjuntoId).ToList();
            _db.AdjuntoContenidos.RemoveRange(_db.AdjuntoContenidos.Where(c => idsAdjuntos.Contains(c.AdjuntoId)));
            _db.Adjuntos.RemoveRange(_db.Adjuntos.Where(a => a.SolucionId == solucionId));
            _db.SaveChanges();

            var solucion = _db.Soluciones.FirstOrDefault(s => s.SolucionId == solucionId);
            if (solucion is not null)
            {
                _db.Soluciones.Remove(solucion);
                _db.SaveChanges();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// El parámetro <paramref name="anio"/> se mantiene por compatibilidad con la
    /// firma de ISolucionRepository (y con la versión en memoria, usada como
    /// referencia/fallback) pero el procedimiento real ignora cualquier año que
    /// se le pase: siempre usa YEAR(SYSUTCDATETIME()) internamente, exactamente
    /// igual que el resto de columnas con SYSUTCDATETIME() de este esquema.
    /// </summary>
    public string GenerarCodigoSolucion(int anio)
    {
        var codigoParam = new SqlParameter
        {
            ParameterName = "@Codigo",
            SqlDbType = SqlDbType.NVarChar,
            Size = 20,
            Direction = ParameterDirection.Output
        };

        _db.Database.ExecuteSqlRaw("EXEC dbo.sp_GenerarCodigoSolucion @Codigo OUTPUT", codigoParam);

        return (string?)codigoParam.Value
               ?? throw new InvalidOperationException("dbo.sp_GenerarCodigoSolucion no devolvió un código (parámetro @Codigo nulo).");
    }

    public void AgregarHistorial(HistorialEstado historial)
    {
        _db.HistorialEstados.Add(historial);
        _db.SaveChanges();
    }

    public bool ExistenSolucionesConPlataforma(int plataformaId) =>
        _db.Soluciones.Any(s => s.PlataformaId == plataformaId);

    public IReadOnlyList<HistorialEstado> ObtenerHistorial(int solucionId) =>
        _db.HistorialEstados
            .Where(h => h.SolucionId == solucionId)
            .OrderBy(h => h.FechaCambio)
            .ToList();

    public void AgregarAdjunto(Adjunto adjunto, byte[] contenido)
    {
        using var transaction = _db.Database.BeginTransaction();
        try
        {
            _db.Adjuntos.Add(adjunto);
            _db.SaveChanges();

            _db.AdjuntoContenidos.Add(new AdjuntoContenido
            {
                AdjuntoId = adjunto.AdjuntoId,
                Contenido = contenido
            });
            _db.SaveChanges();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public IReadOnlyList<Adjunto> ObtenerAdjuntos(int solucionId) =>
        _db.Adjuntos
            .Where(a => a.SolucionId == solucionId)
            .OrderBy(a => a.FechaCarga)
            .ToList();

    public Adjunto? ObtenerAdjunto(int adjuntoId) =>
        _db.Adjuntos.FirstOrDefault(a => a.AdjuntoId == adjuntoId);

    public byte[]? ObtenerContenidoAdjunto(int adjuntoId) =>
        _db.AdjuntoContenidos
            .Where(c => c.AdjuntoId == adjuntoId)
            .Select(c => c.Contenido)
            .FirstOrDefault();

    public void EliminarAdjunto(int adjuntoId)
    {
        using var transaction = _db.Database.BeginTransaction();
        try
        {
            _db.AdjuntoContenidos.RemoveRange(_db.AdjuntoContenidos.Where(c => c.AdjuntoId == adjuntoId));
            _db.SaveChanges();

            var adjunto = _db.Adjuntos.FirstOrDefault(a => a.AdjuntoId == adjuntoId);
            if (adjunto is not null)
            {
                _db.Adjuntos.Remove(adjunto);
                _db.SaveChanges();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void RegistrarNotificacion(NotificacionEnviada notificacion)
    {
        _db.NotificacionesEnviadas.Add(notificacion);
        _db.SaveChanges();
    }

    public IReadOnlyList<NotificacionEnviada> ObtenerNotificaciones(int solucionId) =>
        _db.NotificacionesEnviadas
            .Where(n => n.SolucionId == solucionId)
            .OrderBy(n => n.FechaEnvio)
            .ToList();
}
