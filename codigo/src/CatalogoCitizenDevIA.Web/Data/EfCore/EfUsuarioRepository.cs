using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogoCitizenDevIA.Web.Data.EfCore;

/// <summary>
/// Implementacion real de IUsuarioRepository contra dbo.Usuario (PERMS02), vía
/// EF Core. Reemplaza a InMemoryUsuarioRepository (Data/InMemory/) — mismo
/// contrato, sin tocar controladores/servicios. Ver 06-diseno-bd-sql-server.md.
/// Registrar como Scoped (no Singleton: el DbContext no es thread-safe entre
/// requests concurrentes).
/// </summary>
public class EfUsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _db;

    public EfUsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    public Usuario? ObtenerPorAzureAdObjectId(Guid azureAdObjectId) =>
        _db.Usuarios.FirstOrDefault(u => u.AzureAdObjectId == azureAdObjectId);

    public Usuario? ObtenerPorCorreo(string correo) =>
        _db.Usuarios.FirstOrDefault(u => u.CorreoCorporativo == correo);

    public Usuario? ObtenerPorId(int usuarioId) =>
        _db.Usuarios.FirstOrDefault(u => u.UsuarioId == usuarioId);

    public IReadOnlyList<Usuario> ObtenerTodos() =>
        _db.Usuarios.OrderBy(u => u.NombreCompleto).ToList();

    public Usuario Crear(Usuario usuario)
    {
        _db.Usuarios.Add(usuario);
        _db.SaveChanges();
        return usuario;
    }

    public void Actualizar(Usuario usuario)
    {
        _db.Usuarios.Update(usuario);
        _db.SaveChanges();
    }
}
