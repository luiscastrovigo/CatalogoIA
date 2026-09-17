using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using CatalogoCitizenDevIA.Web.Models.ViewModels;
using CatalogoCitizenDevIA.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CatalogoCitizenDevIA.Web.Controllers;

/// <summary>
/// Modulos "Registro de solucion" y "Catalogo / consulta" de
/// 03-alcance-funcional.md. Cualquier usuario autenticado puede registrar y
/// consultar; solo el dueño (o un Administrador) ve el detalle de edicion.
/// </summary>
[Authorize]
public class SolucionController : Controller
{
    private readonly ISolucionRepository _soluciones;
    private readonly IPlataformaRepository _plataformas;
    private readonly IUsuarioRepository _usuarios;
    private readonly SolucionService _solucionService;
    private readonly AdjuntoService _adjuntos;
    private readonly AsignacionDuenioService _asignacionDuenio;

    public SolucionController(
        ISolucionRepository soluciones,
        IPlataformaRepository plataformas,
        IUsuarioRepository usuarios,
        SolucionService solucionService,
        AdjuntoService adjuntos,
        AsignacionDuenioService asignacionDuenio)
    {
        _soluciones = soluciones;
        _plataformas = plataformas;
        _usuarios = usuarios;
        _solucionService = solucionService;
        _adjuntos = adjuntos;
        _asignacionDuenio = asignacionDuenio;
    }

