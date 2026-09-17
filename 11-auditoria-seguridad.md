---
title: Auditoría de seguridad — Catálogo Citizen Development IA
proyecto: Catalogo App IA
version: "2.0"
fecha: 2026-09-10
autor: Claude (rol solicitado: Director de Seguridad de la Información) — revisado por Luis Castro
depende_de: 03-alcance-funcional.md, 07-arquitectura-despliegue-componentes.md, 09-estado-implementacion-pruebas.md, 10-guia-despliegue-perms220.md
---

# Auditoría de seguridad — Catálogo Citizen Development IA

Revisión completa del código fuente de la App (todos los controladores, servicios, capa de
datos, vistas Razor y configuración) desde la perspectiva de seguridad de aplicaciones web,
a pedido explícito de Luis Castro. Metodología: lectura línea por línea de los ~35 archivos
`.cs`/`.cshtml`/`.json` del proyecto (no un escaneo automático), contrastando cada superficie
de entrada de usuario contra las categorías del OWASP Top 10 y OWASP ASVS — inyección, control
de acceso roto, configuración insegura, fallas criptográficas, XSS, CSRF, diseño inseguro y
manejo de secretos. **No se tuvo acceso a un compilador ni al entorno real de PERMS02/Entra ID
en esta revisión** (misma restricción de red del sandbox documentada en
`09-estado-implementacion-pruebas.md` sección 1); el análisis es estático, sobre el código
fuente sincronizado en la carpeta del caso.

**Historial de este documento:** la versión 1.0 (primera revisión) identificó 14 hallazgos y
corrigió 6. La versión 2.0 corresponde a la **segunda ronda de remediación**, pedida por Luis
Castro con el criterio explícito de «corregir todo lo que se pueda sin afectar la
funcionalidad»: cerró 3 hallazgos adicionales (H-09, H-11, H-12) y dejó preparados por
configuración los dos que dependen de un dato externo (H-07, H-08). El estado actual es
**9 hallazgos corregidos y 5 abiertos**, todos ellos documentados con su plan de cierre en la
sección 7.

## 1. Resumen ejecutivo

| # | Hallazgo | Severidad | Estado |
|---|---|---|---|
| 1 | XSS almacenado → escalada de privilegios (nombre de Solución/Plataforma inyectado en `confirm()`) | **Crítico** | ✅ Corregido (ronda 1) |
| 2 | Código muerto de bypass de autenticación (login sin contraseña) seguía en el repositorio | Alto | ✅ Corregido (ronda 1) |
| 3 | Sobre-posteo (mass assignment) en `PlataformaController.Create` | Medio | ✅ Corregido (ronda 1) |
| 4 | Sin validación de dominio corporativo al invitar Administrador/Aprobador | Medio | ✅ Corregido (ronda 1) |
| 5 | Sin cabeceras de seguridad HTTP (CSP, X-Frame-Options, etc.) | Medio | ✅ Corregido (ronda 1) |
| 6 | Cookies de sesión/antiforgery sin políticas explícitas (Secure/HttpOnly/SameSite) | Bajo | ✅ Corregido (ronda 1) |
| 7 | `ForwardedHeadersOptions` confía en cabeceras `X-Forwarded-*` de cualquier origen | **Alto** | ⏳ Abierto — **preparado**: se activa con una variable de entorno, falta la IP del proxy |
| 8 | Llaves de Data Protection sin cifrado adicional en reposo | Medio | ⏳ Abierto — **preparado**: se activa con una variable de entorno, falta decidir el certificado |
| 9 | Versiones de paquetes NuGet flotantes sin pin exacto | Medio | ✅ Corregido (ronda 2) |
| 10 | Catálogo sin paginación | Bajo | ⏳ Abierto — cambia la experiencia de usuario, requiere aceptación |
| 11 | Sin límite de tasa en endpoints sensibles | Bajo | ✅ Corregido (ronda 2) |
| 12 | CSP con `'unsafe-inline'` en `script-src` | Bajo | ✅ Corregido (ronda 2) |
| 13 | Visibilidad total del catálogo para cualquier usuario autenticado | Informativo | ⏳ Por diseño — falta confirmación del Comité |
| 14 | Fuente externa (Google Fonts) | Informativo | ⏳ Abierto — decisión de política de privacidad |

