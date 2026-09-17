using Azure.Identity;
using CatalogoCitizenDevIA.Web.Data;
using CatalogoCitizenDevIA.Web.Data.EfCore;
using CatalogoCitizenDevIA.Web.Data.Interfaces;
using CatalogoCitizenDevIA.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Autenticacion real: Microsoft.Identity.Web / Microsoft Entra ID (OIDC) ---
// TenantId/ClientId/ClientSecret NUNCA en appsettings.json — vienen de variables
// de entorno (AzureAd__TenantId, AzureAd__ClientId, AzureAd__ClientSecret), ver
// 10-guia-despliegue-perms220.md seccion 4.
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Sesion de 8 horas (jornada laboral) con renovacion automatica mientras haya
// actividad — antes se usaba el valor por defecto del framework (14 dias
// sin comunicar claramente cuando "expira" la sesion). Al vencer, el propio
// middleware de autenticacion redirige automaticamente al login (comportamiento
// estandar de [Authorize] + cookie de ASP.NET Core, no requiere codigo extra).
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // Endurecimiento explicito de la cookie de sesion (auditoria de seguridad,
    // 2026-09-10) — el framework ya trae HttpOnly=true por defecto para esta
    // cookie, pero se deja explicito para que quede documentado y no dependa
    // de un default que podria cambiar. SecurePolicy.Always exige HTTPS
    // siempre (el sitio ya fuerza HTTPS via UseHttpsRedirection/UseHsts, asi
    // que esto no rompe nada en produccion); SameSite=Lax es compatible con
    // el flujo de redireccion OIDC de Microsoft.Identity.Web.
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Antiforgery (auditoria de seguridad, 2026-09-10): mismo criterio que la
// cookie de sesion — solo por HTTPS, nunca accesible desde JavaScript.
builder.Services.Configure<AntiforgeryOptions>(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services
    .AddControllersWithViews()
    .AddMicrosoftIdentityUI();
builder.Services.AddRazorPages();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EsAdministrador", policy =>
        policy.RequireClaim(AppClaimTypes.EsAdministrador, "True"));

    // Nuevo rol Aprobador (punto 6 de retroalimentacion de usuarios, 2026-09):
    // acceso a Revision y aprobación para Administrador O Aprobador — RequireClaim
    // por si solo no puede expresar un OR entre dos claims distintos.
    options.AddPolicy("PuedeAprobar", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == AppClaimTypes.EsAdministrador && c.Value == "True") ||
            ctx.User.HasClaim(c => c.Type == AppClaimTypes.EsAprobador && c.Value == "True")));
});

// --- Persistencia de llaves de Data Protection (retroalimentacion de usuarios,
// 2026-09-07) ---
// Sin esto, ASP.NET Core genera en memoria/perfil de usuario del Application Pool
// las llaves que cifran la cookie de sesion y el token antifalsificacion de cada
// formulario. Si IIS recicla el proceso (inactividad, o el ciclo periodico de
// reciclaje del pool) esas llaves se pierden y toda sesion/token emitido antes
// del recycle queda indescifrable — eso es lo que producia el error genérico al
// dejar una pantalla abierta mucho tiempo y luego enviar un formulario ("aparece
// un error" en vez de pedir login de nuevo). Variable de entorno nueva,
// `DataProtection__KeysPath` — ver 10-guia-despliegue-perms220.md seccion 4: debe
// apuntar a una carpeta FUERA de la carpeta del sitio (para que el robocopy /MIR
// de cada despliegue no la borre) con permiso de escritura para la identidad del
// Application Pool. Sin la variable configurada (ej. en desarrollo local), cae
// al comportamiento por defecto del framework.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("CatalogoCitizenDevIA");
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// Cifrado adicional de las llaves en reposo (auditoria de seguridad 2026-09-10,
// hallazgo H-08). Sin esto, quien tenga permiso de lectura sobre la carpeta de
// llaves puede leerlas en claro y falsificar cookies de sesion y tokens
// antifalsificacion validos. Se activa SOLO si se configura la variable de
// entorno `DataProtection__CertificateThumbprint` con la huella de un
// certificado instalado en el equipo (almacen Personal de LocalMachine o del
// usuario) cuya clave privada sea accesible para la identidad del Application
// Pool. Mientras la variable no exista, el comportamiento es exactamente el
// actual; y si la variable existe pero el certificado no se encuentra, la App
// arranca igual (no se cae) y deja el aviso en el log.
var dataProtectionThumbprint = builder.Configuration["DataProtection:CertificateThumbprint"];
var certificadoDeLlavesNoEncontrado = false;
if (!string.IsNullOrWhiteSpace(dataProtectionThumbprint))
{
    var certificadoDeLlaves = BuscarCertificadoPorHuella(dataProtectionThumbprint);
    if (certificadoDeLlaves is not null)
    {
        dataProtectionBuilder.ProtectKeysWithCertificate(certificadoDeLlaves);
    }
    else
    {
        certificadoDeLlavesNoEncontrado = true;
    }
}

