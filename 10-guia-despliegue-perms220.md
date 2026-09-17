---
title: Guía de despliegue — Catálogo Citizen Development IA en PERMS220
proyecto: Catalogo App IA
version: "1.3"
fecha: 2026-09-07
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
depende_de: 05-documento-cero-prerequisitos.md, 06-diseno-bd-sql-server.md, 07-arquitectura-despliegue-componentes.md, 09-estado-implementacion-pruebas.md
servidor_aplicacion: PERMS220 (unique-yanbal.com)
servidor_base_datos: PERMS02 — YanbalCitizenDevIA
---

# Guía de despliegue — Catálogo Citizen Development IA en PERMS220

Esta guía resuelve el punto que quedaba "a definir" en `05-documento-cero-prerequisitos.md`
(sección 1): el servidor de aplicación es **PERMS220** (`perms220.unique-yanbal.com`), el mismo
servidor donde hoy corre `dashticketsSDM`. Es un despliegue **paso a paso** pensado para que lo
ejecuten en conjunto Infraestructura/IIS, el DBA de PERMS02 y Arquitectura de Aplicaciones — se
indica quién hace cada parte.

## 0. Antes de empezar — quién hace qué

| Rol | Responsable de |
|---|---|
| Infraestructura / IIS (PERMS220) | Secciones 1, 2, 3, 8 |
| DBA (PERMS02) | Sección 6 (ya cubierta en detalle en `06-diseno-bd-sql-server.md`) |
| IT Seguridad / Entra ID | Sección 5 |
| Red / equipo del reverse proxy | Sección 7 |
| Arquitectura de Aplicaciones (Luis Castro) | Secciones 4, 9, 10 — publica el código y valida |

No hay dependencias estrictas de orden entre las secciones 1-7: pueden avanzar en paralelo.
La sección 8 (primer despliegue) sí necesita que las anteriores estén listas.

## 1. Preparar PERMS220 para .NET 8

PERMS220 hoy aloja aplicaciones **Classic ASP** (`dashticketsSDM`, etc.), por lo que el
runtime de .NET 8 probablemente no está instalado todavía. Confirmar y, si falta, instalar:

1. Verificar la versión de Windows Server de PERMS220 (soportada por .NET 8: Windows Server
   2016 en adelante).
2. Descargar e instalar el **ASP.NET Core Hosting Bundle 8.0 (LTS)** desde
   `https://dotnet.microsoft.com/download/dotnet/8.0` (instalador `dotnet-hosting-8.0.x-win.exe`) —
   este instalador trae el runtime de ASP.NET Core **y** el módulo `ANCM` (ASP.NET Core Module
   v2) que IIS necesita para poder alojar la app.
3. Reiniciar IIS después de instalar (`net stop was /y` seguido de `net start w3svc`, o reiniciar
   el servidor si el estándar de Yanbal lo pide).
4. Confirmar la instalación: `dotnet --info` desde una consola en el servidor debe listar
   `Microsoft.AspNetCore.App 8.0.x` entre los runtimes instalados.

## 2. Crear el sitio y el Application Pool en IIS

A diferencia de `dashticketsSDM` (Classic ASP, corre bajo el App Pool clásico existente), esta
App necesita su **propio Application Pool** en modo "No Managed Code" (el CLR de .NET Core no
lo gestiona IIS, lo gestia el propio proceso vía ANCM):

1. En IIS Manager, crear un Application Pool nuevo: `CatalogoCitizenDevIA-AppPool`.
   - .NET CLR version: **No Managed Code**.
   - Modelo de proceso: identidad = la cuenta de servicio de dominio dedicada a esta App
     (ver sección 5 de `05-documento-cero-prerequisitos.md` — no reutilizar la cuenta de otro
     proyecto).
   - Modo de reciclaje: alinear con el estándar ya usado para los demás sitios de PERMS220.