## 2. Hallazgos corregidos en la primera ronda

### 2.1 CRÍTICO — XSS almacenado con escalada de privilegios (CWE-79)

**Dónde:** `Views/Solucion/Index.cshtml` y `Views/Plataforma/Index.cshtml`, botón "Eliminar".

**El código original:**
```cshtml
<form method="post" asp-action="Eliminar" asp-route-id="@s.SolucionId"
      onsubmit="return confirm('¿Eliminar la solución @s.CodigoSolucion — @s.Nombre? Esta acción no se puede deshacer.');">
```

**El problema:** Razor (`@s.Nombre`) hace *HTML-encoding* del valor — protege contra romper el
atributo `onsubmit="..."` en sí (encodea `"` a `&quot;`) — pero **no hace JavaScript-encoding**.
El navegador primero decodifica las entidades HTML del atributo y *después* entrega ese texto
al motor de JavaScript como el cuerpo del manejador `onsubmit`. Como `Solucion.Nombre` es texto
libre (hasta 250 caracteres) que **cualquier usuario autenticado** puede escribir al registrar
una solución (`RegistroSolucionInput.Nombre`, sin restricción de caracteres), un nombre como:

```
x'); fetch('/Usuario/Invitar',{method:'POST',headers:{'RequestVerificationToken':document.querySelector('input[name=__RequestVerificationToken]').value},body:'correoCorporativo=atacante@yanbal.com&comoAdministrador=true'}); //
```

sobrevive el decodificado HTML, cierra el string de JavaScript (`'`) antes de tiempo, y el resto
se ejecuta como código arbitrario **en la sesión del navegador de quien haga clic en "Eliminar"
sobre esa fila** — que, por diseño, solo puede ser un Administrador (`@if (User.GetEsAdministrador())`
en Solucion/Index.cshtml). El script inyectado corre con acceso completo al DOM de la página del
Administrador, incluido el token antifalsificación ya presente en el propio formulario — es decir,
**puede emitir peticiones autenticadas como ese Administrador sin necesitar credenciales
adicionales** (por ejemplo, invitarse a sí mismo como Administrador vía `/Usuario/Invitar`, o
alternar el rol de cualquier usuario vía `/Usuario/ToggleAdmin`).

**Impacto real:** cualquier "Usuario registrante" (el rol de menor privilegio de la App) podía
plantar este pago con un registro de solución aparentemente normal, y escalar a Administrador
completo el día que cualquier Administrador entrara al Catálogo e intentara borrar ese registro
— un flujo de trabajo normal y esperable (limpiar duplicados, registros de prueba, etc.). El
mismo patrón exacto existía en `Plataforma.Nombre` (solo explotable entre Administradores, ya
que crear/nombrar una Plataforma ya requiere ese rol, pero igual de válido como vector de
persistencia/backdoor entre cuentas administrativas).

**Corrección aplicada:** en ambas vistas, el nombre ya no se concatena dentro de un string de
JavaScript. Viaja en un atributo `data-mensaje="..."` (el navegador lo decodifica de forma segura
como texto plano, sin volver a entrar a un contexto de ejecución) y un script separado arma el
`confirm()` leyendo ese atributo. Desde la ronda 2 ese script vive en
`wwwroot/js/catalogo-ui.js` (ver 6.3).

**Regla general para el equipo (para no reintroducir esto):** nunca interpolar un valor dinámico
directamente dentro de un atributo `onXXX="..."` ni dentro de un `<script>` que construye un
string por concatenación — el HTML-encoding de Razor protege el contexto HTML, no el contexto
JavaScript. Si un valor debe llegar a JavaScript, pasarlo por un atributo `data-*` (texto plano)
o serializarlo con `System.Text.Json` correctamente tipado — nunca con interpolación cruda.

