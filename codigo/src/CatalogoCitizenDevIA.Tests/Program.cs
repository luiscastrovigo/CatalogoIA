// Harness de pruebas funcionales — sustituto de xUnit (Microsoft.NET.Test.Sdk,
// xunit, xunit.runner.visualstudio son paquetes NuGet y este sandbox no tiene
// salida de red hacia nuget.org). Es un ejecutable plano, sin dependencias
// externas, que:
//
//   1) Prueba RiesgoCalculator contra la tabla de 10 casos de QA de
//      08-reglas-negocio-calculo-riesgo.md (llamada directa al metodo C#).
//   2) Prueba el ENDPOINT real /Solucion/CalcularRiesgo contra una instancia
//      de la app corriendo en BASE_URL — confirma que la logica cableada end
//      to end (controlador -> RiesgoCalculator) da los mismos resultados.
//   3) Ejecuta el flujo funcional completo por HTTP: login, alta de
//      plataforma (admin), registro de una solucion (usuario), comprobante +
//      notificacion, catalogo, control de acceso admin-only, y el modulo de
//      Gestion de administradores (otorgar / retirar rol, incluida la regla
//      de "no te quedes sin administradores").
//
// Uso: dotnet run --project CatalogoCitizenDevIA.Tests -- http://localhost:5199

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatalogoCitizenDevIA.Web.Services;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5199";
var passed = 0;
var failed = 0;

void Check(string nombre, bool condicion, string? detalle = null)
{
    if (condicion)
    {
        passed++;
        Console.WriteLine($"  [OK]   {nombre}");
    }
    else
    {
        failed++;
        Console.WriteLine($"  [FAIL] {nombre}{(detalle is null ? "" : $" — {detalle}")}");
    }
}

Console.WriteLine("=========================================================");
Console.WriteLine("1) RiesgoCalculator — tabla de casos de QA (08-reglas-negocio-calculo-riesgo.md, sección 4)");
Console.WriteLine("=========================================================");

var casos = new (bool esCritico, string alcance, bool conexionCentral, bool pii, bool publicaInternet, string infra, NivelRiesgo esperado)[]
{
    (false, "Personal", false, false, false, "No", NivelRiesgo.Nivel1),
    (false, "Departamental", false, false, false, "No", NivelRiesgo.Nivel2),
    (false, "Multi-país", false, false, false, "Sandbox personal", NivelRiesgo.Nivel2),
    (true, "Personal", false, false, false, "No", NivelRiesgo.Nivel3),
    (false, "Personal", true, false, false, "No", NivelRiesgo.Nivel3),
    (false, "Personal", false, true, false, "No", NivelRiesgo.Nivel3),
    (false, "Personal", false, false, true, "No", NivelRiesgo.Nivel3),
    (false, "Personal", false, false, false, "Si", NivelRiesgo.Nivel3),
    (false, "Personal", false, false, false, "Entorno compartido", NivelRiesgo.Nivel1),
    (true, "Multi-país", true, true, true, "Si", NivelRiesgo.Nivel3),
};

var i = 1;
foreach (var c in casos)
{
    var resultado = RiesgoCalculator.CalcularNivelRiesgo(c.esCritico, c.alcance, c.conexionCentral, c.pii, c.publicaInternet, c.infra);
    Check($"Caso {i++}: J={c.esCritico} K={c.alcance} L={c.conexionCentral} M={c.pii} N={c.publicaInternet} O={c.infra} -> {c.esperado}",
          resultado == c.esperado, $"obtuvo {resultado}");
}

// Próxima revisión: Nivel 1/2 => +180 días; Nivel 3 => null (N/A)
var hoy = DateOnly.FromDateTime(DateTime.Today);
Check("ProximaRevision Nivel 1 = hoy + 180 días",
      RiesgoCalculator.CalcularProximaRevision(NivelRiesgo.Nivel1, hoy) == hoy.AddDays(180));
