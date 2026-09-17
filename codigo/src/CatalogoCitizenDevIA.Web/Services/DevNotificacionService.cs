using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Services;

/// <summary>
/// Implementacion DEV de INotificacionService: no hay salida de red hacia
/// Microsoft Graph disponible en este sandbox, asi que el "envio" se deja
/// como evidencia verificable en dos lugares — (1) un registro en
/// NotificacionEnviada (igual que en produccion) y (2) un archivo de texto
/// en App_Data/notificaciones/ con el contenido exacto del comprobante, para
/// poder inspeccionarlo como prueba de la prueba funcional.
///
/// Reemplazar por GraphNotificacionService (Microsoft Graph Mail.Send) en
/// produccion — misma interfaz, mismo punto de enganche en SolucionService.
/// </summary>
public class DevNotificacionService : INotificacionService
{
    private readonly ISolucionRepository _soluciones;
    private readonly ILogger<DevNotificacionService> _logger;
    private readonly string _carpetaNotificaciones;

    public DevNotificacionService(ISolucionRepository soluciones, ILogger<DevNotificacionService> logger, IWebHostEnvironment env)
    {
        _soluciones = soluciones;
        _logger = logger;
        _carpetaNotificaciones = Path.Combine(env.ContentRootPath, "App_Data", "notificaciones");
        Directory.CreateDirectory(_carpetaNotificaciones);
    }

    public async Task<NotificacionEnviada> EnviarComprobanteRegistroAsync(Solucion solucion, Usuario destinatario, Plataforma plataforma)
    {
        var notificacion = new NotificacionEnviada
        {
            SolucionId = solucion.SolucionId,
            TipoNotificacion = "ComprobanteRegistro",
            CorreoDestino = destinatario.CorreoCorporativo,
            Estado = NotificacionEnviada.EstadoEnviado
        };

        try
        {
            var cuerpo = ConstruirCuerpoCorreo(solucion, destinatario, plataforma);
            var archivo = Path.Combine(_carpetaNotificaciones, $"{solucion.CodigoSolucion}.txt");
            await File.WriteAllTextAsync(archivo, cuerpo);

            _logger.LogInformation(
                "Comprobante de registro '{Codigo}' enviado (simulado) a {Correo}. Archivo: {Archivo}",
                solucion.CodigoSolucion, destinatario.CorreoCorporativo, archivo);
        }
        catch (Exception ex)
        {
            notificacion.Estado = NotificacionEnviada.EstadoFallido;
            notificacion.MensajeError = ex.Message;
            _logger.LogError(ex, "Fallo al emitir el comprobante de registro de {Codigo}", solucion.CodigoSolucion);
        }

        _soluciones.RegistrarNotificacion(notificacion);
        return notificacion;
    }

    private static string ConstruirCuerpoCorreo(Solucion solucion, Usuario destinatario, Plataforma plataforma) =>
        $"""
        Para: {destinatario.CorreoCorporativo}
        Asunto: Comprobante de registro — {solucion.CodigoSolucion}

        Hola {destinatario.NombreCompleto},

        Tu solución quedó registrada en el Catálogo de Citizen Development & IA de Yanbal.
        Este correo es tu constancia del registro.

        Código asignado:      {solucion.CodigoSolucion}
        Nombre de la solución: {solucion.Nombre}
        Tipo:                  {solucion.TipoSolucion}
        Plataforma:             {plataforma.Nombre}
        Nivel de riesgo:        {solucion.NivelRiesgo}
        Próxima revisión:       {(solucion.ProximaRevision.HasValue ? solucion.ProximaRevision.Value.ToString("yyyy-MM-dd") : "N/A (Nivel 3)")}
        Estado actual:          {solucion.Estado}
        Fecha de registro:      {solucion.FechaRegistro:yyyy-MM-dd HH:mm} UTC

        Puedes consultar el detalle completo desde el Catálogo en cualquier momento.

        — Catálogo de Citizen Development & IA, Yanbal
        """;
}