2. Crear la carpeta física, por ejemplo `D:\wwwroot\Monitoreo\CatalogoCitizenDevIA\` (seguir la
   convención de rutas que ya use PERMS220 para sus otros sitios).
3. Crear el sitio/aplicación en IIS apuntando a esa carpeta, con el Application Pool del
   paso 1. Dos opciones válidas, a decidir con el equipo de Infraestructura según cómo esté
   organizado hoy PERMS220:
   - **Aplicación IIS bajo el sitio existente** (ej. `perms220.unique-yanbal.com/CatalogoIA/`) —
     mismo patrón de URL por subcarpeta que usa `dashticketsSDM` hoy. **Esta es la opción que
     se usó en el despliegue real.**
   - **Sitio IIS independiente** con su propio binding/hostname (ej. `catalogoia.unique-yanbal.com`) —
     más limpio para una app con login propio, pero requiere un registro DNS nuevo.

   Cualquiera de las dos funciona con el código tal como está — solo cambia el valor del
   `Redirect URI` de la sección 5 y, si se usa subcarpeta, agregar `<base href="/CatalogoIA/">`
   ya está resuelto porque los enlaces de la app son todos relativos vía `asp-controller`/`asp-action`.

   **Advertencia real de este despliegue**: al copiar la publicación, copiar el **contenido**
   de la carpeta `publish\` a la raíz del sitio — nunca la carpeta `publish\` completa ni la
   carpeta raíz del proyecto (con `src\` al lado). Copiar la estructura equivocada produjo un
   HTTP 403 genérico porque IIS nunca llegaba a pasarle la petición a ANCM.

## 3. Publicar y copiar la aplicación

Desde una máquina con el SDK de .NET 8 (o desde el propio PERMS220 si tiene el SDK, no solo el
runtime). **Ojo con la carpeta desde la que corres el comando** — es la causa más común de error
en este paso (`MSB1009: El archivo de proyecto no existe`):

- Si estás parado en la **carpeta raíz del código** (donde está `CatalogoCitizenDevIA.sln`,
  normalmente `...\codigo\`, con una subcarpeta `src\` al lado):

  ```powershell
  dotnet publish src\CatalogoCitizenDevIA.Web -c Release -o publish\
  ```

- Si en cambio ya entraste a la **carpeta del proyecto** (`...\codigo\src\CatalogoCitizenDevIA.Web\`
  — lo sabes porque un `dir` ahí muestra `CatalogoCitizenDevIA.Web.csproj` directamente, sin una
  subcarpeta `src\`), quita la ruta del medio:

  ```powershell
  dotnet publish -c Release -o publish\
  ```

Ambos comandos generan el mismo resultado — una publicación **framework-dependent** (usa el runtime que ya instalamos en el
paso 1, no incluye el runtime completo — carpeta más liviana). Copiar el **contenido** de
`publish\` a la carpeta física del sitio en PERMS220 (`D:\wwwroot\Monitoreo\CatalogoCitizenDevIA\`),
por ejemplo con `robocopy publish\ D:\wwwroot\Monitoreo\CatalogoCitizenDevIA /MIR /XF web.config`
(el `/XF web.config` evita pisar las variables de entorno ya configuradas — ver sección 4).

**Importante (2026-09-07):** `robocopy /MIR` **borra** en el destino cualquier archivo o carpeta
que no exista en `publish\` — por eso la carpeta de llaves de Data Protection de la sección 4
(`DataProtection__KeysPath`) debe vivir **fuera** de `D:\wwwroot\Monitoreo\CatalogoCitizenDevIA\`,
nunca como subcarpeta del sitio, o cada despliegue la borraría.

Verificar que quedó el archivo `web.config` generado automáticamente por `dotnet publish` — es
el que le dice a IIS/ANCM cómo arrancar el proceso .NET; si falta, la app no arranca.

## 4. Configurar secretos y cadena de conexión (sin tocar código)

Por decisión de arquitectura (`07-arquitectura-despliegue-componentes.md`, sección 4), **nada
de esto va en `appsettings.json` en texto plano**. IIS Manager (la consola gráfica, `inetmgr`)
**no tiene** una pantalla de "Environment Variables" para el Application Pool — esa opción no
existe en la interfaz gráfica en ninguna versión de Windows Server. Las dos formas que sí
funcionan:

**Variables necesarias, cualquiera sea el método:**

| Variable | Valor |
|---|---|
| `ConnectionStrings__Default` | Cadena de conexión a `YanbalCitizenDevIA` en PERMS02, con la cuenta de servicio de la sección 5 de `05-documento-cero-prerequisitos.md` |
| `AzureAd__TenantId` | Tenant ID de Yanbal en Entra ID (sección 5 de esta guía) |
| `AzureAd__ClientId` | Client ID de la App Registration |
| `AzureAd__ClientSecret` | Secreto/certificado de la App Registration — si Yanbal ya usa Azure Key Vault u otro almacén de secretos corporativo, usar ese en vez de una variable de entorno plana |
| `Notificaciones__RemitenteCorreo` | Buzón remitente del comprobante de registro (ej. `no-reply-catalogoia@yanbal.com`) |
| `DataProtection__KeysPath` | Carpeta **fuera** de `D:\wwwroot\Monitoreo\CatalogoCitizenDevIA\` donde persistir las llaves de cifrado de sesión/antifalsificación — ej. `D:\AppData\CatalogoCitizenDevIA\dp-keys\` (nueva, 2026-09-07 — ver nota debajo de la tabla) |

Los nombres usan doble guion bajo (`__`) porque así es como `IConfiguration` de .NET anida
secciones (`AzureAd__ClientId` llena `AzureAd:ClientId`) — esto lo resuelve automáticamente
Microsoft.Identity.Web / EF Core al leer la configuración, sin código adicional.

**Sobre `DataProtection__KeysPath` (agregada 2026-09-07):** ASP.NET Core cifra la cookie de
sesión y el token antifalsificación de cada formulario con llaves de Data Protection. Sin esta
variable, esas llaves se generan en memoria/perfil de usuario del Application Pool — si IIS
recicla el proceso (por inactividad o por el ciclo periódico de reciclaje), las llaves se
pierden y toda sesión o token emitido antes del recycle queda indescifrable. Esto era la causa
real de que, al dejar una pantalla abierta mucho tiempo y luego enviar un formulario, apareciera
un error en vez de pedir el login de nuevo (retroalimentación de usuarios, 2026-09-07 — ver
`09-estado-implementacion-pruebas.md` sección 9). Antes de crear el sitio:

1. Crear una carpeta dedicada **fuera** de la carpeta del sitio, ej. `D:\AppData\CatalogoCitizenDevIA\dp-keys\`.
2. Dar permiso de **lectura y escritura** sobre esa carpeta a la identidad del Application Pool
   (la misma cuenta de servicio de la sección 2, paso 1).
3. Configurar `DataProtection__KeysPath` con esa ruta, igual que las demás variables de esta
   sección (Opción A o B).

Sin esta variable configurada, la App sigue funcionando (cae al comportamiento por defecto del
framework), pero queda expuesta al mismo problema que se corrigió el 2026-09-07 en cuanto el
Application Pool recicle una vez.

### Opción A — en `web.config` (funciona en cualquier versión de Windows Server; la que se usa hoy en PERMS220)

`dotnet publish` genera un `web.config` en la carpeta publicada, con un bloque `<aspNetCore>`.
**Ese bloque sale autocerrado** (termina en `... hostingModel="inprocess" />`) — hay que
quitarle el autocierre, convertirlo en apertura/cierre normal, y meter el
`<environmentVariables>` en medio. Antes:

```xml
<aspNetCore processPath="dotnet" arguments=".\CatalogoCitizenDevIA.Web.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
```

Después:

```xml
<aspNetCore processPath="dotnet" arguments=".\CatalogoCitizenDevIA.Web.dll"
            stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ConnectionStrings__Default" value="Server=PERMS02;Database=YanbalCitizenDevIA;User Id=...;Password=...;TrustServerCertificate=True;" />
    <environmentVariable name="AzureAd__TenantId" value="..." />
    <environmentVariable name="AzureAd__ClientId" value="..." />
    <environmentVariable name="AzureAd__ClientSecret" value="..." />
    <environmentVariable name="Notificaciones__RemitenteCorreo" value="no-reply-catalogoia@yanbal.com" />
    <environmentVariable name="DataProtection__KeysPath" value="D:\AppData\CatalogoCitizenDevIA\dp-keys\" />
  </environmentVariables>
