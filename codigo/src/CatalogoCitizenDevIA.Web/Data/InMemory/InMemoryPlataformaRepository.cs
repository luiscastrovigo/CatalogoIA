using System.Collections.Concurrent;
using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Data.InMemory;

/// <summary>
/// Sustituto en memoria de dbo.Plataforma. Se deja intencionalmente SIN
/// datos precargados (05-documento-cero-prerequisitos.md, seccion 4: el
/// catalogo real de plataformas no es bloqueante y se completa progresivamente).
/// </summary>
public class InMemoryPlataformaRepository : IPlataformaRepository
{
    private readonly ConcurrentDictionary<int, Plataforma> _plataformas = new();
    private int _siguienteId = 1;
    private readonly object _lock = new();

    public IReadOnlyList<Plataforma> ObtenerTodas() => _plataformas.Values.OrderBy(p => p.Nombre).ToList();

    public IReadOnlyList<Plataforma> ObtenerActivas() =>
        _plataformas.Values.Where(p => p.Activo).OrderBy(p => p.Nombre).ToList();

    public Plataforma? ObtenerPorId(int plataformaId) => _plataformas.GetValueOrDefault(plataformaId);

    public Plataforma Crear(Plataforma plataforma)
    {
        lock (_lock)
        {
            plataforma.PlataformaId = _siguienteId++;
            _plataformas[plataforma.PlataformaId] = plataforma;
            return plataforma;
        }
    }

    public void Actualizar(Plataforma plataforma) => _plataformas[plataforma.PlataformaId] = plataforma;

    public void Eliminar(int plataformaId) => _plataformas.TryRemove(plataformaId, out _);
}
