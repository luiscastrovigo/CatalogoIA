using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogoCitizenDevIA.Web.Data.EfCore;

/// <summary>
/// Implementacion real de IPlataformaRepository contra dbo.Plataforma (PERMS02),
/// vía EF Core. Reemplaza a InMemoryPlataformaRepository. Registrar como Scoped.
/// </summary>
public class EfPlataformaRepository : IPlataformaRepository
{
    private readonly AppDbContext _db;

    public EfPlataformaRepository(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Plataforma> ObtenerTodas() =>
        _db.Plataformas.OrderBy(p => p.Nombre).ToList();

    public IReadOnlyList<Plataforma> ObtenerActivas() =>
        _db.Plataformas.Where(p => p.Activo).OrderBy(p => p.Nombre).ToList();

    public Plataforma? ObtenerPorId(int plataformaId) =>
        _db.Plataformas.FirstOrDefault(p => p.PlataformaId == plataformaId);

    public Plataforma Crear(Plataforma plataforma)
    {
        _db.Plataformas.Add(plataforma);
        _db.SaveChanges();
        return plataforma;
    }

    public void Actualizar(Plataforma plataforma)
    {
        _db.Plataformas.Update(plataforma);
        _db.SaveChanges();
    }

    public void Eliminar(int plataformaId)
    {
        var plataforma = _db.Plataformas.FirstOrDefault(p => p.PlataformaId == plataformaId);
        if (plataforma is null) return;

        _db.Plataformas.Remove(plataforma);
        _db.SaveChanges();
    }
}
