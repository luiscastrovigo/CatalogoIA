---
title: Errores cometidos y controles que los previenen
proyecto: Catalogo App IA
version: "1.2"
fecha: 2026-09-14
autor: Claude — a pedido de Luis Castro
depende_de: 09-estado-implementacion-pruebas.md, 10-guia-despliegue-perms220.md, 11-auditoria-seguridad.md, 13-adjuntos-diseno-y-seguridad.md, 14-reasignacion-de-dueno.md
---

# Errores cometidos y controles que los previenen

Este documento es la base de conocimiento de errores de este proyecto. Existe porque el
código se escribe en un entorno **sin compilador y sin salida a NuGet**: si un error de
sintaxis se escapa, no se descubre hasta que Luis Castro corre `dotnet publish` en su
máquina, y cada ida y vuelta cuesta un ciclo completo de trabajo. La regla de uso es
simple: **consultar este documento antes de escribir código, y correr el validador antes
de decir que algo está listo.**

Cada error registrado tiene un control asociado que lo detecta automáticamente. Un error
sin control no está cerrado: está pendiente de convertirse en control.

## 1. Errores de este asistente

Son errores propios, no del entorno ni del despliegue. Se registran con la misma
disciplina con la que se registran los bugs del producto.

| ID | Qué pasó | Síntoma real | Causa raíz | Control |
|---|---|---|---|---|
| E-01 | En `Views/Solucion/Index.cshtml` se escribió un comentario explicativo dentro del bloque `<script>` que mencionaba `@s.Nombre` como ejemplo del código vulnerable | `error CS0103: El nombre 's' no existe en el contexto actual` | Razor interpreta `@` como transición a código **también dentro de un comentario de JavaScript**: el compilador de vistas no entiende sintaxis JS | **C-05** |
| E-02 | En el `.csproj` se escribió un comentario que incluía el comando `dotnet list package` con sus dos guiones | `error MSB4025: An XML comment cannot contain '--'` | XML prohíbe `--` dentro de un comentario y que un comentario termine en `-` | **C-01** |
| E-03 | `CrearPlataformaInput` usaba `Plataforma.EstadoEnEvaluacion` sin el `using` del espacio de nombres de entidades | Se detectó antes de entregar | Al agregar un tipo a un archivo existente no se verificó que su espacio de nombres estuviera importado | **C-11** |
| E-04 | La primera versión del propio validador marcaba `"Default": "Information"` (nivel de log) como si fuera una cadena de conexión | Falso positivo | La regla buscaba el nombre de la clave por expresión regular, sin mirar dónde estaba esa clave dentro del JSON | Recorrido por ruta de clave + caso de control |
| E-05 | En `Program.cs` se escribió `X509FindType.X509FindByThumbprint` | `error CS0117: 'X509FindType' no contiene una definición para 'X509FindByThumbprint'` | Se usó un miembro de la biblioteca base **de memoria, sin verificarlo**. El correcto es `FindByThumbprint` | **C-16** |
| E-06 | El bloque `@if (User.GetEsAdministrador())` insertado en `Create.cshtml` y `Editar.cshtml` **nunca se cerró** con su llave | `RZ1006`, `RZ1026`, `RZ1034` y `CS1513`: formulario mal formado y bloque `if` sin cerrar | Se generó el bloque de markup completo sin verificar el balance de llaves. **El validador no lo detectó porque C-08 solo contaba llaves desde `@section` hacia el final del archivo, y el bloque estaba antes** | **C-08 ampliado** |

**Patrón de E-01 y E-02:** los dos vinieron de **comentarios explicativos** escritos en
archivos con reglas de sintaxis propias (Razor, XML), tratados como si fueran texto libre. De
ahí las reglas de redacción de la sección 3.

**Patrón de E-05:** no falló la redacción sino la verificación. Se invocó una API de la
biblioteca base desde la memoria, en código que no se podía compilar. La mitigación
estructural, además del control, es de método: **preferir APIs que ya estén en uso en este
mismo repositorio**, porque esas ya demostraron compilar.

