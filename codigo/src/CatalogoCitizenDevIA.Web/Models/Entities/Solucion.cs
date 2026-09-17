namespace CatalogoCitizenDevIA.Web.Models.Entities;

/// <summary>
/// Tabla principal - un registro por solucion de Citizen Development / IA.
/// Replica 1:1 dbo.Solucion de 06-diseno-bd-sql-server.md. NivelRiesgo y
/// ProximaRevision son columnas PERSISTED en SQL Server (calculadas por
/// dbo.fn_NivelRiesgo); aqui, sin motor de base de datos disponible, se
/// recalculan y se guardan explicitamente en SolucionService cada vez que
/// cambia un campo de entrada, para simular el mismo comportamiento
/// "siempre consistente" (ver 08-reglas-negocio-calculo-riesgo.md).
/// </summary>
public class Solucion
{
    public static readonly string[] TiposValidos = { "Dashboard", "Reporte", "Automatización", "Agente de IA", "App", "Bot", "Otro" };
    public static readonly string[] AlcancesValidos = { "Personal", "Departamental", "Multi-área", "Multi-país" };
    public static readonly string[] InfraDedicadaValidos = { "Si", "No", "Entorno compartido", "Sandbox personal" };
    public static readonly string[] EstadosValidos =
    {
        "Registrado", "En revisión", "Aprobado - Comité Citizen Dev", "Aprobado - Comité N-1", "Rechazado", "Reclasificado"
    };
    public static readonly string[] RevisadoPorValidos = { "N/A - autoregistro", "Comité de Citizen Development", "Comité N-1" };
    public static readonly string[] MadurezValidos = { "Piloto", "En producción", "Descontinuado" };
    public static readonly string[] ClasificacionValidos = { "Pública", "Interna", "Confidencial", "Restringida" };

    public int SolucionId { get; set; }
    public string CodigoSolucion { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string TipoSolucion { get; set; } = string.Empty;
    public string? DescripcionBreve { get; set; }
    /// <summary>
    /// OWNER de la solucion: la persona responsable de ella en el negocio. Es el
    /// dato que se muestra como principal en el catalogo, y un Administrador puede
    /// reasignarlo.
    /// </summary>
    public int UsuarioDuenioId { get; set; }

    /// <summary>
    /// Quien cargo el registro en la App. Hecho historico: se fija al registrar y
    /// NO se modifica nunca, ni siquiera al reasignar el owner. Solo aparece en el
    /// detalle, no en el listado del catalogo. Ver sql/migracion_registrador.sql.
    /// </summary>
    public int RegistradoPorId { get; set; }
    public string Area { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public int PlataformaId { get; set; }
    public DateOnly FechaCreacionSolucion { get; set; }

    // Columnas J-O del Excel: disparan el calculo de NivelRiesgo (ver RiesgoCalculator)
    public bool EsProcesoCritico { get; set; }
    public string AlcanceUso { get; set; } = string.Empty;
    public bool RequiereConexionCentral { get; set; }
    public bool UsaPii { get; set; }
    public bool RequierePublicacionInternet { get; set; }
    public string RequiereInfraDedicada { get; set; } = "No";

    // Calculados (persistidos explicitamente por SolucionService; en SQL Server son PERSISTED)
    public string NivelRiesgo { get; set; } = string.Empty;
    public DateOnly? ProximaRevision { get; set; }

    public string? RiesgoJustificacionOverride { get; set; }
    public int? RiesgoOverridePorId { get; set; }

    public string Estado { get; set; } = "Registrado";
    public string? RevisadoPor { get; set; }
    public DateOnly? FechaUltimaRevision { get; set; }

    public string? Comentarios { get; set; }
    public string? EnlaceAcceso { get; set; }
    public string? NivelMadurez { get; set; }
    public string? ClasificacionInformacion { get; set; }
    public int? AudienciaEstimada { get; set; }
    public int? SponsorTiId { get; set; }
    public DateOnly? FechaBaja { get; set; }
    public string? Etiquetas { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;
}