Check("ProximaRevision Nivel 3 = N/A (null)",
      RiesgoCalculator.CalcularProximaRevision(NivelRiesgo.Nivel3, hoy) is null);

Console.WriteLine();
Console.WriteLine("=========================================================");
Console.WriteLine($"2) Pruebas funcionales HTTP contra {baseUrl}");
Console.WriteLine("=========================================================");

try
{
    await EjecutarPruebasHttpAsync(baseUrl);
}
catch (Exception ex)
{
    failed++;
    Console.WriteLine($"  [FAIL] Excepción no controlada en pruebas HTTP: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=========================================================");
Console.WriteLine($"RESULTADO: {passed} OK / {failed} FAIL de {passed + failed} verificaciones");
Console.WriteLine("=========================================================");

Environment.Exit(failed == 0 ? 0 : 1);

async Task EjecutarPruebasHttpAsync(string url)
{
    static (HttpClient client, CookieContainer cookies) NuevaSesion(string baseUrl)
    {
        var cookies = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false };
        return (new HttpClient(handler) { BaseAddress = new Uri(baseUrl) }, cookies);
    }

    static async Task<string> ExtraerTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var m = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : throw new InvalidOperationException($"No se encontró antiforgery token en {path}");
    }

    static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string correo, string nombre)
    {
        var token = await ExtraerTokenAsync(client, "/Account/Login");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["correoCorporativo"] = correo,
            ["nombreCompleto"] = nombre
        });
        return await client.PostAsync("/Account/Login", form);
    }

    // --- Sesión anónima: control de acceso ---
    var (anon, _) = NuevaSesion(url);
    var resAnon = await anon.GetAsync("/Solucion");
    Check("Anónimo -> GET /Solucion redirige a login (302)",
          resAnon.StatusCode == HttpStatusCode.Found && (resAnon.Headers.Location?.ToString().Contains("/Account/Login") ?? false),
          $"status={resAnon.StatusCode}, location={resAnon.Headers.Location}");

    // --- Sesión Administrador (usuario semilla) ---
    var (admin, _) = NuevaSesion(url);
    var loginAdmin = await LoginAsync(admin, "luis.castro@yanbal.com", "Luis Castro");
    Check("Login admin semilla (luis.castro@yanbal.com) -> 302 a catálogo",
          loginAdmin.StatusCode == HttpStatusCode.Found);

    var dashAdmin = await admin.GetAsync("/Dashboard");
    Check("Admin -> GET /Dashboard = 200", dashAdmin.StatusCode == HttpStatusCode.OK, $"status={dashAdmin.StatusCode}");

    // Alta de una plataforma (requisito para poder registrar soluciones)
    var tokenPlataforma = await ExtraerTokenAsync(admin, "/Plataforma/Create");
    var formPlataforma = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = tokenPlataforma,
        ["Nombre"] = "Copilot Studio",
        ["Categoria"] = "IA generativa",
        ["Estado"] = "Aprobada",
        ["Comentario"] = "Alta de prueba funcional"
    });
    var resPlataforma = await admin.PostAsync("/Plataforma/Create", formPlataforma);
    Check("Admin crea plataforma 'Copilot Studio' -> 302", resPlataforma.StatusCode == HttpStatusCode.Found);

    var listaPlataformas = await admin.GetStringAsync("/Plataforma/Index");
    Check("La plataforma creada aparece en /Plataforma/Index", listaPlataformas.Contains("Copilot Studio"));

    var plataformaIdMatch = Regex.Match(await admin.GetStringAsync("/Solucion/Create"), "<option value=\"(\\d+)\">Copilot Studio</option>");
    var plataformaId = plataformaIdMatch.Success ? plataformaIdMatch.Groups[1].Value : "1";

    // --- Sesión Usuario registrante (nuevo, alta automática en primer login) ---
    var (usuario, _) = NuevaSesion(url);
    var loginUsuario = await LoginAsync(usuario, "usuario.prueba@yanbal.com", "Usuario Prueba");
    Check("Login usuario nuevo -> 302 (alta automática en primer login)", loginUsuario.StatusCode == HttpStatusCode.Found);

    var dashComoUsuario = await usuario.GetAsync("/Dashboard");
    Check("Usuario registrante -> GET /Dashboard es rechazado (no admin)",
          dashComoUsuario.StatusCode == HttpStatusCode.Found &&
          (dashComoUsuario.Headers.Location?.ToString().Contains("AccesoDenegado") ?? false),
          $"status={dashComoUsuario.StatusCode}, location={dashComoUsuario.Headers.Location}");

    // --- Endpoint real de recálculo en vivo, vía HTTP (no solo el método estático) ---
    var tokenRegistro = await ExtraerTokenAsync(usuario, "/Solucion/Create");
    var payloadRiesgo = JsonSerializer.Serialize(new { esProcesoCritico = false, alcanceUso = "Personal", requiereConexionCentral = false, usaPii = true, requierePublicacionInternet = false, requiereInfraDedicada = "No" });
    var reqRiesgo = new HttpRequestMessage(HttpMethod.Post, "/Solucion/CalcularRiesgo")
    {
        Content = new StringContent(payloadRiesgo, Encoding.UTF8, "application/json")
    };
    reqRiesgo.Headers.Add("RequestVerificationToken", tokenRegistro);
    var respRiesgo = await usuario.SendAsync(reqRiesgo);
    var jsonRiesgo = await respRiesgo.Content.ReadAsStringAsync();
    Check("POST /Solucion/CalcularRiesgo (UsaPii=true) -> Nivel 3 vía HTTP",
          respRiesgo.IsSuccessStatusCode && jsonRiesgo.Contains("Nivel 3"), jsonRiesgo);

    // --- Registro end-to-end de una solución Nivel 3 (dispara notificación) ---
    var formRegistro = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = tokenRegistro,
        ["Nombre"] = "Agente de seguimiento comercial",
        ["TipoSolucion"] = "Agente de IA",
        ["DescripcionBreve"] = "Agente que usa datos de clientes para seguimiento comercial.",
        ["Pais"] = "Perú",
        ["Area"] = "Comercial",
        ["PlataformaId"] = plataformaId,
        ["FechaCreacionSolucion"] = DateTime.Today.ToString("yyyy-MM-dd"),
        ["EsProcesoCritico"] = "false",
        ["AlcanceUso"] = "Multi-país",
        ["RequiereConexionCentral"] = "false",
        ["UsaPii"] = "true",
        ["RequierePublicacionInternet"] = "false",
        ["RequiereInfraDedicada"] = "No"
    });
    var resRegistro = await usuario.PostAsync("/Solucion/Create", formRegistro);
    Check("POST /Solucion/Create (con UsaPii=true) -> 302 a Comprobante",
          resRegistro.StatusCode == HttpStatusCode.Found &&
          (resRegistro.Headers.Location?.ToString().Contains("/Solucion/Comprobante") ?? false),
          $"status={resRegistro.StatusCode}, location={resRegistro.Headers.Location}");

    var comprobanteUrl = resRegistro.Headers.Location!.ToString();
    var htmlComprobante = await usuario.GetStringAsync(comprobanteUrl);
    Check("Comprobante muestra Nivel 3", htmlComprobante.Contains("Nivel 3"));
    Check("Comprobante confirma envío de notificación por correo", htmlComprobante.Contains("Se envió una constancia"));
    var codigoMatch = Regex.Match(htmlComprobante, @"CD-\d{4}-\d{3}");
    Check("Comprobante muestra un código correlativo CD-AAAA-###", codigoMatch.Success, htmlComprobante[..Math.Min(200, htmlComprobante.Length)]);

    if (codigoMatch.Success)
    {
        var codigo = codigoMatch.Value;
        var archivoNotificacion = Directory.GetFiles(AppContext.BaseDirectory, "*.txt", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.EndsWith($"{codigo}.txt"));
        // Busca también relativo al content root típico del proceso del servidor.
        var rutaEsperada = Path.Combine("App_Data", "notificaciones", $"{codigo}.txt");
        Check($"Se generó el archivo de notificación de {codigo} (evidencia del envío)",
              archivoNotificacion is not null || File.Exists(Path.Combine("..", "CatalogoCitizenDevIA.Web", rutaEsperada)) ||
              Directory.GetFiles(".", "*.txt", SearchOption.AllDirectories).Any(f => f.EndsWith($"{codigo}.txt")),
              "revisar App_Data/notificaciones junto al proceso del servidor");
    }

    var htmlCatalogo = await usuario.GetStringAsync("/Solucion");
    Check("La solución registrada aparece en el catálogo", codigoMatch.Success && htmlCatalogo.Contains(codigoMatch.Value));

    // --- Gestión de administradores ---
    var htmlAdminUsuarios = await admin.GetStringAsync("/Administrador/Index");
    var usuarioIdMatch = Regex.Match(htmlAdminUsuarios, "usuario\\.prueba@yanbal\\.com.*?name=\"usuarioId\" value=\"(\\d+)\"", RegexOptions.Singleline);
    Check("El nuevo usuario aparece en Gestión de administradores", usuarioIdMatch.Success);

    if (usuarioIdMatch.Success)
    {
        var usuarioPruebaId = usuarioIdMatch.Groups[1].Value;
        var tokenAdmin = await ExtraerTokenAsync(admin, "/Administrador/Index");

        async Task<HttpResponseMessage> ToggleAsync(HttpClient c, string usuarioId, string token) =>
            await c.PostAsync("/Administrador/ToggleAdmin", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["usuarioId"] = usuarioId
            }));

        var otorgar = await ToggleAsync(admin, usuarioPruebaId, tokenAdmin);
        Check("Admin otorga rol de Administrador al usuario de prueba", otorgar.StatusCode == HttpStatusCode.Found);

        var dashComoNuevoAdmin = await usuario.GetAsync("/Dashboard");
        Check("El usuario recién promovido accede a /Dashboard SIN reingresar (claim en vivo)",
              dashComoNuevoAdmin.StatusCode == HttpStatusCode.OK, $"status={dashComoNuevoAdmin.StatusCode}");

        // Retira el rol al admin original (ya no es el único admin) -> debe permitirse
        var tokenAdmin2 = await ExtraerTokenAsync(admin, "/Administrador/Index");
        var adminIdMatch = Regex.Match(await admin.GetStringAsync("/Administrador/Index"),
            "luis\\.castro@yanbal\\.com.*?name=\"usuarioId\" value=\"(\\d+)\"", RegexOptions.Singleline);
        if (adminIdMatch.Success)
        {
            var retirar = await ToggleAsync(admin, adminIdMatch.Groups[1].Value, tokenAdmin2);
            Check("Se puede retirar el rol del admin original (ya no es el único)", retirar.StatusCode == HttpStatusCode.Found);

            var dashAdminOriginalTrasRetiro = await admin.GetAsync("/Dashboard");
            Check("Admin original pierde acceso a /Dashboard inmediatamente tras retirar su rol",
                  dashAdminOriginalTrasRetiro.StatusCode == HttpStatusCode.Found);
        }

        // Ahora el usuario de prueba es el ÚNICO administrador: debe bloquearse su auto-revocación.
        var tokenUsuarioAdmin = await ExtraerTokenAsync(usuario, "/Administrador/Index");
        var autoRevocar = await ToggleAsync(usuario, usuarioPruebaId, tokenUsuarioAdmin);
        var htmlTrasAutoRevocar = await usuario.GetStringAsync("/Administrador/Index");
        Check("No se permite auto-revocar al ÚNICO administrador activo",
              autoRevocar.StatusCode == HttpStatusCode.Found && htmlTrasAutoRevocar.Contains("Administrador</span>") &&
              htmlTrasAutoRevocar.Contains("usuario.prueba@yanbal.com"));
    }
}
