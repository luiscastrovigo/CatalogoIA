using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Models.Entities;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace CatalogoCitizenDevIA.Web.Services;

/// <summary>
/// Implementacion real de INotificacionService: envía el comprobante de
/// registro por Microsoft Graph (Mail.Send), en vez del archivo .txt que usa
/// DevNotificacionService en el sandbox. Ver 05-documento-cero-prerequisitos.md
/// sección 5 y 10-guia-despliegue-perms220.md sección 5 (permiso Mail.Send de
/// tipo aplicación, con consentimiento de administrador, sobre la misma App
/// Registration usada para el login).
///
/// El GraphServiceClient se recibe ya construido (registrado en Program.cs con
/// ClientSecretCredential) — este servicio no conoce credenciales, solo arma y
/// envía el mensaje. El buzón remitente (Notificaciones__RemitenteCorreo) debe
/// ser un buzón real del tenant al que la App Registration tenga delegado el
/// permiso Mail.Send de tipo aplicación.
/// </summary>
public class GraphNotificacionService : INotificacionService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ISolucionRepository _soluciones;
    private readonly ILogger<GraphNotificacionService> _logger;
    private readonly string _remitente;

    public GraphNotificacionService(
        GraphServiceClient graphClient,
        ISolucionRepository soluciones,
        IConfiguration configuration,
        ILogger<GraphNotificacionService> logger)
    {
        _graphClient = graphClient;
        _soluciones = soluciones;
        _logger = logger;
        _remitente = configuration["Notificaciones:RemitenteCorreo"]
            ?? throw new InvalidOperationException(
                "Falta configurar Notificaciones__RemitenteCorreo (buzón remitente del comprobante) — ver 10-guia-despliegue-perms220.md sección 4.");
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
            var mensaje = new Message
            {
                Subject = $"Comprobante de registro — {solucion.CodigoSolucion}",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = ConstruirCuerpoCorreoHtml(solucion, destinatario, plataforma)
                },
                ToRecipients = new List<Recipient>
                {
                    new() { EmailAddress = new EmailAddress { Address = destinatario.CorreoCorporativo } }
                }
            };

            // Envía desde el buzón _remitente (permiso de aplicación Mail.Send —
            // no requiere que ese buzón haya iniciado sesión ni delegar nada).
            await _graphClient.Users[_remitente].SendMail.PostAsync(new SendMailPostRequestBody
            {
                Message = mensaje,
                SaveToSentItems = true
            });

            _logger.LogInformation(
                "Comprobante de registro '{Codigo}' enviado por Microsoft Graph a {Correo}.",
                solucion.CodigoSolucion, destinatario.CorreoCorporativo);
        }
        catch (Exception ex)
        {
            notificacion.Estado = NotificacionEnviada.EstadoFallido;
            notificacion.MensajeError = ex.Message;
            _logger.LogError(ex,
                "Fallo al enviar por Microsoft Graph el comprobante de registro de {Codigo}. " +
                "Revisar que el permiso Mail.Send (aplicación) esté consentido y que {Remitente} sea un buzón válido del tenant.",
                solucion.CodigoSolucion, _remitente);
        }

        _soluciones.RegistrarNotificacion(notificacion);
        return notificacion;
    }

    private static string ConstruirCuerpoCorreoHtml(Solucion solucion, Usuario destinatario, Plataforma plataforma)
    {
        var proximaRevision = solucion.ProximaRevision.HasValue
            ? solucion.ProximaRevision.Value.ToString("yyyy-MM-dd")
            : "N/A (Nivel 3)";

        // Estilo minimo, sin dependencias externas, alineado a 04-identidad-visual.md
        // (naranja/dorado Yanbal) — un correo corporativo no debe cargar CSS externo.
        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;color:#2b2b2b;">
              <p>Hola {System.Net.WebUtility.HtmlEncode(destinatario.NombreCompleto)},</p>
              <p>Tu solución quedó registrada en el <strong>Catálogo de Citizen Development &amp; IA</strong> de Yanbal.
                 Este correo es tu constancia del registro.</p>
              <table style="border-collapse:collapse;margin:12px 0;">
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Código asignado</strong></td><td>{System.Net.WebUtility.HtmlEncode(solucion.CodigoSolucion)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Nombre de la solución</strong></td><td>{System.Net.WebUtility.HtmlEncode(solucion.Nombre)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Tipo</strong></td><td>{System.Net.WebUtility.HtmlEncode(solucion.TipoSolucion)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Plataforma</strong></td><td>{System.Net.WebUtility.HtmlEncode(plataforma.Nombre)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Nivel de riesgo</strong></td><td>{System.Net.WebUtility.HtmlEncode(solucion.NivelRiesgo)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Próxima revisión</strong></td><td>{proximaRevision}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Estado actual</strong></td><td>{System.Net.WebUtility.HtmlEncode(solucion.Estado)}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;color:#8a5a00;"><strong>Fecha de registro</strong></td><td>{solucion.FechaRegistro:yyyy-MM-dd HH:mm} UTC</td></tr>
              </table>
              <p>Puedes consultar el detalle completo desde el Catálogo en cualquier momento.</p>
              <p style="color:#8a5a00;">— Catálogo de Citizen Development &amp; IA, Yanbal</p>
            </div>
            """;
    }
}
