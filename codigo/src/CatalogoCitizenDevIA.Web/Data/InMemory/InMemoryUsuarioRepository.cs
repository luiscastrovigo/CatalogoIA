using System.Collections.Concurrent;
using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Data.InMemory;

/// <summary>
/// Sustituto en memoria de dbo.Usuario, DETRAS DE IUsuarioRepository.
/// Reemplazar por una implementacion EF Core + SQL Server (PERMS02) para
/// produccion, sin tocar el resto de la app — ver 06-diseno-bd-sql-server.md
/// y la nota de arquitectura en 07-arquitectura-despliegue-componentes.md.
///
/// Registrado como Singleton para que los datos sobrevivan entre requests
/// dentro del proceso (no hay base de datos real disponible en este sandbox).
/// </summary>
public class InMemoryUsuarioRepository : IUsuarioRepository
{
    private readonly ConcurrentDictionary<int, Usuario> _usuarios = new();
    private int _siguienteId = 1;
    private readonly object _lock = new();

    public InMemoryUsuarioRepository()
    {
        // Replica sql/02_seed_inicial.sql: primer administrador precargado.
        Crear(new Usuario
        {
            AzureAdObjectId = Guid.NewGuid(),
            CorreoCorporativo = "luis.castro@yanbal.com",
            NombreCompleto = "Luis Castro",
            Area = "Arquitectura de Aplicaciones",
            Pais = "Perú",
            EsAdministrador = true
        });
    }

    public Usuario? ObtenerPorAzureAdObjectId(Guid azureAdObjectId) =>
        _usuarios.Values.FirstOrDefault(u => u.AzureAdObjectId == azureAdObjectId);

    public Usuario? ObtenerPorCorreo(string correo) =>
        _usuarios.Values.FirstOrDefault(u => string.Equals(u.CorreoCorporativo, correo, StringComparison.OrdinalIgnoreCase));

    public Usuario? ObtenerPorId(int usuarioId) => _usuarios.GetValueOrDefault(usuarioId);

    public IReadOnlyList<Usuario> ObtenerTodos() => _usuarios.Values.OrderBy(u => u.NombreCompleto).ToList();

    public Usuario Crear(Usuario usuario)
    {
        lock (_lock)
        {
            usuario.UsuarioId = _siguienteId++;
            _usuarios[usuario.UsuarioId] = usuario;
            return usuario;
        }
    }

    public void Actualizar(Usuario usuario) => _usuarios[usuario.UsuarioId] = usuario;
}