### 2.2 Alto — Código muerto de bypass de autenticación en el repositorio (CWE-489)

**Dónde:** `Views/Account/Login.cshtml` + `Services/RoleResolutionService.cs` (ambos **eliminados**).

Estos dos archivos implementaban el "login de desarrollo" de las primeras rondas del proyecto:
una pantalla que dejaba escribir cualquier correo `@yanbal.com` y cualquier nombre, sin
contraseña ni verificación alguna, y creaba/autenticaba a esa persona instantáneamente —
incluso mostraba botones de acceso directo con la lista completa de usuarios ya registrados
(marcando visualmente quién era Administrador). Desde la integración real con Entra ID,
`AccountController` ya no tiene una acción `Login` que sirva esa vista, así que hoy era código
inalcanzable — pero seguía **compilándose en el binario de producción**. Código muerto de
autenticación es un riesgo real de higiene: alguien podría reactivarlo sin darse cuenta del
bypass completo que reintroduce, y cualquiera con acceso al código fuente o a un decompilador
del DLL desplegado puede ver exactamente cómo funcionaba. **Corrección aplicada:** ambos
archivos se eliminaron por completo.

### 2.3 Medio — Sobre-posteo (mass assignment) en `PlataformaController.Create` (CWE-915)

El código original bindeaba directamente la entidad `Plataforma` desde el formulario. El *model
binder* llena cualquier propiedad pública de la entidad que calce con un campo del POST, **no
solo los campos que la vista pinta**. **Corrección aplicada:** `CrearPlataformaInput` con solo
los 4 campos del formulario real; `Activo`/`CreadoPorId` se calculan en el controlador.

### 2.4 Medio — Sin validación de dominio corporativo al invitar (CWE-284)

Solo se validaba que el texto contuviera un `@`. **Corrección aplicada:** lista de dominios
corporativos permitidos (`@yanbal.com`) validada antes de crear la invitación.

### 2.5 Medio — Ausencia total de cabeceras de seguridad HTTP (OWASP A05:2021)

**Corrección aplicada:** middleware que agrega `X-Content-Type-Options`, `X-Frame-Options`,
`Referrer-Policy`, `Permissions-Policy` y `Content-Security-Policy` a toda respuesta, antes de
`UseStaticFiles`.

### 2.6 Bajo — Cookies de sesión/antiforgery sin políticas explícitas (CWE-614)

**Corrección aplicada:** ambas cookies fijan `HttpOnly=true`, `SecurePolicy=Always` y
`SameSite=Lax`.

## 3. Segunda ronda de remediación (2026-09-10)

Pedido de Luis Castro: desarrollar el plan de cierre de todo lo pendiente y **ejecutar de
inmediato aquello que pudiera corregirse sin afectar la funcionalidad**. Ese criterio separó lo
pendiente en tres grupos: lo que se puede cerrar con cambios internos de comportamiento idéntico
(se hizo), lo que depende de un dato o decisión externa (se dejó preparado para activar por
configuración, sin recompilar), y lo que cambia lo que el usuario ve (se planificó, no se hizo).

### 3.1 H-09 cerrado — versiones exactas de paquetes NuGet

Las referencias usaban rangos flotantes (`8.0.*`, `5.*`, `3.*`, `1.*`), lo que permite que
cualquier `dotnet restore` futuro traiga una versión distinta a la probada sin que nadie lo
revise. Se cerró leyendo del `obj/project.assets.json` de la máquina de compilación las
versiones que el restore real **ya había resuelto**, y fijándolas exactas en el `.csproj`:

| Paquete | Antes | Ahora |
|---|---|---|
| Microsoft.EntityFrameworkCore.SqlServer | `8.0.*` | `8.0.30` |
| Microsoft.Data.SqlClient | `5.*` | `5.2.3` |
| Microsoft.EntityFrameworkCore.Design | `8.0.*` | `8.0.30` |
| Microsoft.Identity.Web | `3.*` | `3.15.1` |
| Microsoft.Identity.Web.UI | `3.*` | `3.15.1` |
| Microsoft.Graph | `5.*` | `5.105.0` |
| Azure.Identity | `1.*` | `1.21.0` |