</aspNetCore>
```

> **⚠️ Advertencia real de este despliegue (2026-09-04, recurrente 2026-09-07)** — este bloque
> se ha roto varias veces en producción, siempre con el mismo síntoma
> (`System.ArgumentException: Format of the initialization string does not conform to
> specification`) pero por causas distintas:
> 1. **Llaves de más**: alguien pegó el valor de `ConnectionStrings__Default` envuelto en
>    llaves dobles (`value="{{Server=...;}}"`) — las llaves no van, ni una sola, en ninguna
>    parte del valor. Esto no es sintaxis válida de ADO.NET.
> 2. **Bloque incompleto**: el `<environmentVariables>` quedó a medias (faltando por ejemplo
>    `AzureAd__ClientId`), lo que en realidad produce un error distinto:
>    `IDW10106: The 'ClientId' option must be provided`.
> 3. **Carácter especial sin escapar en la contraseña** (causa confirmada el 2026-09-07): si la
>    contraseña de la cuenta de servicio contiene `;`, `'`, `"` u otro separador de ADO.NET tal
>    cual, el parser corta la cadena de conexión ahí en medio y lanza el mismo
>    `ArgumentException` (el índice que reporta el mensaje señala el punto exacto del corte,
>    normalmente dentro del segmento `Password=...`). La regla de ADO.NET: si la contraseña
>    tiene alguno de esos caracteres, todo el valor va entre comillas simples y cualquier
>    comilla simple interna se duplica (`Password='mi;clave'''`); si no tiene ninguno, va sin
>    comillas.
>
> Los tres se repitieron más de una vez porque, al perderse o corromperse el bloque en cada
> `dotnet publish`, se volvía a pegar desde una copia vieja o incompleta guardada en otro lado.
> **Recomendación operativa mientras se siga usando la Opción A**:
> 1. Guardar el bloque `<environmentVariables>` ya verificado y funcionando en un archivo de
>    texto aparte (por ejemplo `web.config.env-vars.snippet.txt` en la carpeta `codigo\`), y
>    usar siempre esa copia como fuente — nunca retipear los valores a mano.
> 2. Al reinsertarlo después de un `dotnet publish`, usar Buscar-y-reemplazar en vez de
>    reescribir la línea completa, y revisar visualmente que ningún valor tenga `{` ni `}`.
> 3. Antes de reciclar el Application Pool, releer el `web.config` guardado y confirmar
>    carácter por carácter que las 6 variables están completas y que la contraseña, si tiene
>    caracteres especiales, está correctamente entre comillas simples.
> 4. Considerar copiar con `/XF web.config` en el `robocopy` de la sección 3 para no perder el
>    bloque en cada despliegue (aunque esto no ayuda si el cambio es al propio contenido de las
>    variables, solo evita que un republish borre lo ya configurado).

Importante:
- **Reciclar el Application Pool** (o `iisreset`) después de guardar el `web.config` — IIS/ANCM
  solo lee las variables de entorno al arrancar el proceso, no en caliente.
- **Restringir permisos NTFS** de `web.config` (y de toda la carpeta del sitio) para que solo la
  identidad del Application Pool y los administradores de PERMS220 puedan leerlo — al quedar en
  un archivo de texto, la protección real es el permiso de archivo, no el formato.
- `dotnet publish` **regenera `web.config` en cada despliegue** y borra este bloque si se
  vuelve a publicar sin cuidado.

### Opción B — variables de entorno reales del Application Pool (Windows Server 2019 / IIS 10+, vía PowerShell) — **NO disponible hoy en PERMS220**

Esta sí es a nivel de Application Pool (persiste aunque se vuelva a publicar la app), pero
**se intentó en este despliegue (2026-09-04) y no funcionó en PERMS220**: ni el módulo de
PowerShell `WebAdministration` (`Import-Module WebAdministration` falla con
`FileNotFoundException`) ni `appcmd.exe` (`C:\WINDOWS\system32\inetsrv\appcmd.exe` no existe en
este servidor) están disponibles. Esto indica que a PERMS220 nunca se le instaló el rol de
Windows **"IIS Management Scripts and Tools"** (`Web-Scripting-Tools`) — solo tiene la consola
gráfica de IIS Manager (`Web-Mgmt-Console`).

Para habilitar esta opción en el futuro, Infraestructura debe instalar ese rol primero:

```powershell
Install-WindowsFeature Web-Scripting-Tools
```

Una vez instalado, el script de referencia es:

```powershell
Import-Module WebAdministration
$pool = "CatalogoCitizenDevIA-AppPool"

Set-ItemProperty -Path "IIS:\AppPools\$pool" -Name "environmentVariables" -Value @(
    @{name="ConnectionStrings__Default"; value="Server=PERMS02;Database=YanbalCitizenDevIA;..."},
    @{name="AzureAd__TenantId"; value="..."},
    @{name="AzureAd__ClientId"; value="..."},
    @{name="AzureAd__ClientSecret"; value="..."},
    @{name="Notificaciones__RemitenteCorreo"; value="no-reply-catalogoia@yanbal.com"},
    @{name="DataProtection__KeysPath"; value="D:\AppData\CatalogoCitizenDevIA\dp-keys\"}
)

# Verificar lo que quedó guardado:
Get-ItemProperty -Path "IIS:\AppPools\$pool" -Name "environmentVariables"
```

**Mientras ese rol no esté instalado, la Opción A es la única disponible** — seguir la
recomendación operativa de la advertencia de arriba para reducir el riesgo de errores al
reinsertar el bloque en cada publicación.

## 5. Registrar la App en Entra ID

Responsable: IT Seguridad / administrador de Entra ID (ver `05-documento-cero-prerequisitos.md`,
sección 3).

1. Crear una **App Registration** nueva en el tenant de Yanbal, nombre sugerido
   `Catálogo Citizen Development IA`.
2. Tipo de cuenta: solo el tenant de Yanbal (single-tenant).
3. Redirect URI: `https://<dominio elegido en la sección 2>/signin-oidc` (usar HTTPS siempre,
   nunca HTTP, incluso si el reverse proxy termina el TLS antes de llegar a PERMS220 — ver
   sección 7). **Registrar este URI bajo la plataforma "Web"** en Authentication — no bajo
   "Single-page application" ni "Mobile and desktop applications"; en la App Registration real
   de este proyecto, un Redirect URI mal ubicado de plataforma produjo `AADSTS900971: No reply
   address provided` aunque el valor de texto fuera idéntico al correcto.
4. En **Authentication → Implicit grant and hybrid flows** (en la UI "Authentication Preview"
   puede estar bajo una pestaña "Configuración"/"Settings" aparte), marcar **"ID tokens (used
   for implicit and hybrid flows)"** — sin esto, Microsoft.Identity.Web falla con
   `AADSTS700054: response_type 'id_token' is not enabled for the application`.