    // ---------------------------------------------------------
    // Catálogo / consulta
    // ---------------------------------------------------------
    [HttpGet]
    public IActionResult Index(CatalogoFiltro filtro)
    {
        var soluciones = _soluciones.ObtenerTodas().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            soluciones = soluciones.Where(s =>
                s.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                s.CodigoSolucion.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Pais)) soluciones = soluciones.Where(s => s.Pais == filtro.Pais);
        if (!string.IsNullOrWhiteSpace(filtro.Area)) soluciones = soluciones.Where(s => s.Area == filtro.Area);
        if (!string.IsNullOrWhiteSpace(filtro.TipoSolucion)) soluciones = soluciones.Where(s => s.TipoSolucion == filtro.TipoSolucion);
        if (filtro.PlataformaId is > 0) soluciones = soluciones.Where(s => s.PlataformaId == filtro.PlataformaId);
        if (!string.IsNullOrWhiteSpace(filtro.NivelRiesgo)) soluciones = soluciones.Where(s => s.NivelRiesgo == filtro.NivelRiesgo);
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) soluciones = soluciones.Where(s => s.Estado == filtro.Estado);

        if (filtro.Proximas30Dias == true)
        {
            var limite = DateOnly.FromDateTime(DateTime.Today).AddDays(30);
            soluciones = soluciones.Where(s => s.ProximaRevision.HasValue && s.ProximaRevision.Value <= limite);
        }

        var lista = soluciones.ToList();
        var plataformas = _plataformas.ObtenerTodas();

        var vm = new CatalogoViewModel
        {
            Filtro = filtro,
            Soluciones = lista,
            Plataformas = plataformas,
            NombrePlataformaPorId = plataformas.ToDictionary(p => p.PlataformaId, p => p.Nombre),
            NombreDuenioPorId = _usuarios.ObtenerTodos().ToDictionary(u => u.UsuarioId, u => u.NombreCompleto)
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        var solucion = _soluciones.ObtenerPorId(id);
        if (solucion is null) return NotFound();

        var vm = new DetalleSolucionViewModel
        {
            Solucion = solucion,
            Plataforma = _plataformas.ObtenerPorId(solucion.PlataformaId)!,
            Duenio = _usuarios.ObtenerPorId(solucion.UsuarioDuenioId)!,
            Registrador = _usuarios.ObtenerPorId(solucion.RegistradoPorId),
            Historial = _soluciones.ObtenerHistorial(id).ToList(),
            Notificaciones = _soluciones.ObtenerNotificaciones(id).ToList(),
            Adjuntos = _soluciones.ObtenerAdjuntos(id).ToList(),
            NombreUsuarioPorId = _usuarios.ObtenerTodos().ToDictionary(u => u.UsuarioId, u => u.NombreCompleto),
            PuedeEditar = PuedeEditar(solucion)
        };

        return View(vm);
    }

    // ---------------------------------------------------------
    // Edición (punto 3 de retroalimentacion de usuarios, 2026-09):
    // solo el dueño original o un Administrador pueden editar.
    // ---------------------------------------------------------
    [HttpGet]
    public IActionResult Editar(int id)
    {
        var solucion = _soluciones.ObtenerPorId(id);
        if (solucion is null) return NotFound();
        if (!PuedeEditar(solucion)) return Forbid();

        ViewBag.Plataformas = _plataformas.ObtenerActivas();
        ViewBag.Usuarios = _usuarios.ObtenerTodos();
        ViewBag.Registrador = _usuarios.ObtenerPorId(solucion.RegistradoPorId)?.NombreCompleto;

        var input = new EditarSolucionInput
        {
            SolucionId = solucion.SolucionId,
            Nombre = solucion.Nombre,
            TipoSolucion = solucion.TipoSolucion,
            DescripcionBreve = solucion.DescripcionBreve,
            Area = solucion.Area,
            Pais = solucion.Pais,
            PlataformaId = solucion.PlataformaId,
            FechaCreacionSolucion = solucion.FechaCreacionSolucion,
            EsProcesoCritico = solucion.EsProcesoCritico,
            AlcanceUso = solucion.AlcanceUso,
            RequiereConexionCentral = solucion.RequiereConexionCentral,
            UsaPii = solucion.UsaPii,
            RequierePublicacionInternet = solucion.RequierePublicacionInternet,
            RequiereInfraDedicada = solucion.RequiereInfraDedicada,
            EnlaceAcceso = solucion.EnlaceAcceso,
            NivelMadurez = solucion.NivelMadurez,
            ClasificacionInformacion = solucion.ClasificacionInformacion,
            AudienciaEstimada = solucion.AudienciaEstimada,
            Etiquetas = solucion.Etiquetas,
            Comentarios = solucion.Comentarios
        };

        // Precarga del dueno actual; el bloque solo se pinta para Administradores.
        var duenioActual = _usuarios.ObtenerPorId(solucion.UsuarioDuenioId);
        input.DuenioCorreo = duenioActual?.CorreoCorporativo;
        input.DuenioNombre = duenioActual?.NombreCompleto;

        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(EditarSolucionInput input)
    {
        var solucion = _soluciones.ObtenerPorId(input.SolucionId);
        if (solucion is null) return NotFound();
        if (!PuedeEditar(solucion)) return Forbid();

        if (_plataformas.ObtenerActivas().Count == 0)
        {
            ModelState.AddModelError(string.Empty,
                "Todavía no hay plataformas activas en el maestro. Un Administrador debe darlas de alta desde 'Administración de plataformas' antes de poder guardar cambios.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Plataformas = _plataformas.ObtenerActivas();
            return View(input);
        }

        var request = new ActualizarSolucionRequest(
            input.Nombre, input.TipoSolucion, input.DescripcionBreve, input.Area, input.Pais,
            input.PlataformaId, input.FechaCreacionSolucion, input.EsProcesoCritico, input.AlcanceUso,
            input.RequiereConexionCentral, input.UsaPii, input.RequierePublicacionInternet,
            input.RequiereInfraDedicada, input.EnlaceAcceso, input.NivelMadurez, input.ClasificacionInformacion,
            input.AudienciaEstimada, input.Etiquetas, input.Comentarios);

        _solucionService.Actualizar(input.SolucionId, request);

        // Reasignacion de dueno: SOLO Administradores. Para cualquier otro usuario
        // los campos del formulario se ignoran por completo aunque vengan en el POST.
        // Se distingue lo que FALLO (rojo) de lo que simplemente informa (verde): que
        // el dueno nuevo no haya ingresado nunca a la App es el caso normal de esta
        // funcion, no un error.
        var errorDuenio = string.Empty;
        var infoDuenio = string.Empty;
        if (User.GetEsAdministrador() && !string.IsNullOrWhiteSpace(input.DuenioCorreo))
        {
            var duenioAnterior = _usuarios.ObtenerPorId(solucion.UsuarioDuenioId);
            var esOtroCorreo = duenioAnterior is null
                || !string.Equals(duenioAnterior.CorreoCorporativo, input.DuenioCorreo.Trim(), StringComparison.OrdinalIgnoreCase);

            var resultado = _asignacionDuenio.Resolver(input.DuenioCorreo, input.DuenioNombre);
            if (!resultado.EsValido)
            {
                errorDuenio = resultado.Error;
            }
            else if (esOtroCorreo && duenioAnterior is not null)
            {
                _solucionService.ReasignarDuenio(input.SolucionId, resultado.Duenio!, duenioAnterior, User.GetUsuarioId());
                infoDuenio = resultado.UsuarioPrecreado
                    ? $" El dueño quedó asignado a {resultado.Duenio!.CorreoCorporativo}, que aún no ha ingresado a la App."
                    : $" El dueño quedó asignado a {resultado.Duenio!.CorreoCorporativo}.";
            }
        }

        TempData["Mensaje"] = "Cambios guardados." + infoDuenio;
        if (errorDuenio.Length > 0)
        {
            TempData["Error"] = $"No se cambió el dueño: {errorDuenio}";
        }
        return RedirectToAction(nameof(Details), new { id = input.SolucionId });
    }

    // ---------------------------------------------------------
    // Eliminar (punto 4 de retroalimentacion de usuarios, 2026-09):
    // solo Administradores — ver policy "EsAdministrador" abajo.
    // ---------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "EsAdministrador")]
    public IActionResult Eliminar(int id)
    {
        var solucion = _soluciones.ObtenerPorId(id);
        if (solucion is null) return NotFound();

        _soluciones.Eliminar(id);

        TempData["Mensaje"] = $"Se eliminó la solución {solucion.CodigoSolucion} — {solucion.Nombre}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Solo quien registró la solución (dueño original) o un Administrador
    /// pueden editarla — decisión explícita del Arquitecto de Aplicación
    /// (punto 3 de retroalimentacion de usuarios, 2026-09).
    /// </summary>
    private bool PuedeEditar(Solucion solucion) =>
        User.GetEsAdministrador() || User.GetUsuarioId() == solucion.UsuarioDuenioId;

    // ---------------------------------------------------------
    // Registro de solución
    // ---------------------------------------------------------
    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Plataformas = _plataformas.ObtenerActivas();
        ViewBag.Usuarios = _usuarios.ObtenerTodos();

        // Si venimos de un registro exitoso (ver Create POST), TempData trae el Id
        // para ofrecer el link "Ver comprobante" — no se reenvia a si mismo en
        // TempData asi que desaparece en el siguiente GET normal (F5 o navegacion).
        if (TempData.TryGetValue("UltimoSolucionId", out var ultimoId))
        {
            ViewBag.UltimoSolucionId = ultimoId;
        }

        return View(new RegistroSolucionInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Limite de tasa (auditoria de seguridad, hallazgo H-11): 30 envios por
    // minuto por usuario. Muy por encima de cualquier uso humano normal.
    [EnableRateLimiting("OperacionesSensibles")]
    // Limite explicito de tamano de la peticion: un archivo de 10 MB mas los campos
    // del formulario entran holgados en 12 MB, y queda por debajo del limite por
    // defecto de IIS, asi que no hace falta tocar web.config en el servidor.
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Create(RegistroSolucionInput input, IFormFile? archivo, string? tipoAdjunto)
    {
        if (_plataformas.ObtenerActivas().Count == 0)
        {
            ModelState.AddModelError(string.Empty,
                "Todavía no hay plataformas activas en el maestro. Un Administrador debe darlas de alta desde 'Administración de plataformas' antes de poder registrar una solución.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Plataformas = _plataformas.ObtenerActivas();
            return View(input);
        }

        var request = new NuevaSolucionRequest(
            input.Nombre, input.TipoSolucion, input.DescripcionBreve, input.Area, input.Pais,
            input.PlataformaId, input.FechaCreacionSolucion, input.EsProcesoCritico, input.AlcanceUso,
            input.RequiereConexionCentral, input.UsaPii, input.RequierePublicacionInternet,
            input.RequiereInfraDedicada, input.EnlaceAcceso, input.NivelMadurez, input.ClasificacionInformacion,
            input.AudienciaEstimada, input.Etiquetas, input.Comentarios);

        var usuarioId = User.GetUsuarioId();

        // Un Administrador puede registrar a nombre de otra persona. Para cualquier
        // otro usuario estos campos se ignoran y el dueno es siempre quien registra.
        var duenioId = usuarioId;
        var duenioCorreo = string.Empty;
        var duenioEsNuevoEnLaApp = false;
        if (User.GetEsAdministrador() && !string.IsNullOrWhiteSpace(input.DuenioCorreo))
        {
            var resultadoDuenio = _asignacionDuenio.Resolver(input.DuenioCorreo, input.DuenioNombre);
            if (!resultadoDuenio.EsValido)
            {
                ModelState.AddModelError(string.Empty, resultadoDuenio.Error);
                ViewBag.Plataformas = _plataformas.ObtenerActivas();
                return View(input);
            }

            duenioId = resultadoDuenio.Duenio!.UsuarioId;
            duenioCorreo = resultadoDuenio.Duenio.CorreoCorporativo;
            duenioEsNuevoEnLaApp = resultadoDuenio.UsuarioPrecreado;
        }

        var solucion = await _solucionService.RegistrarAsync(
            request, duenioId, duenioId == usuarioId ? null : (int?)usuarioId);

        // Puntos 1 y 2 de retroalimentacion de usuarios (2026-09): tras registrar,
        // se debe ver un mensaje de exito y la pantalla debe quedar en blanco lista
        // para un nuevo registro — en vez de saltar automaticamente al comprobante.
        // El banner lo pinta _Layout.cshtml (TempData["Mensaje"]); el link opcional
        // al comprobante se arma en el Create GET via TempData["UltimoSolucionId"].
        // El adjunto es opcional y NO debe hacer fallar el registro: la solucion ya
        // quedo guardada, asi que si el archivo no pasa la validacion se avisa el
        // motivo y el usuario puede volver a subirlo desde "Ver detalle" — perder el
        // registro completo por un archivo rechazado seria peor.
        var avisoAdjunto = string.Empty;
        if (archivo is not null && archivo.Length > 0)
        {
            var resultado = _adjuntos.Validar(archivo);
            if (resultado.EsValido)
            {
                _soluciones.AgregarAdjunto(new Adjunto
                {
                    SolucionId = solucion.SolucionId,
                    NombreArchivo = resultado.NombreArchivo,
                    TipoAdjunto = AdjuntoService.TipoAdjuntoValido(tipoAdjunto),
                    TipoMime = resultado.TipoMime,
                    TamanoBytes = resultado.Contenido.Length,
                    SubidoPorId = usuarioId
                }, resultado.Contenido);
            }
            else
            {
                avisoAdjunto = resultado.Error;
            }
        }

        // El correo del dueno se confirma DENTRO del mensaje de exito, no como una
        // alerta roja aparte: registrar a nombre de otra persona es el camino feliz de
        // esta funcion, no un problema. Mostrar el correo asignado sigue cumpliendo su
        // proposito real, que es que un error de tipeo se note de inmediato. Una
        // advertencia que se dispara siempre que todo sale bien solo ensena a ignorar
        // las advertencias.
        var mensaje = $"Solución {solucion.CodigoSolucion} registrada exitosamente";
        if (duenioId != usuarioId && duenioCorreo.Length > 0)
        {
            mensaje += $" a nombre de {duenioCorreo}";
            if (duenioEsNuevoEnLaApp)
            {
                mensaje += ", que aún no ha ingresado a la App";
            }
        }

        TempData["Mensaje"] = mensaje + ".";
        TempData["UltimoSolucionId"] = solucion.SolucionId;

        // Rojo SOLO cuando algo falló de verdad.
        if (avisoAdjunto.Length > 0)
        {
            TempData["Error"] = $"La solución se registró, pero el archivo adjunto no se guardó: {avisoAdjunto}";
        }

        return RedirectToAction(nameof(Create));
    }

    // Endpoint de recalculo en vivo — ver 08-reglas-negocio-calculo-riesgo.md
    // seccion 2 y 3. Llamado por JS desde la vista Create.cshtml; nunca se
    // reimplementa la regla en el propio JavaScript.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CalcularRiesgo([FromBody] CalcularRiesgoRequest request)
    {
        var nivel = RiesgoCalculator.CalcularNivelRiesgo(
            request.EsProcesoCritico,
            request.AlcanceUso,
            request.RequiereConexionCentral,
            request.UsaPii,
            request.RequierePublicacionInternet,
            request.RequiereInfraDedicada);

        var proximaRevision = RiesgoCalculator.CalcularProximaRevision(nivel, DateOnly.FromDateTime(DateTime.Today));

        return Json(new
        {
            nivel = nivel.ComoTextoBd(),
            proximaRevision = proximaRevision?.ToString("yyyy-MM-dd") ?? "N/A"
        });
    }

    // ---------------------------------------------------------
    // Adjuntos (2026-09-10). El contenido vive en SQL Server; ver
    // Services/AdjuntoService.cs para los controles de seguridad aplicados
    // a todo archivo que entra.
    // ---------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("OperacionesSensibles")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public IActionResult SubirAdjunto(int id, IFormFile? archivo, string? tipoAdjunto)
    {
        var solucion = _soluciones.ObtenerPorId(id);
        if (solucion is null) return NotFound();
        if (!PuedeEditar(solucion)) return Forbid();

        if (_soluciones.ObtenerAdjuntos(id).Count >= AdjuntoService.MaximoPorSolucion)
        {
            TempData["Error"] = $"Esta solución ya alcanzó el máximo de {AdjuntoService.MaximoPorSolucion} adjuntos.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var resultado = _adjuntos.Validar(archivo);
        if (!resultado.EsValido)
        {
            TempData["Error"] = resultado.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        _soluciones.AgregarAdjunto(new Adjunto
        {
            SolucionId = id,
            NombreArchivo = resultado.NombreArchivo,
            TipoAdjunto = AdjuntoService.TipoAdjuntoValido(tipoAdjunto),
            TipoMime = resultado.TipoMime,
            TamanoBytes = resultado.Contenido.Length,
            SubidoPorId = User.GetUsuarioId()
        }, resultado.Contenido);

        TempData["Mensaje"] = $"Se adjuntó {resultado.NombreArchivo}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Descarga de un adjunto. Se devuelve SIEMPRE como application/octet-stream y
    /// con nombre de descarga (Content-Disposition: attachment), nunca con su tipo
    /// MIME real ni en linea: asi ningun archivo subido por un usuario puede
    /// interpretarse ni ejecutarse dentro del dominio de la App. Es el mismo
    /// razonamiento del hallazgo critico de 11-auditoria-seguridad.md.
    /// </summary>
    [HttpGet]
    public IActionResult DescargarAdjunto(int id)
    {
        var adjunto = _soluciones.ObtenerAdjunto(id);
        if (adjunto is null) return NotFound();

        // El adjunto debe pertenecer a una solucion existente: evita exponer huerfanos.
        if (_soluciones.ObtenerPorId(adjunto.SolucionId) is null) return NotFound();

        var contenido = _soluciones.ObtenerContenidoAdjunto(id);
        if (contenido is null) return NotFound();

        return File(contenido, "application/octet-stream", adjunto.NombreArchivo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EliminarAdjunto(int id)
    {
        var adjunto = _soluciones.ObtenerAdjunto(id);
        if (adjunto is null) return NotFound();

        var solucion = _soluciones.ObtenerPorId(adjunto.SolucionId);
        if (solucion is null) return NotFound();
        if (!PuedeEditar(solucion)) return Forbid();

        _soluciones.EliminarAdjunto(id);

        TempData["Mensaje"] = $"Se eliminó el adjunto {adjunto.NombreArchivo}.";
        return RedirectToAction(nameof(Details), new { id = solucion.SolucionId });
    }

    [HttpGet]
    public IActionResult Comprobante(int id)
    {
        var solucion = _soluciones.ObtenerPorId(id);
        if (solucion is null) return NotFound();

        var vm = new DetalleSolucionViewModel
        {
            Solucion = solucion,
            Plataforma = _plataformas.ObtenerPorId(solucion.PlataformaId)!,
            Duenio = _usuarios.ObtenerPorId(solucion.UsuarioDuenioId)!,
            Registrador = _usuarios.ObtenerPorId(solucion.RegistradoPorId),
            Notificaciones = _soluciones.ObtenerNotificaciones(id).ToList()
        };

        return View(vm);
    }
}
