using System.Collections.Concurrent;
using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Data.InMemory;

/// <summary>
/// Sustituto en memoria de dbo.Solucion + dbo.SolucionContador +
/// dbo.HistorialEstado + dbo.NotificacionEnviada. El metodo
/// GenerarCodigoSolucion replica sp_GenerarCodigoSolucion (bloqueo
/// exclusivo por anio, sin duplicados bajo concurrencia) usando un
/// lock de .NET en vez de UPDLOCK/HOLDLOCK de SQL Server — mismo
/// contrato, misma garantia de no-duplicados.
/// </summary>
public class InMemorySolucionRepository : ISolucionRepository
{
    private readonly ConcurrentDictionary<int, Solucion> _soluciones = new();
    private readonly ConcurrentDictionary<int, int> _contadorPorAnio = new();
    private readonly List<HistorialEstado> _historial = new();
    private readonly List<NotificacionEnviada> _notificaciones = new();
    private readonly List<Adjunto> _adjuntos = new();
    private readonly Dictionary<int, byte[]> _contenidosAdjuntos = new();
    private int _siguienteAdjuntoId = 1;
    private readonly object _lockAdjunto = new();
    private int _siguienteSolucionId = 1;
    private int _siguienteHistorialId = 1;
    private int _siguienteNotificacionId = 1;
    private readonly object _lockSolucion = new();
    private readonly object _lockContador = new();
    private readonly object _lockHistorial = new();
    private readonly object _lockNotificacion = new();

    public IReadOnlyList<Solucion> ObtenerTodas() => _soluciones.Values.OrderByDescending(s => s.FechaRegistro).ToList();

    public Solucion? ObtenerPorId(int solucionId) => _soluciones.GetValueOrDefault(solucionId);

    public Solucion Crear(Solucion solucion)
    {
        lock (_lockSolucion)
        {
            solucion.SolucionId = _siguienteSolucionId++;
            _soluciones[solucion.SolucionId] = solucion;
            return solucion;
        }
    }

    public void Actualizar(Solucion solucion)
    {
        solucion.FechaModificacion = DateTime.UtcNow;
        _soluciones[solucion.SolucionId] = solucion;
    }

    public void Eliminar(int solucionId)
    {
        lock (_lockAdjunto)
        {
            foreach (var adjunto in _adjuntos.Where(a => a.SolucionId == solucionId).ToList())
            {
                _contenidosAdjuntos.Remove(adjunto.AdjuntoId);
                _adjuntos.Remove(adjunto);
            }
        }
        _soluciones.TryRemove(solucionId, out _);

        lock (_lockHistorial)
        {
            _historial.RemoveAll(h => h.SolucionId == solucionId);
        }

        lock (_lockNotificacion)
        {
            _notificaciones.RemoveAll(n => n.SolucionId == solucionId);
        }
    }

    public string GenerarCodigoSolucion(int anio)
    {
        lock (_lockContador)
        {
            var numero = _contadorPorAnio.AddOrUpdate(anio, 1, (_, actual) => actual + 1);
            return $"CD-{anio}-{numero:000}";
        }
    }

    public void AgregarHistorial(HistorialEstado historial)
    {
        lock (_lockHistorial)
        {
            historial.HistorialEstadoId = _siguienteHistorialId++;
            _historial.Add(historial);
        }
    }

    public bool ExistenSolucionesConPlataforma(int plataformaId) =>
        _soluciones.Values.Any(s => s.PlataformaId == plataformaId);

    public IReadOnlyList<HistorialEstado> ObtenerHistorial(int solucionId) =>
        _historial.Where(h => h.SolucionId == solucionId).OrderBy(h => h.FechaCambio).ToList();

    public void RegistrarNotificacion(NotificacionEnviada notificacion)
    {
        lock (_lockNotificacion)
        {
            notificacion.NotificacionId = _siguienteNotificacionId++;
            _notificaciones.Add(notificacion);
        }
    }

    public IReadOnlyList<NotificacionEnviada> ObtenerNotificaciones(int solucionId) =>
        _notificaciones.Where(n => n.SolucionId == solucionId).OrderBy(n => n.FechaEnvio).ToList();

    public void AgregarAdjunto(Adjunto adjunto, byte[] contenido)
    {
        lock (_lockAdjunto)
        {
            adjunto.AdjuntoId = _siguienteAdjuntoId++;
            _adjuntos.Add(adjunto);
            _contenidosAdjuntos[adjunto.AdjuntoId] = contenido;
        }
    }

    public IReadOnlyList<Adjunto> ObtenerAdjuntos(int solucionId)
    {
        lock (_lockAdjunto)
        {
            return _adjuntos.Where(a => a.SolucionId == solucionId).OrderBy(a => a.FechaCarga).ToList();
        }
    }

    public Adjunto? ObtenerAdjunto(int adjuntoId)
    {
        lock (_lockAdjunto)
        {
            return _adjuntos.FirstOrDefault(a => a.AdjuntoId == adjuntoId);
        }
    }

    public byte[]? ObtenerContenidoAdjunto(int adjuntoId)
    {
        lock (_lockAdjunto)
        {
            return _contenidosAdjuntos.GetValueOrDefault(adjuntoId);
        }
    }

    public void EliminarAdjunto(int adjuntoId)
    {
        lock (_lockAdjunto)
        {
            _contenidosAdjuntos.Remove(adjuntoId);
            var adjunto = _adjuntos.FirstOrDefault(a => a.AdjuntoId == adjuntoId);
            if (adjunto is not null)
            {
                _adjuntos.Remove(adjunto);
            }
        }
    }
}