Como son exactamente las versiones que ya se venían usando, el binario resultante es el mismo:
**cero cambio funcional**. Recomendación de seguimiento: correr periódicamente
`dotnet list package --vulnerable --include-transitive` para revisar avisos conocidos.

### 3.2 H-11 cerrado — límite de tasa en operaciones sensibles

Se habilitó el middleware nativo de .NET 8 (`AddRateLimiter`/`UseRateLimiter`, sin paquetes
adicionales) con una política `OperacionesSensibles` de **30 envíos por minuto por usuario
autenticado** (ventana fija, particionada por identidad y, en su defecto, por IP), aplicada con
`[EnableRateLimiting("OperacionesSensibles")]` en `UsuarioController.Invitar` y
`SolucionController.Create` (POST). El límite está muy por encima de cualquier uso humano real
— registrar una solución o invitar a un usuario toma decenas de segundos — de modo que **ningún
usuario legítimo lo percibe**. Si se excediera, la respuesta es un 429 con un mensaje en
castellano en vez de una página en blanco.

### 3.3 H-12 cerrado — CSP sin `'unsafe-inline'` en `script-src`

Se movió todo el JavaScript de las vistas a archivos estáticos y se retiró la excepción:

- **`wwwroot/js/riesgo-live.js`** — cálculo de riesgo en vivo y bloqueo de doble envío;
  lo usan `Views/Solucion/Create.cshtml` y `Views/Solucion/Editar.cshtml`. La URL del endpoint
  ya no se interpola dentro del JS: viaja en el atributo `data-url-calcular` del formulario,
  que escribe Razor con `Url.Action`.
- **`wwwroot/js/catalogo-ui.js`** — confirmación de borrado (H-01) y selects que envían su
  formulario al cambiar; lo usan `Views/Solucion/Index.cshtml` y `Views/Plataforma/Index.cshtml`.

**Detalle importante encontrado al hacerlo:** además de los bloques `<script>`, quedaba un
manejador de evento en línea, `onchange="this.form.submit()"` en el desplegable de Estado de
`Views/Plataforma/Index.cshtml`. La CSP sin `'unsafe-inline'` **también bloquea los manejadores
`onXXX=`**, así que retirar la excepción sin convertir ese desplegable habría roto el cambio de
estado de plataformas de forma silenciosa. Se reemplazó por la clase `js-auto-submit` con su
listener en `catalogo-ui.js`. Se verificó por búsqueda en todas las vistas que no queda ningún
otro manejador en línea ni URL `javascript:`.

`script-src` es ahora `'self'`. `'unsafe-inline'` se mantiene solo en `style-src`, porque las
vistas sí usan atributos `style=` en línea (eso es una mejora futura de menor valor: un estilo
inyectado no ejecuta código).

### 3.4 H-07 preparado — proxies conocidos por variable de entorno

No se puede cerrar sin la IP real del proxy (ver 4.1), pero se eliminó la necesidad de tocar
código el día que Red la confirme. `ForwardedHeadersOptions` ahora lee
`ForwardedHeaders__KnownProxies` (una o varias IPs separadas por coma o punto y coma) y las
registra con `KnownProxies.Add`. Mientras la variable no exista, el comportamiento es
**idéntico al actual**; una IP mal escrita se ignora en vez de impedir el arranque; y al
arrancar sin la variable la App deja una advertencia en el log para que el pendiente no se
olvide en silencio. Activarlo es editar el `<environmentVariables>` de `web.config` y reiniciar
el Application Pool — sin recompilar. Revertirlo es quitar la variable.

### 3.5 H-08 preparado — cifrado de llaves por variable de entorno

