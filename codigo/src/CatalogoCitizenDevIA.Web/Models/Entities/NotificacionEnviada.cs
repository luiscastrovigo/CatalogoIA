namespace CatalogoCitizenDevIA.Web.Models.Entities;

/// <summary>
/// Auditoria del comprobante de registro por correo (soporta el requisito de
/// "prueba del registro"). En produccion lo envia NotificacionService via
/// Microsoft Graph (Mail.Send); en este sandbox lo emite DevNotificacionService.
/// Ver 06-diseno-bd-sql-server.md y 07-arquitectura-despliegue-componentes.md.
/// </summary>
public class NotificacionEnviada
{
    public const string EstadoEnviado = "Enviado";
    public const string EstadoFallido = "Fallido";

    public int NotificacionId { get; set; }
    public int SolucionId { get; set; }
    public string TipoNotificacion { get; set; } = "ComprobanteRegistro";
    public string CorreoDestino { get; set; } = string.Empty;
    public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = EstadoEnviado;
    public string? MensajeError { get; set; }
}