**E-06 es de otra naturaleza, y por eso es el más instructivo.** Aquí el control existía, la
salida del validador decía "0 errores", y el código estaba roto igual. El problema no fue la
ausencia de una regla sino el **alcance equivocado** de una regla existente: C-08 se había
escrito pensando en las secciones de scripts, que están al final del archivo, y nunca miró lo
que hubiera antes. La lección: **un validador en verde solo prueba que pasaron las
verificaciones que existen, con el alcance con que fueron escritas** — no que el código esté
bien. Cuando se agrega un bloque nuevo de markup, el balance de llaves se revisa a mano
además de correr el validador, y si un control no cubrió un caso que debía cubrir, lo que hay
que corregir es su alcance, no agregar un control nuevo al lado.

## 2. Errores de entorno y despliegue ya resueltos

Documentados en detalle en `10-guia-despliegue-perms220.md` y en la skill `appnet-yanbal`.

| ID | Síntoma | Causa real |
|---|---|---|
| D-01 | `MSB1009: El archivo de proyecto no existe` | Ruta de proyecto de más, estando ya dentro de esa carpeta |
| D-02 | HTTP 403 genérico al cargar el sitio | Se copió la carpeta `publish\` completa en vez de su **contenido** |
| D-03 | `Format of the initialization string does not conform to specification` | Llaves `{ }` de más envolviendo el valor de la cadena de conexión en `web.config` |
| D-04 | El bloque `<environmentVariables>` desaparece tras publicar | `dotnet publish` regenera `web.config` en cada publicación |
| D-05 | `Violation of UNIQUE KEY constraint ...AzureAdObjectId` al invitar al segundo usuario | Se usó `Guid.Empty` compartido como marcador en vez de `Guid.NewGuid()` por invitación |
| D-06 | `AADSTS700054` / `AADSTS900971` en el login | Falta marcar "ID tokens"; Redirect URI bajo la plataforma equivocada |
| D-07 | Login en loop de redirect detrás del proxy | Falta `UseForwardedHeaders` antes de `UseAuthentication` |

## 3. Reglas de redacción para archivos con sintaxis propia

- **En `.cshtml`**: ningún comentario puede contener `@` sin escapar como `@@`. Aplica a
  comentarios de JavaScript dentro de `<script>`, a comentarios HTML y a cualquier texto de la
  vista. Además, **todo bloque `@if`, `@foreach` o `@section` que se agregue debe verificarse
  cerrado**, contando llaves sobre el archivo completo (E-06).
- **En `.csproj`, `.config` y cualquier XML**: ningún comentario puede contener `--` ni
  terminar en `-`. No escribir comandos de consola con sus opciones dentro de un comentario XML.
- **En `.js` servido como estático**: el texto visible al usuario se escribe con escapes
  `\uXXXX`, porque el archivo se sirve sin `charset` explícito.
- **En `.cs`**: sin restricciones especiales; es el lugar natural para el comentario largo que
  explica una decisión.

## 4. El validador estático

`codigo/herramientas/validar_codigo.py`, sin dependencias fuera de la biblioteca estándar.

```
python3 herramientas/validar_codigo.py src/CatalogoCitizenDevIA.Web
```

Devuelve código de salida 1 si hay algún ERROR, así que puede encadenarse antes de
`dotnet publish`.

| Control | Qué verifica | Origen |
|---|---|---|
| C-01 | Comentarios XML con `--` o terminados en `-` | E-02 |
| C-02 | `.csproj` y `.config` bien formados como XML | Preventivo |
| C-03 | Ningún `PackageReference` con versión flotante | Hallazgo H-09 |
| C-04 | Ningún secreto ni cadena de conexión con valor en `appsettings*.json`, por ruta de clave | Regla de arquitectura |
| C-05 | Ningún `@` sin escapar en comentarios JS/HTML de vistas Razor | E-01 |
| C-06 | Ningún `<script>` inline si la CSP declara `script-src` sin `'unsafe-inline'` | Hallazgo H-12 |
| C-07 | Ningún manejador `onXXX=` inline ni URL `javascript:` bajo esa CSP | Hallazgo H-12 |
| C-08 | **Llaves balanceadas en la vista completa**, no solo desde `@section` | E-06 |
| C-09 | Todo `<script src="~/...">` apunta a un archivo que existe | Preventivo |
| C-10 | Llaves y paréntesis balanceados en cada `.cs`, ignorando cadenas y comentarios | Preventivo |
| C-11 | Todo tipo de una lista conocida tiene declarado su `using` | E-03 |
| C-12 | Toda política de `[Authorize(Policy=...)]` / `[EnableRateLimiting(...)]` está registrada | Preventivo |
| C-13 | Sintaxis de los `.js` propios y no-ASCII en código ejecutable | Preventivo |
| C-14 | Toda clase que declara una interfaz implementa todos sus métodos | Riesgo de los adjuntos |
| C-15 | Todo formulario con `<input type="file">` declara `enctype` | Riesgo de los adjuntos |
| C-16 | Usos de API de la biblioteca base que ya rompieron la compilación | E-05 |

**Sobre C-16, con honestidad:** es una lista curada de formas incorrectas concretas, no una
verificación general de la biblioteca base — eso solo lo puede hacer un compilador. Detecta la
repetición del error exacto, no un error nuevo del mismo tipo.

**Sobre C-08, tras E-06:** ahora cuenta las llaves de toda la vista, sobre el texto sin
comentarios HTML, sin comentarios Razor y sin valores entre comillas dobles. Se verificó
contra las 17 vistas del proyecto: todas balancean en cero, de modo que la regla no produce
falsos positivos en este código.

## 5. La batería de regresión del validador

`codigo/herramientas/probar_validador.py` arma proyectos sintéticos mínimos, le inyecta a cada
uno un defecto conocido —incluidos E-01, E-02, E-03, E-05 y E-06 tal como ocurrieron— y
verifica que el validador lo reporte con el control correcto.

```
python3 herramientas/probar_validador.py
```

Incluye **casos de control**: código correcto que se parece al defectuoso y que no debe
generar hallazgo. Existen por E-04. Estado actual: **24 casos, 0 fallas**.

## 6. Protocolo por iteración

1. **Antes de escribir**: consultar este documento y la skill `appnet-yanbal`.
2. **Al escribir**: respetar las reglas de redacción de la sección 3, preferir APIs que ya
   estén en uso en el repositorio (E-05), y verificar a mano el cierre de todo bloque de
   markup nuevo (E-06).
3. **Antes de entregar**: correr `validar_codigo.py`. Si reporta un ERROR, no se entrega.
   Un resultado en verde **no significa que el código esté bien**: significa que pasaron las
   verificaciones existentes, con el alcance con que fueron escritas.
4. **Al recibir un error de compilación**: registrarlo aquí con su causa raíz y **agregar o
   ampliar el control que lo detecta**, junto con su caso en la batería. Si el control ya
   existía pero no cubrió el caso, corregir su alcance en vez de agregar otro al lado.
5. **Después de publicar**: prueba de humo funcional de lo que se tocó.

El validador cubre sintaxis y consistencia estructural. **No reemplaza a la compilación ni a
la prueba funcional**: no verifica tipos, firmas de métodos, ni comportamiento en ejecución.

## 7. Pendiente de propagar a la skill

La skill `appnet-yanbal` incorpora las reglas de redacción y los controles C-01 a C-13. Falta
agregarle E-05 y E-06 y los controles C-14, C-15, C-16 y el alcance corregido de C-08, para
que las lecciones viajen también a las demás Apps .NET de Yanbal.
