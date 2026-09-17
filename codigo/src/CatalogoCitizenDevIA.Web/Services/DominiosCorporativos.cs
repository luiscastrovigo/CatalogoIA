namespace CatalogoCitizenDevIA.Web.Services;

/// <summary>
/// Dominios de correo corporativo aceptados por la App (auditoria de seguridad
/// 2026-09-10, hallazgo H-04). Vivia como campo privado de UsuarioController;
/// se extrajo aqui porque ahora tambien lo necesita la reasignacion de dueno de
/// una solucion, y dos listas separadas que deben decir lo mismo terminan
/// divergiendo. Ajustar aqui si Yanbal incorpora otro dominio.
/// </summary>
public static class DominiosCorporativos
{
    public static readonly string[] Permitidos = { "@yanbal.com" };

    public static string ListaLegible => string.Join(", ", Permitidos);

    public static bool EsCorporativo(string? correo) =>
        !string.IsNullOrWhiteSpace(correo)
        && correo.Contains('@')
        && Permitidos.Any(dominio => correo.EndsWith(dominio, StringComparison.OrdinalIgnoreCase));
}