5. Generar un Client Secret (o, mejor, un certificado) y entregarlo de forma segura a
   Arquitectura de Aplicaciones para cargarlo como variable de entorno protegida (sección 4).
6. No se necesita configurar ningún **grupo de seguridad** ni permiso de "App roles" para el
   rol Administrador — esa decisión ya se tomó y el rol se gestiona dentro de la propia App
   (`05-documento-cero-prerequisitos.md`, sección 3). Sí se necesita el permiso delegado o de
   aplicación **`Mail.Send`** de Microsoft Graph si el envío del comprobante de registro se
   hace con esta misma App Registration (recomendado, ver sección 5.1).

Nota: el log de inicio de sesión de Entra ID para este flujo muestra "Recurso: Microsoft
Graph" incluso en un login normal — es el permiso delegado `User.Read` que Azure agrega por
defecto a todo App Registration nuevo, no indica un problema.

### 5.1 Permiso `Mail.Send` de Microsoft Graph — paso a paso

1. En la misma App Registration, ir a **API permissions → Add a permission → Microsoft Graph →
   Application permissions** (no delegated) → buscar y marcar **`Mail.Send`**.
2. Click en **"Grant admin consent for Yanbal"** (requiere un administrador global o de
   aplicaciones del tenant) — sin este consentimiento, el envío falla en tiempo de ejecución
   aunque el permiso esté agregado.
