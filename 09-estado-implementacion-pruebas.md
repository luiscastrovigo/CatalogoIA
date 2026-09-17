---
title: Estado de implementación y pruebas — primera versión
proyecto: Catalogo App IA
version: "2.0"
fecha: 2026-09-10
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
depende_de: 03-alcance-funcional.md, 06-diseno-bd-sql-server.md, 07-arquitectura-despliegue-componentes.md, 08-reglas-negocio-calculo-riesgo.md, 10-guia-despliegue-perms220.md, 11-auditoria-seguridad.md
---

# Estado de implementación y pruebas

Este documento registra qué se construyó de la App, dónde y cómo se probó, y qué falta
para llevarla a la infraestructura real de Yanbal (PERMS02 + Entra ID). El código fuente
completo se entregó como archivo adjunto y quedó sincronizado en la carpeta del caso en
OneDrive.

## 1. Restricción del entorno de construcción y cómo se resolvió

El entorno cloud donde se construyó esta App **no tiene salida de red hacia NuGet.org ni
hacia la red interna de Yanbal** (PERMS02, Entra ID) — son restricciones de la política de
egress del sandbox de este asistente, no de la infraestructura de Yanbal. Esto impide
restaurar paquetes NuGet y conectarse de verdad a SQL Server o a un tenant de Azure AD desde
ese entorno, en cualquiera de las rondas de construcción (ver secciones 2 y 8). Un puente de
archivos hacia el equipo de Luis Castro (donde sí hay SDK/NuGet reales) permite sincronizar el
código, pero tampoco tiene salida de red desde ese entorno intermedio — compilar y publicar
sigue siendo un paso manual, en la máquina real de Luis Castro o en PERMS220 (ver sección 9).

## 2. Primera ronda — implementación sin paquetes NuGet, probada de punta a punta

Para poder entregar algo real que compile, se pueda desplegar y se pueda probar hoy mismo, la
primera versión se construyó en ASP.NET Core MVC (.NET 8) usando únicamente lo que trae el
SDK (sin ningún paquete NuGet de terceros), con la capa de acceso a datos, la autenticación y
el envío de notificaciones detrás de interfaces — `IUsuarioRepository`, `IPlataformaRepository`,
`ISolucionRepository`, `INotificacionService` — para que la implementación real se conectara
ahí sin tocar controladores, vistas ni reglas de negocio.

### 2.1 Qué se implementó (código real, no maqueta)

| Módulo (03-alcance-funcional.md) | Implementado como |
|---|---|
| Autenticación | `AccountController` + cookie de sesión + `RoleClaimsTransformation` |
| Registro de solución | `SolucionController.Create` + `SolucionService` — código correlativo `CD-AAAA-###`, `RiesgoCalculator`, primer `HistorialEstado`, notificación de comprobante |
| Recálculo de riesgo en vivo | Endpoint `POST /Solucion/CalcularRiesgo` |
| Catálogo / consulta | `SolucionController.Index` con filtros, `Details` con historial y notificaciones |
| Comprobante de registro | `SolucionController.Comprobante` |
| Administración de plataformas | `PlataformaController` (solo Administrador) |
| Revisión y aprobación | `RevisionController` (solo Administrador) |
| Dashboard ejecutivo | `DashboardController` (solo Administrador) |
| Gestión de administradores | `AdministradorController` (solo Administrador) |

### 2.2 Pruebas realizadas

Harness HTTP sin dependencias externas (`CatalogoCitizenDevIA.Tests`, xUnit tampoco es
alcanzable desde el sandbox): 12/12 verificaciones de `RiesgoCalculator` contra la tabla de
casos de `08-reglas-negocio-calculo-riesgo.md`, más 20 verificaciones funcionales end-to-end
por HTTP (login, alta de plataforma, registro completo, catálogo, control de acceso,
Gestión de administradores). **Resultado: 32/32 verificaciones correctas.**

## 3. Despliegue en PERMS220 — primera prueba de infraestructura

Con la decisión de servidor de aplicación ya tomada (`10-guia-despliegue-perms220.md`), esta
primera versión (login de desarrollo + repositorios en memoria) se desplegó en PERMS220 como
prueba de que la cadena IIS + ANCM + Hosting Bundle funciona de punta a punta antes de invertir
en la integración real. Problemas reales encontrados y resueltos durante ese despliegue (el
detalle completo, con el diagnóstico y el fix de cada uno, está en
`10-guia-despliegue-perms220.md`):

- Confusión de carpeta al correr `dotnet publish` (`MSB1009`).
- Corrección de un error de esta guía sobre una pantalla de IIS Manager para variables de
  entorno del Application Pool que no existe en ninguna versión de Windows Server.
