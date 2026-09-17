using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Services;

public sealed class ResultadoAsignacion
{
    public bool EsValido { get; init; }
    public string Error { get; init; } = string.Empty;
    public Usuario? Duenio { get; init; }
    public bool UsuarioPrecreado { get; init; }

    public static ResultadoAsignacion Invalido(string error) => new() { EsValido = false, Error = error };
}

/// <summary>
/// Resuelve a que Usuario debe quedar asignada una solucion cuando un Administrador
/// registra o corrige un registro a nombre de otra persona.
///
/// IMPORTANTE, para no confundir con codigo retirado: este servicio NO es el
/// RoleResolutionService que se elimino en la auditoria de seguridad (hallazgo
/// H-02). Aquel autenticaba a cualquiera sin contrasena. Este no autentica a
/// nadie: solo resuelve o precrea una fila de dbo.Usuario SIN ningun rol, y sus
/// dos unicos llamadores exigen rol Administrador antes de invocarlo. Es el mismo
/// mecanismo que ya usaba "Invitar" en Gestion de Usuarios.
///
/// Regla deliberada sobre el nombre: si la persona YA existe, su NombreCompleto
/// solo se corrige cuando lo guardado es el marcador de invitacion o parece un
/// correo. Nunca se pisa un nombre real, porque la fuente de verdad de ese dato
/// es el token de Entra ID en el primer login de esa persona, no lo que escriba
/// un Administrador en este formulario.
/// </summary>
public class AsignacionDuenioService
{
    public const string NombrePendiente = "(Pendiente de primer ingreso)";

    private readonly IUsuarioRepository _usuarios;

    public AsignacionDuenioService(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public ResultadoAsignacion Resolver(string? correo, string? nombre)
    {
        correo = correo?.Trim() ?? string.Empty;
        nombre = nombre?.Trim();

        if (string.IsNullOrWhiteSpace(correo))
        {
            return ResultadoAsignacion.Invalido("Ingresa el correo corporativo del dueño del registro.");
        }

        if (!DominiosCorporativos.EsCorporativo(correo))
        {
            return ResultadoAsignacion.Invalido(
                $"El dueño debe tener un correo corporativo ({DominiosCorporativos.ListaLegible}).");
        }

        var existente = _usuarios.ObtenerPorCorreo(correo);
        if (existente is not null)
        {
            if (!string.IsNullOrWhiteSpace(nombre) && EsNombreProvisional(existente.NombreCompleto))
            {
                existente.NombreCompleto = nombre;
                _usuarios.Actualizar(existente);
            }

            return new ResultadoAsignacion { EsValido = true, Duenio = existente };
        }

        var creado = _usuarios.Crear(new Usuario
        {
            AzureAdObjectId = Guid.NewGuid(),
            CorreoCorporativo = correo,
            NombreCompleto = string.IsNullOrWhiteSpace(nombre) ? NombrePendiente : nombre,
            EsAdministrador = false,
            EsAprobador = false
        });

        return new ResultadoAsignacion { EsValido = true, Duenio = creado, UsuarioPrecreado = true };
    }

    private static bool EsNombreProvisional(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) || nombre == NombrePendiente || nombre.Contains('@');
}