3. (Recomendado, no bloqueante) Acotar el alcance con una **Application Access Policy** en
   Exchange Online, para que esta App solo pueda enviar como el buzón remitente configurado y
   no como cualquier buzón del tenant:

   ```powershell
   New-ApplicationAccessPolicy -AppId "<Client ID de la App Registration>" `
       -PolicyScopeGroupId "no-reply-catalogoia@yanbal.com" `
       -AccessRight RestrictAccess `
       -Description "Catalogo Citizen Development IA - solo puede enviar como no-reply-catalogoia@yanbal.com"
   ```

## 6. Base de datos en PERMS02

Ya cubierto en detalle en `06-diseno-bd-sql-server.md`. Resumen de lo que el DBA debe tener
listo antes del primer despliegue:

1. Base de datos `YanbalCitizenDevIA` creada con `sql/01_schema_YanbalCitizenDevIA.sql`.
2. `sql/02_seed_inicial.sql` ejecutado, con el `AzureAdObjectId` real de Luis Castro (obtenerlo
   de Entra ID una vez creada la App Registration de la sección 5 — no dejar el `NEWID()` de
   ejemplo).
3. Cuenta de servicio de la aplicación creada con permisos `db_datareader`, `db_datawriter` y
   `EXECUTE` sobre `dbo.sp_GenerarCodigoSolucion` — nunca `db_owner` (`06-diseno-bd-sql-server.md`,
   sección 5 / `07-arquitectura-despliegue-componentes.md`, sección 4).

## 7. Reglas de red / firewall

| Origen | Destino | Puerto | Motivo |
|---|---|---|---|
| PERMS220 | PERMS02 | 1433 (TDS) | Conexión SQL Server de la App |
| PERMS220 | `login.microsoftonline.com` | 443 (HTTPS) | Login OIDC contra Entra ID |
| PERMS220 | `graph.microsoft.com` | 443 (HTTPS) | Envío del comprobante por Microsoft Graph (`Mail.Send`) |
| Usuarios / reverse proxy | PERMS220 | 443 (HTTPS) | Acceso de los usuarios a la App |

Si PERMS220 sale a Internet a través de un proxy corporativo (no directo), configurar las
variables de entorno estándar de .NET (`HTTP_PROXY`/`HTTPS_PROXY`) en el Application Pool para
que el login contra Entra ID y el envío por Graph puedan salir.

## 8. Reverse proxy y dominio público

PERMS220 ya sirve al menos un dashboard (`dashboard_unified_mobile.asp`) a través de un reverse
proxy externo (`proxynp.unique-yanbal.com`) que reescribe encabezados `X-Forwarded-Host` /
`X-Forwarded-Proto`. Si esta App se publica detrás del mismo esquema:

1. Coordinar con el equipo que administra ese reverse proxy para agregar la ruta/dominio de
   esta App, igual que se hizo para los dashboards existentes.
2. **Importante — requisito específico de ASP.NET Core detrás de un reverse proxy**: sin esto,
   los redirects de login de Entra ID se arman con el esquema/host internos de PERMS220
   (`http://perms220...`) en vez del dominio público (`https://...`), y el login falla o cae en
   un loop de redirect. Se agregó en `Program.cs`, antes de `app.UseAuthentication()`:

   ```csharp
   var forwardedHeadersOptions = new ForwardedHeadersOptions
   {
       ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
   };
   forwardedHeadersOptions.KnownNetworks.Clear();
   forwardedHeadersOptions.KnownProxies.Clear(); // o agregar la IP del proxy con KnownProxies.Add(...)
   app.UseForwardedHeaders(forwardedHeadersOptions);
   ```