- Ubicación exacta del bloque `<environmentVariables>` dentro de un `web.config` con
  `<aspNetCore />` autocerrado.
- **HTTP 403 genérico en `/CatalogoIA/`**: causa real fue que se copió la carpeta raíz del
  proyecto completa (con subcarpetas `publish\` y `src\` anidadas) en vez de solo el
  **contenido** de `publish\` — IIS nunca llegaba a pasarle la petición a ANCM. Se resolvió
  recopiando correctamente el contenido de `publish\` a la raíz del sitio.
- **Pérdida del bloque `<environmentVariables>` tras un re-publish**: exactamente el riesgo ya
  documentado en la guía (sección 4, Opción A) — `dotnet publish` regenera `web.config` y borra
  cualquier edición manual. Confirmado en la práctica repetidas veces (ver sección 7).

Con esto, la infraestructura de PERMS220 quedó validada de punta a punta con la App real
(no solo con Classic ASP como `dashticketsSDM`).

## 4. Módulo "Gestión de administradores" — hallazgo real de uso e invitación por correo

Al probar en PERMS220, se detectó que el módulo solo permitía alternar el rol de personas que
**ya habían iniciado sesión al menos una vez** — no había forma de asignar el rol Administrador
a alguien nuevo antes de su primer ingreso. Se agregó `AdministradorController.Invitar`: precarga
un registro de `Usuario` con `EsAdministrador = true` y sin `AzureAdObjectId` vinculado; en el
primer login de esa persona, la resolución de identidad (`RoleClaimsTransformation`) completa
el vínculo automáticamente sin perder el rol precargado. Ver sección 7 para un bug real
encontrado y corregido sobre esta misma funcionalidad ya en producción. **Nota:** este
controlador se renombró a `UsuarioController` en la ronda de la sección 8 — ver ahí.

## 5. Segunda ronda — integración real (EF Core / Microsoft.Identity.Web / Microsoft Graph)

Con la infraestructura de PERMS220 ya validada (sección 3) y la App Registration de Entra ID ya
creada, se escribió la integración real que reemplaza las tres piezas "Dev" de la primera ronda:

| Pieza | Reemplaza a | Implementación |
|---|---|---|
| Persistencia | `Data/InMemory/*` | `Data/AppDbContext.cs` + `Data/EfCore/*` — EF Core contra SQL Server (PERMS02), mapeado al esquema ya aprobado de `06-diseno-bd-sql-server.md`; `EfSolucionRepository.GenerarCodigoSolucion` llama al procedimiento real `dbo.sp_GenerarCodigoSolucion` (no reimplementa el bloqueo en C#, para ser seguro con múltiples workers de IIS) |
| Autenticación | `AccountController.Login` (login de desarrollo, retirado) | `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApp`) contra la App Registration de Entra ID |
| Alta automática y resolución de rol | `RoleResolutionService` (retirado) + `RoleClaimsTransformation` | Fusionadas en `RoleClaimsTransformation`, que ahora corre igual con el login real — sigue resolviendo el rol en cada request desde `dbo.Usuario`, nunca desde Azure AD |
| Notificación de comprobante | `DevNotificacionService` | `GraphNotificacionService` vía Microsoft Graph `Mail.Send` (permiso de aplicación, paso a paso de consentimiento en `10-guia-despliegue-perms220.md` sección 5.1) |

Esta ronda se escribió en un entorno sin salida a NuGet.org/red interna de Yanbal, así que no
se pudo compilar ni probar antes de entregarla — ver sección 7 para el resultado real de
llevarla a PERMS220/PERMS02/Entra ID de producción.

## 6. Qué falta

1. **Probar el envío real del comprobante por Microsoft Graph (`Mail.Send`)** con un buzón
   remitente que exista de verdad en el tenant — la primera prueba en producción falló con
   "The requested user 'no-reply-catalogoia@yanbal.com' is invalid" porque ese buzón todavía no
   existe en Microsoft 365/Exchange Online (no es un bug: `GraphNotificacionService` degradó
   correctamente, el registro quedó guardado y el usuario vio el aviso en pantalla). Falta
   decidir y ejecutar una de dos: crear `no-reply-catalogoia@yanbal.com` como Shared Mailbox
   (no requiere licencia), o apuntar temporalmente `Notificaciones__RemitenteCorreo` a un buzón
   existente para aislar y confirmar que el permiso `Mail.Send` en sí funciona.
2. **Exchange — Application Access Policy para `Mail.Send`** (recomendado, no bloqueante): acotar
   el permiso de aplicación para que esta App solo pueda enviar como el buzón remitente
   configurado, no como cualquier buzón del tenant — paso a paso en
   `10-guia-despliegue-perms220.md` sección 5.1.
3. **Aplicar en PERMS02 la migración incremental de la sección 8** (`ALTER TABLE dbo.Usuario ADD
   EsAprobador ...`, script completo en `06-diseno-bd-sql-server.md` sección 7) — bloqueante
   para que el rol Aprobador funcione contra la base de datos real.
4. Confirmar que el resto de módulos (Registro de solución, Catálogo, Revisión, Dashboard
   ejecutivo, Administración de plataformas) funcionan correctamente contra datos reales de
   PERMS02, ahora que el login y la persistencia real ya están validados (sección 7).
5. **Compilar y publicar en PERMS220 el código de las secciones 8 a 15** (no se pudo compilar
   en este entorno por la misma restricción de red de la sección 1) y ejecutar la prueba de humo
   de la sección 15.4, que cubre las pantallas cuyo JavaScript se movió a archivos externos.
6. **Crear la carpeta de `DataProtection__KeysPath` y configurar la variable en PERMS220**
   (sección 9, punto 2) — sin esto, la corrección del cierre automático de sesión sigue
   funcionando para los casos que ya detecta, pero el problema de fondo (llaves perdidas en
   cada recycle del Application Pool) no queda resuelto de raíz.
7. **Obtener del equipo Red la IP real del reverse proxy `proxynp.unique-yanbal.com`** y
   configurar `ForwardedHeaders__KnownProxies` (ver `11-auditoria-seguridad.md`, hallazgo 4.1 y
   plan 7.1) — desde la ronda de la sección 15 esto **ya no requiere recompilar**: es una
   variable de entorno más en `web.config` y un reinicio del Application Pool.

## 7. Segunda ronda validada en producción — 2026-09-04

La integración real (sección 5) se llevó directamente a PERMS220/PERMS02/Entra ID de
producción (no hubo ambiente de prueba/UAT disponible) y, tras resolver varios problemas de
despliegue — documentados en detalle en `10-guia-despliegue-perms220.md` secciones 4 y 10 —
**la App carga correctamente, el login real contra Entra ID funciona, y la conexión a PERMS02
funciona**. Hallazgos reales de esta puesta en producción:

- **Login contra Entra ID (`AADSTS700054` y `AADSTS900971`)**: ambos se resolvieron en el App
  Registration real — habilitar "ID tokens" en "Implicit grant and hybrid flows", y verificar/
  reescribir el Redirect URI bajo la plataforma **Web** (no SPA ni otra) hasta que quedó
  reconocido correctamente. El detalle "Recurso: Microsoft Graph" que aparece en el log de
  inicio de sesión de Entra ID para este tipo de login es un comportamiento normal de
  Microsoft.Identity.Web/MSAL (el permiso delegado `User.Read` de Graph que Azure agrega por
  defecto a todo App Registration nuevo) — no indica un problema ni un segundo flujo distinto.
- **Bug real corregido — `AdministradorController.Invitar` con `Guid.Empty` compartido**: la
  tabla `dbo.Usuario` tiene `UQ_Usuario_AzureAdObjectId` (restricción única). El código
  precargaba cada invitación pendiente con el mismo valor `Guid.Empty` como marcador de
  "todavía no inició sesión" — funcionaba para el primer Administrador invitado, pero el
  segundo violaba la restricción única (`Violation of UNIQUE KEY constraint`). Corregido:
  cada invitación ahora usa `Guid.NewGuid()` como marcador (único por invitación); la lógica
  de `RoleClaimsTransformation` que vincula el Object ID real en el primer login no depende de
  que el marcador sea `Guid.Empty` específicamente, así que el fix no requirió tocar nada más.
- **Fragilidad operativa real de la Opción A (variables de entorno en `web.config`)**: el mismo
  error de formato de cadena de conexión (llaves `{{ }}` de más alrededor del valor completo)
  reapareció **varias veces** durante el despliegue porque cada `dotnet publish` borra el
  bloque `<environmentVariables>` y hay que volver a pegarlo a mano — y en más de una ocasión
  se pegó por error una versión vieja/incorrecta guardada en otro lado. Se intentó migrar a la
  Opción B (variables a nivel de Application Pool) pero **en PERMS220, ni el módulo de
  PowerShell `WebAdministration` ni `appcmd.exe` están disponibles** (falta el rol de
  Windows "IIS Management Scripts and Tools" / `Web-Scripting-Tools`) — ver
  `10-guia-despliegue-perms220.md` sección 4 para el detalle y la recomendación operativa
  mientras tanto (guardar el bloque ya verificado en un archivo aparte y usar
  Buscar-y-reemplazar en vez de retipear a mano en cada publicación).

## 8. Tercera ronda — retroalimentación de usuarios en producción (2026-09-07)

Tras la puesta en producción de la sección 7, el primer ciclo real de uso (registro de
CD-2026-004 y pruebas posteriores) generó 7 puntos de retroalimentación. Se implementaron los
7 en el código (sincronizado a la carpeta del caso en OneDrive vía el puente de archivos, mismo
mecanismo de las rondas anteriores) — **no se pudo compilar en este entorno** por la misma
restricción de red de la sección 1, así que la compilación y el despliegue a PERMS220 quedan
pendientes (ver sección 6, punto 5).

| # | Pedido | Implementado como |
|---|---|---|
| 1 | Mensaje de éxito al registrar | `SolucionController.Create` (POST) fija `TempData["Mensaje"]` antes del redirect — el banner ya lo pinta `_Layout.cshtml` globalmente, no hizo falta markup nuevo |
| 2 | Pantalla en blanco tras registrar | El POST de `Create` ahora redirige a `Create` (GET) en vez de a `Comprobante` — el formulario vuelve vacío; se agregó un link opcional "Ver comprobante del registro anterior" vía `TempData["UltimoSolucionId"]` para no perder esa pantalla |
| 3 | Editar desde "Ver Detalle" (solo dueño o Administrador — decisión explícita) | `SolucionController.Editar` (GET/POST) + `Views/Solucion/Editar.cshtml` (formulario igual al de registro, precargado) + `SolucionService.Actualizar` (recalcula NivelRiesgo/ProximaRevision) + helper privado `PuedeEditar` |
| 4 | Eliminar registros — solo Administrador | `SolucionController.Eliminar` (`[Authorize(Policy = "EsAdministrador")]`) + `ISolucionRepository.Eliminar` (implementado en `EfSolucionRepository` con borrado en cascada de HistorialEstado/NotificacionEnviada/Adjunto dentro de una transacción, porque esas FK son `DeleteBehavior.Restrict`) + botón "Eliminar" con confirmación en `Views/Solucion/Index.cshtml` |
| 5 | Registrar quién cambia el estado | `HistorialEstado.UsuarioId` ya existía y ya se poblaba correctamente en `SolucionService` — el gap real era solo de UI: `Views/Solucion/Details.cshtml` no mostraba esa columna. Se agregó la columna "Usuario" a la tabla de historial |
| 6 | Nuevo rol Aprobador + renombrar "Gestión de administradores" a "Gestión de Usuarios" | `Usuario.EsAprobador` (nueva columna, ver `06-diseno-bd-sql-server.md` sección 7) + claim `AppClaimTypes.EsAprobador` + policy `PuedeAprobar` (`RequireAssertion` — Administrador O Aprobador) en `Program.cs` + `RevisionController` ahora usa esa policy + `AdministradorController` renombrado a `UsuarioController` con checkboxes independientes de Administrador/Aprobador tanto al invitar como al alternar rol de un usuario existente + `_Layout.cshtml` actualizado (nav renombrado, "Revisión y aprobación" visible también para Aprobador) |
| 8 | Tipografía de campos de formulario igual a títulos/texto | `wwwroot/css/site.css`: `input, select, textarea, button { font-family: inherit; }` — el body ya usaba Geist, pero los navegadores aplican su propia tipografía a controles de formulario por defecto |

**Cambio de esquema:** una sola columna nueva, `dbo.Usuario.EsAprobador BIT NOT NULL DEFAULT 0`
— sin backfill necesario (todos los usuarios existentes quedan en `0`, comportamiento idéntico
al actual hasta que un Administrador otorgue el rol). Script completo en
`06-diseno-bd-sql-server.md` sección 7.

**Archivos nuevos:** `Controllers/UsuarioController.cs` (reemplaza a `AdministradorController.cs`,
eliminado), `Views/Usuario/Index.cshtml` (reemplaza a `Views/Administrador/Index.cshtml`,
eliminado), `Views/Solucion/Editar.cshtml`.

**Archivos modificados:** `Controllers/SolucionController.cs`, `Controllers/RevisionController.cs`,
`Models/Entities/Usuario.cs`, `Models/ViewModels/SolucionViewModels.cs`, `Services/SolucionService.cs`,
`Services/AppClaimTypes.cs`, `Services/CurrentUserExtensions.cs`, `Services/RoleClaimsTransformation.cs`,
`Data/Interfaces/ISolucionRepository.cs`, `Data/EfCore/EfSolucionRepository.cs`,
`Data/InMemory/InMemorySolucionRepository.cs`, `Program.cs`, `Views/Shared/_Layout.cshtml`,
`Views/Solucion/Create.cshtml`, `Views/Solucion/Details.cshtml`, `Views/Solucion/Index.cshtml`,
`Views/Account/AccesoDenegado.cshtml`, `wwwroot/css/site.css`.

## 9. Cuarta ronda — retroalimentación de usuarios, segunda tanda (2026-09-07)

El mismo día, ya con la corrección del error de la cadena de conexión en producción (ver
`10-guia-despliegue-perms220.md` sección 4, causa 3), llegaron dos puntos adicionales de
retroalimentación real de uso:

| # | Pedido | Diagnóstico e implementación |
|---|---|---|
| 1 | Al dejar una pantalla abierta mucho tiempo y luego seleccionar una opción, aparece un error — debería cerrar sesión automáticamente y pedir el login | Dos causas reales encontradas al revisar el código: (a) **bug preexistente** — `Program.cs` tenía `app.UseExceptionHandler("/Home/Error")` pero el proyecto **nunca tuvo un `HomeController`**, así que cualquier excepción no controlada en producción caía en una ruta sin resolver en vez de una página de error legible; se agregó `Controllers/HomeController.cs` con la acción `Error()` (usa el `ErrorViewModel`/`Views/Shared/Error.cshtml` que ya existían sin controlador que los sirviera). (b) **causa de fondo del "error" en sí** — la App nunca configuraba persistencia de las llaves de Data Protection (`AddDataProtection().PersistKeysToFileSystem(...)`); sin eso, un recycle del Application Pool de IIS (por inactividad o su ciclo periódico) invalida la cookie de sesión y los tokens antifalsificación ya emitidos, y el intento de decodificarlos lanza `AntiforgeryValidationException`/`CryptographicException`. Se agregó en `Program.cs`: (i) un middleware que detecta específicamente ese tipo de excepción, cierra la sesión (`SignOutAsync`) y redirige al login de Entra ID en vez de mostrar un error; (ii) `builder.Services.AddDataProtection().PersistKeysToFileSystem(...)` configurable por la nueva variable de entorno `DataProtection__KeysPath` (ver `10-guia-despliegue-perms220.md` sección 4) para que las llaves sobrevivan un recycle; (iii) una expiración de sesión explícita de 8 horas con renovación automática (`ExpireTimeSpan`/`SlidingExpiration` sobre el esquema de cookie), en vez del valor por defecto del framework (14 días) que no comunicaba claramente cuándo "expira" la sesión. **Pendiente:** crear la carpeta de `DataProtection__KeysPath` en PERMS220 y configurar la variable (sección 6, punto 6) |
| 2 | En el Catálogo, el código de la solución aparece en 2 líneas — reducir la fuente para que se vea completo en una sola | `wwwroot/css/site.css`: nueva regla `table.table-clean td.font-mono { font-size: 11.5px; white-space: nowrap; }`, sin tocar la regla general `.font-mono` (que solo se usa en esta columna hoy) |

**Archivos nuevos:** `Controllers/HomeController.cs`.

**Archivos modificados:** `Program.cs`, `wwwroot/css/site.css`.

**No requiere cambio de esquema de base de datos.**

## 10. Quinta ronda — Administración de plataformas: editar/eliminar y confirmación de accesos por rol (2026-09-08)

Dos pedidos sobre el módulo "Administración de plataformas" y una confirmación de las reglas de
acceso por rol ya vigentes:

| # | Pedido | Implementado como |
|---|---|---|
| 1 | Poder editar una Plataforma, pero solo el nombre | `PlataformaController.Editar` (GET/POST) + `Views/Plataforma/Editar.cshtml` + `EditarPlataformaInput` (nuevo, en `Models/ViewModels/PlataformaViewModels.cs`) — a propósito el input **solo** trae `PlataformaId` y `Nombre`, así que no hay forma de sobre-postear otros campos aunque alguien arme el POST a mano. **Nota:** ampliado en la sección 12 para también permitir `Categoria` |
| 2 | Poder eliminar una Plataforma, pero bloquear el borrado si ya existen soluciones registradas con ella | `PlataformaController.Eliminar` valida `ISolucionRepository.ExistenSolucionesConPlataforma(id)` (nuevo método) antes de borrar; si hay soluciones asociadas, no elimina y muestra el motivo en `TempData["Mensaje"]`. `Views/Plataforma/Index.cshtml` ya deshabilita visualmente el botón para las plataformas en uso, calculado en el `Index()` vía `ViewBag.PlataformasConSoluciones`; el controlador vuelve a validar aunque llegue un POST directo |
| 3 | Solo Administrador debe poder eliminar Plataformas | Ya cubierto por el `[Authorize(Policy = "EsAdministrador")]` de clase en `PlataformaController` |
| 4 | Confirmar accesos por rol: cualquier usuario → Catálogo y Registro; Aprobador → + Revisión y aprobación; Administrador → todo | **Ya implementado correctamente desde rondas anteriores** — se revisó el código y coincide exactamente con lo pedido. No se hizo ningún cambio de código para este punto, solo se confirmó |

**No requiere cambio de esquema de base de datos.**

**Archivos nuevos:** `Models/ViewModels/PlataformaViewModels.cs`, `Views/Plataforma/Editar.cshtml`.

**Archivos modificados:** `Controllers/PlataformaController.cs`, `Data/Interfaces/ISolucionRepository.cs`,
`Data/EfCore/EfSolucionRepository.cs`, `Data/InMemory/InMemorySolucionRepository.cs`,
`Data/Interfaces/IPlataformaRepository.cs`, `Data/EfCore/EfPlataformaRepository.cs`,
`Data/InMemory/InMemoryPlataformaRepository.cs`, `Views/Plataforma/Index.cshtml`.

## 11. Sexta ronda — Dashboard ejecutivo: cards enlazadas al listado correspondiente (2026-09-08)

Pedido: que las 4 cards de KPIs de la parte superior del Dashboard ejecutivo lleven, al hacer
clic, al listado correspondiente en vez de ser solo texto estático.

| Card | Destino | Detalle |
|---|---|---|
| Soluciones registradas | Catálogo sin filtro | `asp-controller="Solucion" asp-action="Index"` |
| Nivel 3 (riesgo alto) | Catálogo filtrado por Nivel de riesgo | Reutiliza el filtro `NivelRiesgo` que ya existía |
| Pendientes de revisión inicial | Módulo Revisión y aprobación | Se decidió llevar al flujo de trabajo real en vez de a una vista de solo lectura |
| Próximas a revisión (30 días) | Catálogo filtrado por próxima revisión | Filtro nuevo `CatalogoFiltro.Proximas30Dias`; `SolucionController.Index` ahora acepta `?Proximas30Dias=true` y filtra `ProximaRevision <= hoy + 30 días` |

Se agregó la clase CSS `.kpi-card-link` (`wwwroot/css/site.css`) para que las cards se vean
claramente clicables.

**Archivos modificados:** `Models/ViewModels/SolucionViewModels.cs`, `Controllers/SolucionController.cs`,
`Views/Dashboard/Index.cshtml`, `wwwroot/css/site.css`.

## 12. Séptima ronda — corrección: "Editar plataforma" también debe permitir Categoría (2026-09-08)

Ajuste sobre el punto 1 de la sección 10: el pedido original decía "solo permitiendo cambiar
el nombre", pero en el uso real también hace falta poder corregir la Categoría. Se amplió
`EditarPlataformaInput` para incluir `Categoria` (sigue sin traer `Estado`/`Comentario`/`Activo`)
y se agregó el campo a `Views/Plataforma/Editar.cshtml` y al POST de `PlataformaController.Editar`.

**Archivos modificados:** `Models/ViewModels/PlataformaViewModels.cs`, `Controllers/PlataformaController.cs`,
`Views/Plataforma/Editar.cshtml`.

## 13. Octava ronda — retroalimentación de usuarios, tercera tanda (2026-09-08)

| # | Pedido / síntoma | Diagnóstico e implementación |
|---|---|---|
| 1 | Columna "Dueño" del Catálogo: para el Administrador se veía el nombre completo, pero para otros usuarios se veía el correo | La causa está en `RoleClaimsTransformation`: usaba `principal.FindFirstValue(ClaimTypes.Name) ?? correo` — si el token OIDC no trae el claim `name`, el correo quedaba grabado como `NombreCompleto` **para siempre**. Se corrigió con `ResolverNombre()`: (1) claim `name` si no es un correo; (2) `given_name` + `family_name`; (3) nombre legible derivado de la parte local del correo. Además la corrección se reintenta en **cada login** mientras el nombre guardado siga pareciendo un correo, así los usuarios ya afectados se autocorrigen solos sin UPDATE manual |
| 2 | Al presionar "Registrar solución" debe deshabilitarse el botón y mostrar "Procesando… Espere un momento" | `Views/Solucion/Create.cshtml`: listener del evento `submit` del formulario (no del `click` del botón, para no interferir con la validación HTML5) que deshabilita el botón y muestra el mensaje |
| 3 | En el Catálogo, colorear el Estado: Aprobado en verde, Rechazado en rojo | `Views/Solucion/Index.cshtml`: helper `TagEstado()`. Se completó el mapeo ya definido en `04-identidad-visual.md`: Aprobado → verde, Rechazado → rojo, En revisión → ámbar, Reclasificado → violeta (nueva clase `tag-reclasificado`), Registrado → gris |

**Archivos modificados:** `Services/RoleClaimsTransformation.cs`, `Views/Solucion/Create.cshtml`,
`Views/Solucion/Index.cshtml`, `wwwroot/css/site.css`.

## 14. Novena ronda — auditoría de seguridad integral (2026-09-10)

A pedido explícito de Luis Castro, actuando en rol de Director de Seguridad de la Información,
se realizó una auditoría de seguridad de toda la aplicación. El reporte completo está en
`11-auditoria-seguridad.md`. Se identificaron **14 hallazgos** y se corrigieron 6 en esta ronda:

| # | Hallazgo | Severidad | Estado tras esta ronda |
|---|---|---|---|
| H-01 | XSS almacenado en los listados de Catálogo y Plataformas: el nombre se interpolaba dentro de un `onsubmit="confirm('...')"`. El encoding de Razor protege el atributo HTML pero no el contexto JS anidado, así que un nombre con comillas simples permitía inyectar script que, al ejecutarse en la sesión de un Administrador, podía autoconcederle el rol al atacante vía `/Usuario/Invitar` | **Crítico** | ✅ Corregido — el mensaje pasó a un atributo `data-mensaje` leído por un listener |
| H-02 | Código muerto de login de desarrollo sin contraseña (`Views/Account/Login.cshtml` + `Services/RoleResolutionService.cs`), compilado en el binario de producción | Alto | ✅ Corregido — ambos eliminados |
| H-03 | *Mass assignment* en `PlataformaController.Create` | Medio | ✅ Corregido — DTO `CrearPlataformaInput` |
| H-04 | `UsuarioController.Invitar` sin validación de dominio corporativo | Medio | ✅ Corregido |
| H-05 | Sin cabeceras de seguridad HTTP | Medio | ✅ Corregido — middleware nuevo |
| H-06 | Cookies sin `HttpOnly`/`Secure`/`SameSite` explícitos | Bajo | ✅ Corregido |
| H-07 a H-14 | Ocho hallazgos que requerían un dato externo, una decisión, o un compilador | Alto a Informativo | ⏳ Ver sección 15 |

**Archivos eliminados:** `Views/Account/Login.cshtml`, `Services/RoleResolutionService.cs`.

**Archivos modificados:** `Views/Solucion/Index.cshtml`, `Views/Plataforma/Index.cshtml`,
`Models/ViewModels/PlataformaViewModels.cs`, `Controllers/PlataformaController.cs`,
`Views/Plataforma/Create.cshtml`, `Controllers/UsuarioController.cs`, `Program.cs`.

**Error de compilación encontrado y corregido:** el comentario explicativo que se agregó dentro
del bloque `<script>` de `Views/Solucion/Index.cshtml` mencionaba `@s.Nombre` como ejemplo del
código vulnerable. Razor interpreta el carácter `@` como transición a código **incluso dentro de
un comentario de JavaScript**, y `s` no existe fuera del `foreach` — de ahí el
`CS0103: El nombre 's' no existe en el contexto actual`. Se reescribió esa línea del comentario
sin el `@`. Es un error de sintaxis, no de seguridad.

## 15. Décima ronda — cierre de pendientes de la auditoría (2026-09-10)

Pedido de Luis Castro: desarrollar el plan de cierre de todo lo pendiente y **ejecutar de
inmediato lo que pudiera corregirse sin afectar la funcionalidad**. Ese criterio dividió los 8
hallazgos abiertos en tres grupos, y el resultado es **9 corregidos, 2 preparados y 3 en
decisión**. El detalle técnico completo y el plan paso a paso de lo que sigue abierto están en
`11-auditoria-seguridad.md` secciones 3 y 7.

### 15.1 Cerrados en esta ronda

| # | Hallazgo | Cómo se cerró |
|---|---|---|
| H-09 | Versiones NuGet flotantes (`8.0.*`, `5.*`, `3.*`, `1.*`) | Se leyeron del `obj/project.assets.json` de la máquina de compilación las versiones que el restore real **ya había resuelto** y se fijaron exactas en el `.csproj`: EF Core SqlServer y Design `8.0.30`, Data.SqlClient `5.2.3`, Identity.Web y Identity.Web.UI `3.15.1`, Graph `5.105.0`, Azure.Identity `1.21.0`. Como son las mismas versiones que ya se usaban, **el binario es idéntico**. Control asociado recomendado: `dotnet list package --vulnerable --include-transitive` en cada publicación |
| H-11 | Sin límite de tasa en endpoints sensibles | Limitador nativo de .NET 8 (`AddRateLimiter`/`UseRateLimiter`, sin paquetes nuevos): política `OperacionesSensibles` de **30 envíos/minuto por usuario** (ventana fija, particionada por identidad y en su defecto por IP), aplicada con `[EnableRateLimiting("OperacionesSensibles")]` en `UsuarioController.Invitar` y `SolucionController.Create` (POST). El umbral está muy por encima de cualquier uso humano, así que nadie lo percibe; si se alcanzara, devuelve 429 con un mensaje en castellano en vez de una página en blanco |
| H-12 | CSP con `'unsafe-inline'` en `script-src` | Se movió **todo** el JavaScript de las vistas a dos archivos estáticos y se retiró la excepción: `script-src` es ahora `'self'`. `'unsafe-inline'` se mantiene solo en `style-src`, porque las vistas sí usan atributos `style=` en línea (un estilo inyectado no ejecuta código) |

**Detalle importante de H-12:** además de los 4 bloques `<script>`, quedaba un manejador de
evento en línea — `onchange="this.form.submit()"` en el desplegable de Estado de
`Views/Plataforma/Index.cshtml`. La CSP sin `'unsafe-inline'` **también bloquea los manejadores
`onXXX=`**, así que retirar la excepción sin convertir ese desplegable habría roto el cambio de
estado de plataformas de forma silenciosa. Se reemplazó por la clase `js-auto-submit` con su
listener en el archivo externo, y se verificó por búsqueda sobre todas las vistas que no queda
ningún otro manejador en línea ni URL `javascript:`.

### 15.2 Preparados — se activan por variable de entorno, sin recompilar

| # | Hallazgo | Qué se dejó listo |
|---|---|---|
| H-07 | `ForwardedHeaders` acepta cabeceras de cualquier origen | `Program.cs` ahora lee `ForwardedHeaders__KnownProxies` (una o varias IPs separadas por coma o punto y coma) y las registra con `KnownProxies.Add`. Sin la variable, comportamiento idéntico al actual; una IP mal escrita se ignora en vez de tumbar el arranque; y al arrancar sin la variable la App deja una **advertencia en el log** para que el pendiente no se olvide. Falta solo la IP real del proxy (equipo de Red) |
| H-08 | Llaves de Data Protection sin cifrado en reposo | Si se configura `DataProtection__CertificateThumbprint`, la App busca el certificado en el almacén Personal (de la máquina o del usuario) y llama a `ProtectKeysWithCertificate`. Sin la variable, comportamiento actual; si la variable está pero el certificado no aparece, **la App arranca igual** y deja la advertencia en el log — se descartó a propósito que un error de configuración deje la App caída. Falta solo la decisión sobre el certificado |

### 15.3 No se tocaron — requieren una decisión previa

`H-10` (paginación del Catálogo) es el único abierto cuyo cierre **cambia lo que el usuario ve**,
por lo que requiere aceptación funcional; `H-13` (visibilidad organizacional del catálogo) y
`H-14` (autohospedar la tipografía) son decisiones del Comité y de política de privacidad
respectivamente. Plan concreto de los tres en `11-auditoria-seguridad.md` secciones 7.3 a 7.5.

### 15.4 Prueba de humo obligatoria tras publicar esta ronda

Los cambios son internos y no deberían alterar nada visible, pero tocaron JavaScript que sí se
ve. Antes de dar por cerrados H-11 y H-12 hay que verificar: (1) que al marcar/desmarcar las
casillas de riesgo en **Registrar solución** la insignia de Nivel se recalcule en vivo; (2) que
al enviar ese formulario el botón se deshabilite y aparezca "Procesando…"; (3) lo mismo en
**Editar solución**; (4) que el diálogo de confirmación de **Eliminar** siga apareciendo en
Catálogo y en Plataformas con el nombre correcto; (5) que el desplegable de **Estado** en
Plataformas siga grabando al cambiar la selección — este es el punto de mayor riesgo de
regresión; y (6) que la consola del navegador (F12) **no muestre ningún error del tipo
"Refused to execute inline script"** — si aparece, quedó JavaScript inline sin externalizar y
H-12 no puede darse por cerrado.

**No requiere cambio de esquema de base de datos.**

**Archivos nuevos:** `wwwroot/js/riesgo-live.js`, `wwwroot/js/catalogo-ui.js`.

**Archivos modificados:** `Program.cs`, `CatalogoCitizenDevIA.Web.csproj`,
`Controllers/SolucionController.cs`, `Controllers/UsuarioController.cs`,
`Views/Solucion/Create.cshtml`, `Views/Solucion/Editar.cshtml`, `Views/Solucion/Index.cshtml`,
`Views/Plataforma/Index.cshtml`.

**Entregable asociado:** informe formal de Ethical Hacking en Word (paleta de marca Yanbal),
versión 2.0, en la carpeta `Claude outputs` del caso.