Mismo criterio. Si se configura `DataProtection__CertificateThumbprint` con la huella de un
certificado instalado en el almacén Personal (de la máquina o del usuario), la App llama a
`ProtectKeysWithCertificate`. Si la variable no está, el comportamiento es el actual; si está
pero el certificado no se encuentra, **la App arranca igual** (no se cae) y deja la advertencia
en el log. Falta únicamente la decisión de si se dedica un certificado (ver 4.2).

### 3.6 Resumen de archivos tocados en la ronda 2

**Archivos nuevos:** `wwwroot/js/riesgo-live.js`, `wwwroot/js/catalogo-ui.js`.

**Archivos modificados:** `Program.cs`, `CatalogoCitizenDevIA.Web.csproj`,
`Controllers/SolucionController.cs`, `Controllers/UsuarioController.cs`,
`Views/Solucion/Create.cshtml`, `Views/Solucion/Editar.cshtml`, `Views/Solucion/Index.cshtml`,
`Views/Plataforma/Index.cshtml`.

**No requiere cambio de esquema de base de datos.** Sí requiere recompilar y publicar.

## 4. Hallazgos abiertos

### 4.1 ALTO — `ForwardedHeadersOptions` confía en cualquier origen (CWE-346)

Vaciar `KnownNetworks`/`KnownProxies` hace que la App confíe en las cabeceras
`X-Forwarded-For/-Proto/-Host` **venga de donde venga la petición**, no solo del reverse proxy
real (`proxynp.unique-yanbal.com`). Riesgos concretos: (1) si PERMS220 es alcanzable
directamente (sin pasar por el proxy) desde algún segmento de red, cualquiera ahí puede
falsificar `X-Forwarded-Host`/`-Proto` y potencialmente manipular el `redirect_uri` que
Microsoft.Identity.Web arma para el login OIDC; (2) el `X-Forwarded-For` que queda en los logs
para trazabilidad de incidentes puede ser falsificado, restándole valor forense.

**Por qué no se corrigió a ciegas:** una IP incorrecta rompería el login (Microsoft.Identity.Web
dejaría de ver el esquema HTTPS real y entraría en loop de redirect, el mismo síntoma ya
documentado en `10-guia-despliegue-perms220.md` sección 8). El código ya está preparado (3.4);
lo que falta es el dato. Plan de cierre en 7.1.

### 4.2 Medio — Llaves de Data Protection sin cifrado adicional en reposo (CWE-311)

Quien tenga acceso de lectura a la carpeta de `DataProtection__KeysPath` puede leer las llaves
en texto plano y falsificar cookies de sesión/tokens antifalsificación. Mitigado parcialmente
por los permisos de carpeta recomendados en `10-guia-despliegue-perms220.md` (solo la identidad
del Application Pool). El código ya está preparado (3.5); falta la decisión. Plan en 7.2.

### 4.3 Bajo — Catálogo sin paginación

`SolucionController.Index` carga **todas** las soluciones a memoria en cada request sin límite.
No es explotable hoy con el volumen esperado y el endpoint exige autenticación, pero es un punto
de degradación progresiva. **No se corrigió porque cambia lo que el usuario ve** y eso requiere
aceptación explícita. Plan en 7.3.

### 4.4 Informativo — Visibilidad total del catálogo (por diseño, a confirmar)

`SolucionController.Details`/`Index`/`Comprobante` no restringen por dueño: cualquier usuario
autenticado puede ver el detalle completo de la solución de cualquier otro. Está definido así
en `03-alcance-funcional.md` y no se trata como un bug — pero vale la pena que el Comité
confirme explícitamente que ningún campo de texto libre (`Comentarios`, `Etiquetas`,
`DescripcionBreve`) está pensado para llevar información que no deba ser visible para toda la
organización. Plan en 7.4.

### 4.5 Informativo — Fuente externa (Google Fonts)

`_Layout.cshtml` carga la tipografía Geist desde `fonts.googleapis.com`/`fonts.gstatic.com`,
lo que implica una petición a un dominio de Google (con la IP del usuario) en cada carga de
página. Decisión de política de privacidad interna. Plan en 7.5.