3. Confirmar con el equipo de red que el proxy efectivamente envía `X-Forwarded-Host` y
   `X-Forwarded-Proto` — en el proyecto `dashboard_unified_mobile.asp` esos encabezados
   faltaban inicialmente y el SSO no funcionaba hasta agregarlos en la configuración de Nginx;
   vale la pena confirmarlo de entrada para no repetir ese diagnóstico.

## 9. Primer despliegue y validación

1. Arrancar el sitio en IIS (`Start` sobre el sitio/aplicación).
2. Verificar que responde y redirige a Entra ID para el login.
3. Validar el flujo completo manualmente una vez, igual que se probó en el sandbox
   (`09-estado-implementacion-pruebas.md`): login, alta de una plataforma real como
   Administrador, registro de una solución, confirmar que llega el correo de comprobante,
   verificar que aparece en el catálogo y en el dashboard.
4. Opcional pero recomendado: apuntar el harness `CatalogoCitizenDevIA.Tests` (entregado en el
   código fuente) contra la URL real de PERMS220 como prueba de regresión post-despliegue —
   requiere adaptarlo primero, ya que se escribió contra el login de desarrollo de la primera
   ronda y ya no aplica con el login real de Entra ID.

**Estado real (2026-09-04): pasos 1-2 completados y validados en producción.** Pendiente
completar el punto 3 (probar el envío de correo por Graph de punta a punta) — ver
`09-estado-implementacion-pruebas.md` sección 6.

