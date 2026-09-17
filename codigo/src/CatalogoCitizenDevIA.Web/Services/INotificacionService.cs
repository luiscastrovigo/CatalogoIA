using CatalogoCitizenDevIA.Web.Models.Entities;

namespace CatalogoCitizenDevIA.Web.Services;

/// <summary>
/// Envio del comprobante de registro por correo (requisito explicito:
/// "notificar al usuario como prueba del registro"). En produccion se
/// implementa contra Microsoft Graph API (Mail.Send) — ver
/// 05-documento-cero-prerequisitos.md seccion 5 y 07-arquitectura-despliegue-componentes.md.
/// </summary>
public interface INotificacionService
{
    Task<NotificacionEnviada> EnviarComprobanteRegistroAsync(Solucion solucion, Usuario destinatario, Plataforma plataforma);
}