## 5. Buenas prácticas ya presentes

- **El rol nunca se confía desde el token de Entra ID.** `RoleClaimsTransformation` descarta
  cualquier claim `yanbal:*` que venga en el principal original y los reconstruye siempre desde
  `dbo.Usuario` en cada request.
- **CSRF cubierto de forma consistente:** todas las acciones `POST` que cambian estado llevan
  `[ValidateAntiForgeryToken]` y su formulario tiene `@Html.AntiForgeryToken()`.
- **Secretos nunca en `appsettings.json`:** `TenantId`/`ClientId`/`ClientSecret`/cadena de
  conexión/remitente se leen siempre de variables de entorno.
- **Sin SQL dinámico concatenado:** LINQ sobre EF Core; el único `ExecuteSqlRaw` pasa el
  parámetro de salida vía `SqlParameter` tipado.
- **Concurrencia del correlativo resuelta a nivel de base de datos**, correcto para múltiples
  workers de IIS.
- **HTML-encoding correcto en el cuerpo de los correos** (`GraphNotificacionService`).
- **Autorización por política granular y consistente**, coherente con `03-alcance-funcional.md`.
- **Edición de Solución restringida a dueño o Administrador**, verificada en `GET` y en `POST`.
- **DTOs de edición deliberadamente angostos** (`EditarPlataformaInput`, `EditarSolucionInput`).
- **Transporte y sesión:** `UseHttpsRedirection` + `UseHsts` + expiración explícita de 8 horas
  con renovación deslizante.

## 6. Pruebas funcionales a realizar tras publicar la ronda 2

Los cambios de la ronda 2 son internos y no deberían alterar nada visible, pero tocaron
JavaScript que sí se ve, así que conviene una prueba de humo dirigida:

1. **Registrar solución:** al marcar/desmarcar las casillas de riesgo, la insignia de Nivel debe
   recalcularse en vivo; al enviar, el botón debe deshabilitarse y aparecer "Procesando…".
2. **Editar solución:** la insignia de Nivel debe recalcularse igual.
3. **Catálogo → Eliminar:** debe aparecer el diálogo de confirmación con el código y nombre
   correctos, y cancelar debe abortar el borrado.
4. **Plataformas → Eliminar:** ídem.
5. **Plataformas → cambiar Estado con el desplegable:** debe grabar al cambiar la selección
   (este es el `onchange` convertido en 3.3 — el punto de mayor riesgo de regresión).
6. **Consola del navegador (F12):** no debe aparecer ningún error de tipo
   *"Refused to execute inline script"*. Si aparece, significa que quedó JavaScript inline sin
   externalizar y hay que moverlo antes de dar por cerrado H-12.

## 7. Plan de cierre de lo pendiente

### 7.1 H-07 — `ForwardedHeaders` (severidad Alto, prioridad 1)

| Paso | Detalle | Responsable |
|---|---|---|
| 1 | Solicitar al equipo de Red la IP (o rango) del servidor que resuelve `proxynp.unique-yanbal.com`, y confirmar si PERMS220 acepta tráfico directo sin pasar por el proxy. | Luis Castro → Red |
| 2 | Agregar `ForwardedHeaders__KnownProxies` con esa IP al bloque `<environmentVariables>` de `web.config` (`10-guia-despliegue-perms220.md` sección 4). **No requiere recompilar.** | Luis Castro |
| 3 | Reiniciar el Application Pool y verificar: el login contra Entra ID completa correctamente y la advertencia de arranque ya no aparece en el log. | Luis Castro |
| 4 | Si el login fallara, quitar la variable y reiniciar: se vuelve al comportamiento actual de inmediato. | Luis Castro |

Si Red confirma que PERMS220 **solo** es alcanzable a través del proxy, el riesgo real de este
hallazgo baja sustancialmente respecto del teórico, y esa confirmación debe quedar registrada.

### 7.2 H-08 — Cifrado de llaves de Data Protection (severidad Medio, prioridad 2)