## 10. Problemas comunes al desplegar (troubleshooting)

| Síntoma | Causa probable | Revisar |
|---|---|---|
| HTTP 502.5 / "Process Failure" | Falta el Hosting Bundle o el `web.config` no llegó en la publicación | Sección 1 y 3 |
| HTTP 500.30 al arrancar | Variable de entorno de conexión a BD mal escrita o inaccesible | Sección 4 |
| HTTP 403 genérico al cargar el sitio | Se copió la carpeta `publish\` completa (o la raíz del proyecto) en vez de su contenido a la raíz del sitio | Sección 2 |
| `System.ArgumentException: Format of the initialization string does not conform to specification` — llaves `{ }` de más | El valor de `ConnectionStrings__Default` en `web.config` quedó envuelto en llaves dobles alrededor de todo el valor | Sección 4, Opción A (causa 1) |
| `System.ArgumentException: Format of the initialization string does not conform to specification` — con un índice de carácter específico en el mensaje | Carácter especial (`;`, `'`, `"`) sin escapar dentro del valor de `Password=...` en `ConnectionStrings__Default` — falta encerrar la contraseña entre comillas simples (y duplicar comillas simples internas) | Sección 4, Opción A (causa 3) — **ocurrió y se corrigió el 2026-09-07** |
| `IDW10106: The 'ClientId' option must be provided` | El bloque `<environmentVariables>` del `web.config` quedó incompleto — falta `AzureAd__ClientId` (o se perdió en un `dotnet publish` posterior) | Sección 4 |
| Al dejar una pantalla abierta mucho tiempo y luego enviar un formulario o hacer clic en una acción, aparece una página de error genérica en vez de pedir el login de nuevo | Dos causas combinadas, corregidas el 2026-09-07: (a) faltaba `Controllers/HomeController.cs` — el `app.UseExceptionHandler("/Home/Error")` de `Program.cs` apuntaba a una ruta inexistente, así que **cualquier** excepción no controlada mostraba una página rota; (b) sin `DataProtection__KeysPath` configurada, un recycle del Application Pool invalida la cookie de sesión y los tokens antifalsificación ya emitidos. Ahora `Program.cs` detecta ese caso puntual y cierra sesión + redirige al login automáticamente | Sección 4 (variable nueva) y `09-estado-implementacion-pruebas.md` sección 9 |
| `Violation of UNIQUE KEY constraint 'UQ_Usuario_AzureAdObjectId'` al invitar a un administrador nuevo | Bug ya corregido en el código (`AdministradorController.Invitar` usaba `Guid.Empty` compartido) — confirmar que el binario desplegado incluye el fix con `Guid.NewGuid()` | `09-estado-implementacion-pruebas.md` sección 7 |
| Login contra Entra ID en loop infinito de redirect | Faltan los encabezados `X-Forwarded-*` o el Redirect URI no coincide exactamente | Secciones 5 y 8 |
| `AADSTS700054: response_type 'id_token' is not enabled` | Falta marcar "ID tokens" en Implicit grant and hybrid flows | Sección 5 |
| `AADSTS900971: No reply address provided` | El Redirect URI está registrado bajo la plataforma equivocada (no "Web"), o el `AzureAd__ClientId` configurado no corresponde a esa App Registration | Sección 5 |
| "Login failed for user" al conectar a PERMS02 | Cuenta de servicio sin permisos o regla de firewall 1433 no abierta | Secciones 6 y 7 |
| El comprobante de registro no llega por correo | Permiso `Mail.Send` no otorgado/consentido en la App Registration | Sección 5.1 |

## 11. Documentación relacionada que se actualizó con esta decisión

Esta guía resuelve la decisión de servidor de aplicación que quedaba pendiente. Se actualizaron
en consecuencia:

- `05-documento-cero-prerequisitos.md`, sección 1 — servidor de aplicación: **PERMS220** (ya no "a definir").
- `07-arquitectura-despliegue-componentes.md`, vista física de despliegue — el placeholder `PERMSxx` se reemplazó por `PERMS220`.
- `09-estado-implementacion-pruebas.md`, sección 7 — resultado real de la puesta en producción del 2026-09-04.