// --- Persistencia real: EF Core contra SQL Server (PERMS02) ---
// ConnectionStrings__Default viene de variable de entorno (seccion 4 de la guia).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IUsuarioRepository, EfUsuarioRepository>();
builder.Services.AddScoped<IPlataformaRepository, EfPlataformaRepository>();
builder.Services.AddScoped<ISolucionRepository, EfSolucionRepository>();

builder.Services.AddScoped<SolucionService>();
builder.Services.AddScoped<AdjuntoService>();
builder.Services.AddScoped<AsignacionDuenioService>();
builder.Services.AddTransient<IClaimsTransformation, RoleClaimsTransformation>();

// --- Notificaciones reales: Microsoft Graph (Mail.Send) ---
// Mismo Client ID/Secret de la App Registration de arriba (permiso de aplicacion
// Mail.Send con consentimiento de administrador — 10-guia-despliegue-perms220.md,
// seccion 5.1). Si en el futuro se usa una App Registration separada solo para
// el envio de correo, cambiar aqui las tres variables por unas especificas.
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var tenantId = config["AzureAd:TenantId"]
        ?? throw new InvalidOperationException("Falta AzureAd__TenantId.");
    var clientId = config["AzureAd:ClientId"]
        ?? throw new InvalidOperationException("Falta AzureAd__ClientId.");
    var clientSecret = config["AzureAd:ClientSecret"]
        ?? throw new InvalidOperationException("Falta AzureAd__ClientSecret.");

    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    return new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
});
builder.Services.AddScoped<INotificacionService, GraphNotificacionService>();

// --- Reverse proxy (proxynp.unique-yanbal.com) ---
// Sin esto, los redirects de login OIDC se arman con el esquema/host internos de
// PERMS220 en vez del dominio publico, y el login cae en loop de redirect — ver
// 10-guia-despliegue-perms220.md seccion 8 y /areas/yanbal-mobile-dashboard-sso.md
// (mismo sintoma ya visto en dashboard_unified_mobile.asp).
// Auditoria de seguridad 2026-09-10, hallazgo H-07: dejar KnownNetworks y
// KnownProxies vacios hace que la App acepte las cabeceras X-Forwarded-* venga
// de donde venga la peticion, y no solo del reverse proxy real. La correccion
// definitiva es registrar la IP del proxy, dato que debe confirmar el equipo de
// Red. Para que ese dia NO haya que tocar codigo ni recompilar, la lista se lee
// de la variable de entorno `ForwardedHeaders__KnownProxies` (una o varias IPs
// separadas por coma o punto y coma). Mientras la variable no este configurada,
// el comportamiento es identico al actual y la App deja un aviso en el log al
// arrancar. Una IP mal escrita se ignora en vez de tumbar el arranque.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    var proxiesConocidos = builder.Configuration["ForwardedHeaders:KnownProxies"];
    if (!string.IsNullOrWhiteSpace(proxiesConocidos))
    {
        foreach (var textoIp in proxiesConocidos.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (System.Net.IPAddress.TryParse(textoIp.Trim(), out var ipProxy))
            {
                options.KnownProxies.Add(ipProxy);
            }
        }
    }
});

// --- Limite de tasa en operaciones sensibles (auditoria de seguridad, hallazgo
// H-11) --- Middleware nativo de .NET 8, sin paquetes adicionales. El limite es
// deliberadamente holgado: 30 envios por minuto por usuario autenticado esta muy
// por encima de cualquier uso humano real (registrar una solucion o invitar a un
// usuario toma decenas de segundos), asi que no cambia la experiencia de nadie,
// pero acota el abuso automatizado de esos endpoints. Se aplica con el atributo
// [EnableRateLimiting("OperacionesSensibles")] en las acciones correspondientes.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy<string>("OperacionesSensibles", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonimo",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Se recibieron demasiadas solicitudes seguidas. Espere un momento y vuelva a intentarlo.",
            cancellationToken);
    };
});

var app = builder.Build();

// Avisos de arranque de los dos controles que quedan condicionados a una
// variable de entorno (hallazgos H-07 y H-08 de la auditoria de seguridad):
// que su estado quede en el log evita que se olviden silenciosamente.
if (string.IsNullOrWhiteSpace(builder.Configuration["ForwardedHeaders:KnownProxies"]))
{
    app.Logger.LogWarning(
        "ForwardedHeaders: no hay proxies conocidos configurados (variable de entorno " +
        "ForwardedHeaders__KnownProxies). Las cabeceras X-Forwarded-* se aceptan de cualquier " +
        "origen. Ver hallazgo H-07 de la auditoria de seguridad.");
}
if (certificadoDeLlavesNoEncontrado)
{
    app.Logger.LogWarning(
        "DataProtection: se configuro DataProtection__CertificateThumbprint pero no se encontro " +
        "ningun certificado con esa huella; las llaves quedan sin cifrado adicional en reposo. " +
        "Ver hallazgo H-08 de la auditoria de seguridad.");
}

