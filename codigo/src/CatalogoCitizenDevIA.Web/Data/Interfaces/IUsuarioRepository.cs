using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Data.Interfaces;

/// <summary>
/// Contrato de acceso a dbo.Usuario. En produccion se implementa con EF Core
/// contra SQL Server (PERMS02); en este sandbox, InMemoryUsuarioRepository.
/// </summary>
public interface IUsuarioRepository
{
    Usuario? ObtenerPorAzureAdObjectId(Guid azureAdObjectId);
    Usuario? ObtenerPorCorreo(string correo);
    Usuario? ObtenerPorId(int usuarioId);
    IReadOnlyList<Usuario> ObtenerTodos();
    Usuario Crear(Usuario usuario);
    void Actualizar(Usuario usuario);
}
