namespace CatalogoCitizenDevIA.Web.Services;

public enum NivelRiesgo
{
    Nivel1 = 1,
    Nivel2 = 2,
    Nivel3 = 3
}

/// <summary>
/// Replica exacta, en C#, de la funcion SQL dbo.fn_NivelRiesgo
/// (06-diseno-bd-sql-server.md) y de la formula original del Excel
/// (columnas J a O -> P, y P -> T). Ver el detalle completo, la
/// justificacion de por que vive en dos capas y la tabla de casos de
/// prueba en 08-reglas-negocio-calculo-riesgo.md.
///
/// Cualquier cambio a esta regla debe reflejarse tambien en la funcion
/// SQL y en ese documento.
/// </summary>
public static class RiesgoCalculator
{
    private static readonly string[] AlcancesQueSubenANivel2 =
    {
        "Departamental", "Multi-área", "Multi-país"
    };

    public static NivelRiesgo CalcularNivelRiesgo(
        bool esProcesoCritico,            // columna J
        string alcanceUso,                // columna K: Personal | Departamental | Multi-área | Multi-país
        bool requiereConexionCentral,     // columna L
        bool usaPii,                      // columna M
        bool requierePublicacionInternet, // columna N
        string requiereInfraDedicada)     // columna O: Si | No | Entorno compartido | Sandbox personal
    {
        var esNivel3 =
            esProcesoCritico
            || requiereConexionCentral
            || usaPii
            || requierePublicacionInternet
            || string.Equals(requiereInfraDedicada, "Si", StringComparison.OrdinalIgnoreCase);

        if (esNivel3)
        {
            return NivelRiesgo.Nivel3;
        }

        if (AlcancesQueSubenANivel2.Contains(alcanceUso, StringComparer.OrdinalIgnoreCase))
        {
            return NivelRiesgo.Nivel2;
        }

        return NivelRiesgo.Nivel1;
    }

    /// <summary>
    /// Replica de la columna T "Proxima revision". Devuelve null para
    /// Nivel 3 (equivalente al "N/A" del Excel) — ver la observacion en
    /// 08-reglas-negocio-calculo-riesgo.md sobre si este comportamiento
    /// debe mantenerse.
    /// </summary>
    public static DateOnly? CalcularProximaRevision(NivelRiesgo nivelRiesgo, DateOnly? fechaUltimaRevision)
    {
        if (fechaUltimaRevision is null)
        {
            return null;
        }

        return nivelRiesgo switch
        {
            NivelRiesgo.Nivel1 or NivelRiesgo.Nivel2 => fechaUltimaRevision.Value.AddDays(180),
            NivelRiesgo.Nivel3 => null,
            _ => throw new ArgumentOutOfRangeException(nameof(nivelRiesgo))
        };
    }

    public static string ComoTextoBd(this NivelRiesgo nivel) => nivel switch
    {
        NivelRiesgo.Nivel1 => "Nivel 1",
        NivelRiesgo.Nivel2 => "Nivel 2",
        NivelRiesgo.Nivel3 => "Nivel 3",
        _ => throw new ArgumentOutOfRangeException(nameof(nivel))
    };
}