| Paso | Detalle | Responsable |
|---|---|---|
| 1 | Decidir si se dedica un certificado. Criterio: si la carpeta de llaves ya tiene ACL restringida a la identidad del Application Pool y a administradores, el riesgo residual es bajo y la medida es defensa en profundidad. | Seguridad / Infraestructura |
| 2 | Si la decisión es sí: instalar el certificado en el almacén Personal de LocalMachine en PERMS220 y otorgar a la identidad del Application Pool permiso de lectura de la clave privada ("Manage Private Keys"). | Infraestructura |
| 3 | Configurar `DataProtection__CertificateThumbprint` con la huella. **No requiere recompilar.** | Luis Castro |
| 4 | Reiniciar, verificar que el login sigue funcionando y que no aparece la advertencia de certificado no encontrado. | Luis Castro |
| 5 | **Resguardar el certificado con su clave privada.** Si se pierde, las llaves cifradas quedan ilegibles: el efecto es que todos los usuarios deben volver a iniciar sesión (no hay pérdida de datos), pero conviene tenerlo previsto. | Infraestructura |

### 7.3 H-10 — Paginación del Catálogo (severidad Bajo, prioridad 4)

Requiere aceptación previa porque cambia lo que ve el usuario. Propuesta concreta: 50 filas por
página, controles al pie de la tabla, los filtros vigentes se conservan en el querystring al
cambiar de página, y el filtrado pasa a resolverse en la base de datos en vez de en memoria
(hoy `ObtenerTodas()` trae todo el catálogo y filtra en C#). Alcance del cambio:
`ISolucionRepository` y sus dos implementaciones, `SolucionController.Index`, `CatalogoViewModel`
y `Views/Solucion/Index.cshtml`. Esfuerzo estimado: media jornada más pruebas. Momento sugerido:
cuando el catálogo se acerque a los 200 registros, o antes si se prefiere no volver al tema.

### 7.4 H-13 — Confirmación de la visibilidad organizacional (Informativo, prioridad 3)

Llevar el punto al Comité Citizen Development y dejar constancia en acta de que el catálogo es
visible para toda la organización y de que los campos de texto libre no deben usarse para
información de circulación restringida. Si la respuesta fuera que sí puede haber información
sensible, la alternativa técnica es restringir la vista de detalle a dueño, aprobadores y
administradores, dejando el listado con los campos no sensibles: alcance aproximado de una
jornada. Sin decisión no hay trabajo técnico que hacer.

### 7.5 H-14 — Autohospedar la tipografía (Informativo, prioridad 5)

Si la política de privacidad interna lo considera relevante: descargar los archivos `woff2` de
la tipografía Geist, ubicarlos en `wwwroot/fonts/`, declarar las reglas `@font-face` en
`site.css`, quitar el `<link>` a Google de `_Layout.cshtml` y ajustar la CSP a
`style-src 'self' 'unsafe-inline'` y `font-src 'self'`. Verificación: la tipografía se ve igual
y la pestaña de red del navegador no muestra ninguna petición a `fonts.gstatic.com`. Esfuerzo
estimado: una hora.

### 7.6 Seguimiento continuo (no es un hallazgo)

Incorporar al procedimiento de cada publicación la ejecución de
`dotnet list package --vulnerable --include-transitive`. Con las versiones ya fijadas (3.1),
ese comando es la forma barata de enterarse de un aviso de seguridad publicado sobre alguna de
las dependencias, en vez de descubrirlo por casualidad.

## 8. Próximos pasos priorizados

1. Compilar y publicar la ronda 2 en PERMS220 y ejecutar la prueba de humo de la sección 6.
2. Conseguir del equipo de Red la IP de `proxynp.unique-yanbal.com` y cerrar H-07 (sección 7.1).
3. Decidir sobre el certificado de Data Protection y cerrar H-08 (sección 7.2).
4. Llevar H-13 al Comité para confirmación formal (sección 7.4).
5. Programar H-10 y H-14 según roadmap (secciones 7.3 y 7.5).