if (!app.Environment.IsDevelopment())
{
    // Antes este handler apuntaba a "/Home/Error" sin que existiera HomeController
    // en el proyecto — cualquier excepcion no controlada caia en una pagina rota
    // en vez de esta pantalla de error (retroalimentacion de usuarios, 2026-09-07;
    // ver Controllers/HomeController.cs, que se agrego para resolver esta ruta).
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Retroalimentacion de usuarios (2026-09-07): si la cookie de sesion o el token
// antifalsificacion de un formulario quedan indescifrables (tipicamente porque se
// perdieron las llaves de Data Protection en un recycle del Application Pool sin
// persistencia — ver comentario mas arriba), en vez de dejar que caiga en la
// pagina de error generica se cierra la sesion y se manda directo al login. Debe
// ir DESPUES de UseExceptionHandler (para interceptar la excepcion antes de que
// llegue a el) y ANTES de UseAuthentication.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (EsFalloDeSesionOToken(ex))
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/MicrosoftIdentity/Account/SignIn?redirectUri="
            + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
    }
});

// Debe ir ANTES de UseAuthentication — ver comentario de ForwardedHeadersOptions arriba.
app.UseForwardedHeaders();

// Cabeceras de seguridad (auditoria de seguridad, 2026-09-10) — ninguna se
// enviaba antes. Van en TODAS las respuestas, antes de UseStaticFiles para
// que tambien cubran CSS/JS/imagenes servidos estaticamente:
//  - X-Content-Type-Options: evita que el navegador "adivine" un tipo MIME
//    distinto al declarado (mitiga ataques de MIME-sniffing).
//  - X-Frame-Options + frame-ancestors: esta App nunca debe cargarse dentro
//    de un <iframe> de otro sitio (mitiga clickjacking).
//  - Referrer-Policy: no filtrar la URL completa (que puede incluir un
//    codigo de solucion en la ruta) a sitios externos al navegar afuera.
//  - Permissions-Policy: esta App no usa camara/microfono/geolocalizacion,
//    se desactivan explicitamente.
//  - Content-Security-Policy: restringe scripts/estilos/fuentes/imagenes a
//    este origen (mas Google Fonts, unico recurso externo real que carga
//    _Layout.cshtml) y bloquea que la pagina se enmarque o envie formularios
//    a otro origen. Desde el cierre del hallazgo H-12, script-src es 'self'
//    SIN 'unsafe-inline': todo el JavaScript de la App vive en archivos bajo
//    wwwroot/js/ y no queda ningun bloque <script> ni manejador onXXX= inline
//    en las vistas, de modo que el navegador rechaza cualquier script que
//    aparezca incrustado en el HTML. 'unsafe-inline' se mantiene solo en
//    style-src, porque las vistas si usan atributos style= en linea.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Debe ir despues de UseRouting (y aqui, ademas, despues de la autenticacion,
// porque el limite se particiona por usuario autenticado).
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Solucion}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

static bool EsFalloDeSesionOToken(Exception ex) =>
    ex is AntiforgeryValidationException
    || ex is CryptographicException
    || ex.InnerException is CryptographicException
    || (ex.Message?.Contains("key ring", StringComparison.OrdinalIgnoreCase) ?? false)
    || (ex.Message?.Contains("protected message", StringComparison.OrdinalIgnoreCase) ?? false);

// Busca un certificado por huella en los almacenes Personal de la maquina y del
// usuario (hallazgo H-08). Devuelve null si no lo encuentra: el llamador decide
// que hacer, y en esta App eso es seguir sin cifrado adicional dejando el aviso
// en el log, nunca impedir el arranque.
static X509Certificate2? BuscarCertificadoPorHuella(string huella)
{
    var huellaLimpia = huella.Replace(" ", string.Empty).Trim().ToUpperInvariant();

    foreach (var ubicacion in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
    {
        try
        {
            using var almacen = new X509Store(StoreName.My, ubicacion);
            almacen.Open(OpenFlags.ReadOnly);
            var encontrados = almacen.Certificates.Find(X509FindType.FindByThumbprint, huellaLimpia, validOnly: false);
            if (encontrados.Count > 0)
            {
                return encontrados[0];
            }
        }
        catch (CryptographicException)
        {
            // Almacen no disponible en este equipo: se intenta con el siguiente.
        }
    }

    return null;
}

public partial class Program { }
