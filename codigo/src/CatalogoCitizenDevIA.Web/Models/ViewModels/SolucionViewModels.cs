using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Models.ViewModels;

public class CatalogoFiltro
{
    public string? Texto { get; set; }
    public string? Pais { get; set; }
    public string? Area { get; set; }
    public string? TipoSolucion { get; set; }
    public int? PlataformaId { get; set; }
    public string? NivelRiesgo { get; set; }
    public string? Estado { get; set; }

    /// <summary>
    /// Filtro usado por el card "Próximas a revisión (30 días)" del Dashboard
    /// ejecutivo (retroalimentacion de usuarios, 2026-09-08): trae las
    /// soluciones con ProximaRevision dentro de los próximos 30 días
    /// (incluye las ya vencidas, igual que el conteo del KPI).
    /// </summary>
    public bool? Proximas30Dias { get; set; }
}

public class CatalogoViewModel
{
    public CatalogoFiltro Filtro { get; set; } = new();
    public List<Solucion> Soluciones { get; set; } = new();
    public Dictionary<int, string> NombrePlataformaPorId { get; set; } = new();
    public Dictionary<int, string> NombreDuenioPorId { get; set; } = new();
    public IReadOnlyList<Plataforma> Plataformas { get; set; } = new List<Plataforma>();
}

public class RegistroSolucionInput
{
    public string Nombre { get; set; } = string.Empty;
    public string TipoSolucion { get; set; } = string.Empty;
    public string? DescripcionBreve { get; set; }
    public string Area { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public int PlataformaId { get; set; }
    public DateOnly FechaCreacionSolucion { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool EsProcesoCritico { get; set; }
    public string AlcanceUso { get; set; } = "Personal";
    public bool RequiereConexionCentral { get; set; }
    public bool UsaPii { get; set; }
    public bool RequierePublicacionInternet { get; set; }
    public string RequiereInfraDedicada { get; set; } = "No";

    public string? EnlaceAcceso { get; set; }
    public string? NivelMadurez { get; set; }
    public string? ClasificacionInformacion { get; set; }
    public int? AudienciaEstimada { get; set; }
    public string? Etiquetas { get; set; }
    public string? Comentarios { get; set; }

    /// <summary>
    /// Dueno del registro. Solo los usa un Administrador que registra o corrige un
    /// registro a nombre de otra persona; el controlador IGNORA ambos campos si
    /// quien envia el formulario no es Administrador, de modo que no sirven para
    /// sobre-postear el dueno (mismo criterio del hallazgo H-03 de la auditoria).
    /// </summary>
    public string? DuenioCorreo { get; set; }
    public string? DuenioNombre { get; set; }
}

/// <summary>
/// Formulario de edición desde "Ver Detalle" (punto 3 de retroalimentacion de
/// usuarios, 2026-09). Mismos campos que RegistroSolucionInput — se reutiliza
/// por herencia para no duplicar el formulario, mas SolucionId para saber cual
/// registro actualizar en el POST.
/// </summary>
public class EditarSolucionInput : RegistroSolucionInput
{
    public int SolucionId { get; set; }
}

public class CalcularRiesgoRequest
{
    public bool EsProcesoCritico { get; set; }
    public string AlcanceUso { get; set; } = "Personal";
    public bool RequiereConexionCentral { get; set; }
    public bool UsaPii { get; set; }
    public bool RequierePublicacionInternet { get; set; }
    public string RequiereInfraDedicada { get; set; } = "No";
}

public class DetalleSolucionViewModel
{
    public Solucion Solucion { get; set; } = null!;
    public Plataforma Plataforma { get; set; } = null!;
    /// <summary>Owner de la solucion: el dato principal, el que muestra el catalogo.</summary>
    public Usuario Duenio { get; set; } = null!;

    /// <summary>Quien cargo el registro. Solo se muestra en el detalle y el comprobante.</summary>
    public Usuario? Registrador { get; set; }
    public List<HistorialEstado> Historial { get; set; } = new();
    public List<NotificacionEnviada> Notificaciones { get; set; } = new();
    public List<Adjunto> Adjuntos { get; set; } = new();

    /// <summary>Nombre para mostrar en la columna "Usuario" del historial (punto 5, 2026-09).</summary>
    public Dictionary<int, string> NombreUsuarioPorId { get; set; } = new();

    /// <summary>Punto 3 (2026-09): solo el dueño original o un Administrador ven el botón Editar.</summary>
    public bool PuedeEditar { get; set; }
}
