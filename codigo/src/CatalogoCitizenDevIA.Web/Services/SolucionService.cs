using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Services;

public record NuevaSolucionRequest(
    string Nombre,
    string TipoSolucion,
    string? DescripcionBreve,
    string Area,
    string Pais,
    int PlataformaId,
    DateOnly FechaCreacionSolucion,
    bool EsProcesoCritico,
    string AlcanceUso,
    bool RequiereConexionCentral,
    bool UsaPii,
    bool RequierePublicacionInternet,
    string RequiereInfraDedicada,
    string? EnlaceAcceso,
    string? NivelMadurez,
    string? ClasificacionInformacion,
    int? AudienciaEstimada,
    string? Etiquetas,
    string? Comentarios);

/// <summary>
/// Campos editables desde "Ver Detalle" del Catálogo (punto 3 de retroalimentacion
/// de usuarios, 2026-09). Deliberadamente NO incluye Estado/RevisadoPor — esos solo
/// cambian desde el módulo "Revisión y aprobación" (RevisionController.CambiarEstado),
/// para no mezclar edición de datos con el flujo de aprobación.
/// </summary>
public record ActualizarSolucionRequest(
    string Nombre,
    string TipoSolucion,
    string? DescripcionBreve,
    string Area,
    string Pais,
    int PlataformaId,
    DateOnly FechaCreacionSolucion,
    bool EsProcesoCritico,
    string AlcanceUso,
    bool RequiereConexionCentral,
    bool UsaPii,
    bool RequierePublicacionInternet,
    string RequiereInfraDedicada,
    string? EnlaceAcceso,
    string? NivelMadurez,
    string? ClasificacionInformacion,
    int? AudienciaEstimada,
    string? Etiquetas,
    string? Comentarios);

/// <summary>
/// Orquesta la creacion de una Solucion: genera el codigo correlativo,
/// calcula el nivel de riesgo (RiesgoCalculator — la misma logica que
/// fn_NivelRiesgo en SQL Server), guarda el registro, abre el primer
/// HistorialEstado y dispara la notificacion de comprobante. Ver
/// 08-reglas-negocio-calculo-riesgo.md y 03-alcance-funcional.md (modulo
/// "Registro de solucion").
/// </summary>
public class SolucionService
{
    private readonly ISolucionRepository _soluciones;
    private readonly IUsuarioRepository _usuarios;
    private readonly IPlataformaRepository _plataformas;
    private readonly INotificacionService _notificaciones;

    public SolucionService(
        ISolucionRepository soluciones,
        IUsuarioRepository usuarios,
        IPlataformaRepository plataformas,
        INotificacionService notificaciones)
    {
        _soluciones = soluciones;
        _usuarios = usuarios;
        _plataformas = plataformas;
        _notificaciones = notificaciones;
    }

    /// <summary>
    /// Registra una solucion. El parametro registradoPorId permite que un
    /// Administrador registre a nombre de otra persona: el dueno del registro es
    /// usuarioDuenioId, pero el historial guarda quien lo hizo realmente, que es
    /// lo que se audita despues.
    /// </summary>
    public async Task<Solucion> RegistrarAsync(NuevaSolucionRequest request, int usuarioDuenioId, int? registradoPorId = null)
    {
        var nivelRiesgo = RiesgoCalculator.CalcularNivelRiesgo(
            request.EsProcesoCritico,
            request.AlcanceUso,
            request.RequiereConexionCentral,
            request.UsaPii,
            request.RequierePublicacionInternet,
            request.RequiereInfraDedicada);

        var codigo = _soluciones.GenerarCodigoSolucion(request.FechaCreacionSolucion.Year);

        var solucion = new Solucion
        {
            CodigoSolucion = codigo,
            Nombre = request.Nombre,
            TipoSolucion = request.TipoSolucion,
            DescripcionBreve = request.DescripcionBreve,
            UsuarioDuenioId = usuarioDuenioId,
            RegistradoPorId = registradoPorId ?? usuarioDuenioId,
            Area = request.Area,
            Pais = request.Pais,
            PlataformaId = request.PlataformaId,
            FechaCreacionSolucion = request.FechaCreacionSolucion,
            EsProcesoCritico = request.EsProcesoCritico,
            AlcanceUso = request.AlcanceUso,
            RequiereConexionCentral = request.RequiereConexionCentral,
            UsaPii = request.UsaPii,
            RequierePublicacionInternet = request.RequierePublicacionInternet,
            RequiereInfraDedicada = request.RequiereInfraDedicada,
            NivelRiesgo = nivelRiesgo.ComoTextoBd(),
            // Al registrar, la fecha de ultima revision arranca en "hoy" para
            // que Nivel 1/2 ya tengan una Proxima revision calculada (180 dias) —
            // igual que asumiria el Excel para una fila recien creada.
            FechaUltimaRevision = request.FechaCreacionSolucion,
            ProximaRevision = RiesgoCalculator.CalcularProximaRevision(nivelRiesgo, request.FechaCreacionSolucion),
            Estado = "Registrado",
            RevisadoPor = "N/A - autoregistro",
            EnlaceAcceso = request.EnlaceAcceso,
            NivelMadurez = request.NivelMadurez,
            ClasificacionInformacion = request.ClasificacionInformacion,
            AudienciaEstimada = request.AudienciaEstimada,
            Etiquetas = request.Etiquetas,
            Comentarios = request.Comentarios
        };

        _soluciones.Crear(solucion);

        _soluciones.AgregarHistorial(new HistorialEstado
        {
            SolucionId = solucion.SolucionId,
            EstadoAnterior = null,
            EstadoNuevo = "Registrado",
            UsuarioId = registradoPorId ?? usuarioDuenioId,
            Comentario = registradoPorId is null || registradoPorId == usuarioDuenioId
                ? "Registro inicial de la solución."
                : "Registro inicial de la solución, hecho por un Administrador a nombre del dueño."
        });

        // Decision del Arquitecto de Aplicacion (2026-09-14): el comprobante va a
        // quien REGISTRA, no al owner. Quien hace el tramite necesita su constancia;
        // el owner ve la solucion en el catalogo. Si mas adelante se decide avisar
        // tambien al owner, es agregar un segundo envio aqui.
        var destinatario = _usuarios.ObtenerPorId(solucion.RegistradoPorId)!;
        var plataforma = _plataformas.ObtenerPorId(request.PlataformaId)!;
        await _notificaciones.EnviarComprobanteRegistroAsync(solucion, destinatario, plataforma);

        return solucion;
    }

    /// <summary>
    /// Edición de un registro ya existente (punto 3 de retroalimentacion de
    /// usuarios, 2026-09) — solo el dueño original o un Administrador pueden
    /// llegar aquí (verificado en SolucionController antes de llamar este
    /// método). Recalcula NivelRiesgo/ProximaRevision igual que al registrar,
    /// por si el cambio de algún campo de riesgo (J-O) cambia el nivel.
    /// </summary>
    public Solucion Actualizar(int solucionId, ActualizarSolucionRequest request)
    {
        var solucion = _soluciones.ObtenerPorId(solucionId) ?? throw new InvalidOperationException("Solución no encontrada.");

        var nivelRiesgo = RiesgoCalculator.CalcularNivelRiesgo(
            request.EsProcesoCritico,
            request.AlcanceUso,
            request.RequiereConexionCentral,
            request.UsaPii,
            request.RequierePublicacionInternet,
            request.RequiereInfraDedicada);

        solucion.Nombre = request.Nombre;
        solucion.TipoSolucion = request.TipoSolucion;
        solucion.DescripcionBreve = request.DescripcionBreve;
        solucion.Area = request.Area;
        solucion.Pais = request.Pais;
        solucion.PlataformaId = request.PlataformaId;
        solucion.FechaCreacionSolucion = request.FechaCreacionSolucion;
        solucion.EsProcesoCritico = request.EsProcesoCritico;
        solucion.AlcanceUso = request.AlcanceUso;
        solucion.RequiereConexionCentral = request.RequiereConexionCentral;
        solucion.UsaPii = request.UsaPii;
        solucion.RequierePublicacionInternet = request.RequierePublicacionInternet;
        solucion.RequiereInfraDedicada = request.RequiereInfraDedicada;
        solucion.NivelRiesgo = nivelRiesgo.ComoTextoBd();
        solucion.ProximaRevision = RiesgoCalculator.CalcularProximaRevision(nivelRiesgo, solucion.FechaUltimaRevision ?? request.FechaCreacionSolucion);
        solucion.EnlaceAcceso = request.EnlaceAcceso;
        solucion.NivelMadurez = request.NivelMadurez;
        solucion.ClasificacionInformacion = request.ClasificacionInformacion;
        solucion.AudienciaEstimada = request.AudienciaEstimada;
        solucion.Etiquetas = request.Etiquetas;
        solucion.Comentarios = request.Comentarios;

        _soluciones.Actualizar(solucion);
        return solucion;
    }

    /// <summary>
    /// Reasigna el dueno de un registro y deja constancia en el historial. Solo lo
    /// invoca SolucionController tras verificar rol Administrador.
    ///
    /// El cambio se anota en HistorialEstado con el mismo estado de entrada y de
    /// salida: esa tabla es la bitacora que ya se muestra en "Ver detalle", asi que
    /// es donde un auditor va a buscar. Se documenta aqui que es un uso ampliado de
    /// esa tabla, no un cambio de estado real.
    /// </summary>
    public void ReasignarDuenio(int solucionId, Usuario nuevoDuenio, Usuario duenioAnterior, int administradorId)
    {
        var solucion = _soluciones.ObtenerPorId(solucionId) ?? throw new InvalidOperationException("Solución no encontrada.");

        solucion.UsuarioDuenioId = nuevoDuenio.UsuarioId;
        _soluciones.Actualizar(solucion);

        _soluciones.AgregarHistorial(new HistorialEstado
        {
            SolucionId = solucionId,
            EstadoAnterior = solucion.Estado,
            EstadoNuevo = solucion.Estado,
            UsuarioId = administradorId,
            Comentario = $"Owner del registro reasignado de {duenioAnterior.CorreoCorporativo} a {nuevoDuenio.CorreoCorporativo}. Quien registró originalmente no cambia."
        });
    }

    public void CambiarEstado(int solucionId, string nuevoEstado, string revisadoPor, int usuarioQueRevisaId, string? comentario)
    {
        var solucion = _soluciones.ObtenerPorId(solucionId) ?? throw new InvalidOperationException("Solución no encontrada.");
        var estadoAnterior = solucion.Estado;

        solucion.Estado = nuevoEstado;
        solucion.RevisadoPor = revisadoPor;
        _soluciones.Actualizar(solucion);

        _soluciones.AgregarHistorial(new HistorialEstado
        {
            SolucionId = solucionId,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = nuevoEstado,
            UsuarioId = usuarioQueRevisaId,
            Comentario = comentario
        });
    }
}
