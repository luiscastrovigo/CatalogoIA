---
title: Adjuntos en el registro de soluciones — diseño, seguridad y despliegue
proyecto: Catalogo App IA
version: "1.0"
fecha: 2026-09-10
autor: Claude — a pedido de Luis Castro
depende_de: 02-modelo-datos-ampliado.md, 06-diseno-bd-sql-server.md, 11-auditoria-seguridad.md, 12-errores-y-controles.md
---

# Adjuntos en el registro de soluciones

Pedido de Luis Castro (2026-09-10): que al registrar una solución se pueda adjuntar un
documento, una imagen o cualquier otro archivo de soporte.

## 1. Punto de partida: la tabla ya existía

El modelo de datos ya contemplaba esta funcionalidad desde el diseño original.
`dbo.Adjunto` estaba creada, mapeada en EF Core y contemplada en el borrado en cascada de
una solución, con sus llaves foráneas hacia `Solucion` y `Usuario` y un `CHECK` que limita
el tipo a tres valores: captura de pantalla, acta de comité y otro documento. Lo que faltaba
era todo lo demás: repositorio, servicio, endpoints, interfaz y controles de seguridad.

El diseño original asumía que el archivo se guardaba **en disco** y que en la base solo
quedaba la ruta — de ahí la columna `RutaAlmacenamiento`.

## 2. Decisión: el contenido va dentro de SQL Server

Luis Castro optó por almacenar el binario **dentro de la base de datos**, no en el sistema
de archivos. Se evaluaron tres alternativas:

| Opción | A favor | En contra |
|---|---|---|
| Carpeta en PERMS220 | Es lo que asumía el diseño; no infla la base | Hay que respaldar la carpeta aparte del backup de SQL; requiere permisos NTFS y una variable de entorno más |
| **Dentro de SQL Server (elegida)** | Un solo backup, escritura transaccional junto con el resto del registro, sin permisos de carpeta ni prerrequisitos de infraestructura | Aumenta el tamaño de la base en PERMS02, que es un servidor compartido, y alarga backup y restauración |
| SharePoint vía Graph | Retención y permisos corporativos | Exige el permiso `Files.ReadWrite.All`, que es amplio, y agrega una dependencia externa al flujo de registro |

**Consecuencia dimensionada:** con el tope de 10 MB por archivo y 5 archivos por solución, el
peor caso teórico es de 50 MB por solución. Con cien soluciones y un uso realista de uno o
dos archivos livianos por registro, el crecimiento esperable es del orden de unos pocos
cientos de megabytes, no de gigabytes. Conviene igualmente revisar el tamaño de la base
después de los primeros meses de uso real.

### 2.1 El binario va en una tabla aparte

`dbo.Adjunto` conserva únicamente los metadatos y el contenido vive en `dbo.AdjuntoContenido`,
en relación uno a uno. La razón es concreta: si el `VARBINARY(MAX)` estuviera en la misma
tabla, cualquier consulta que solo quiera listar los adjuntos de una solución —nombre, tipo,
tamaño, quién lo subió— arrastraría megabytes de contenido sin necesitarlos. Con la
separación, el binario se lee únicamente cuando alguien descarga el archivo.

## 3. Migración de base de datos

Script en `codigo/sql/migracion_adjuntos.sql`, idempotente y ejecutable varias veces sin
efecto adicional. **Es bloqueante: sin correrlo en PERMS02, la funcionalidad falla.** Hace
cuatro cosas: vuelve anulable `RutaAlmacenamiento`, que queda en desuso pero se conserva por
trazabilidad del diseño aprobado; agrega `TipoMime` y `TamanoBytes` a `dbo.Adjunto`; crea
`dbo.AdjuntoContenido`; y crea el índice `IX_Adjunto_Solucion`.

## 4. Comportamiento funcional

Se adjunta en dos momentos. Al **registrar** una solución hay un campo opcional de archivo
con su tipo. Y desde **Ver detalle**, quien puede editar la solución —su dueño o un
Administrador, el mismo criterio que ya regía la edición— puede agregar más archivos hasta el
tope de cinco, o eliminar los existentes con confirmación previa.

Una decisión de diseño que conviene conocer: si el archivo adjunto no pasa la validación
durante el registro, **la solución se registra igual** y se avisa en pantalla por qué el
archivo no se guardó. Perder un formulario completo por un archivo rechazado sería peor que
el problema que resuelve, y el usuario puede volver a subirlo desde el detalle.

## 5. Controles de seguridad aplicados

Subir archivos es la superficie de ataque más peligrosa que se le puede agregar a una
aplicación web, y se está agregando inmediatamente después de la auditoría de seguridad. Los
controles implementados, en el orden en que actúan:

| # | Control | Qué previene |
|---|---|---|
| 1 | Límite de tamaño de la petición en la acción, además del límite por archivo de 10 MB | Agotamiento de memoria y de disco |
| 2 | Nombre saneado con `Path.GetFileName`, que descarta cualquier componente de directorio | Rutas del tipo `..\..\web.config`. El nombre solo se usa para mostrar y para el encabezado de descarga; **nunca** para construir una ruta |
| 3 | Lista **blanca** de extensiones: PDF, Word, Excel, PowerPoint, PNG y JPG | Ejecutables, y también `.html` y `.svg`, que son ejecutables de facto dentro de un navegador. Se eligió lista blanca y no lista negra porque con lista negra siempre queda una extensión peligrosa afuera |
| 4 | Verificación de la firma binaria del archivo contra la extensión declarada | Renombrar un ejecutable a `.pdf` |
| 5 | Descarga siempre como `application/octet-stream` y con `Content-Disposition: attachment` | Que un archivo subido por un usuario se interprete o ejecute dentro del dominio de la App — el mismo razonamiento del hallazgo crítico H-01 |
| 6 | El tipo de adjunto se normaliza contra la lista válida antes de grabar | Que un POST armado a mano viole el `CHECK` de la base |
| 7 | Subida y borrado exigen ser dueño o Administrador; ambos endpoints con token antifalsificación | Que un tercero adjunte o borre evidencia en una solución ajena |
| 8 | Límite de tasa en la subida, reutilizando la política ya existente | Abuso automatizado del endpoint |

**Punto abierto que corresponde al Comité, no al desarrollo:** la descarga está permitida a
cualquier usuario autenticado, igual que hoy lo está el detalle completo de cualquier
solución. Es coherente con el diseño vigente, pero un adjunto puede contener información más
sensible que los campos del formulario. Este punto debe confirmarse junto con el hallazgo
informativo H-13 de `11-auditoria-seguridad.md`; si la respuesta fuera restringir, el cambio
es acotado y se aplica en la acción de descarga.

## 6. Por qué NO hace falta tocar el servidor

El diseño se acotó deliberadamente a **un archivo por petición**, con un límite de 12 MB para
toda la petición. Ese valor queda por debajo del límite por defecto de IIS —unos 28,6 MB— así
que no hay que modificar `maxAllowedContentLength` en `web.config`. Esto evita de raíz el
problema documentado como D-04 en `12-errores-y-controles.md`: cada `dotnet publish` regenera
`web.config` y borra las ediciones manuales, de modo que cualquier límite configurado ahí se
perdería en la siguiente publicación y la funcionalidad se rompería sin causa aparente.

Si en el futuro se quisiera permitir varios archivos en una sola petición, ese límite pasaría
a ser obligatorio y se sumaría al procedimiento de reinserción del bloque de variables de
entorno.

## 7. Cambios de código

**Archivos nuevos:** `Models/Entities/AdjuntoContenido.cs`, `Services/AdjuntoService.cs`,
`sql/migracion_adjuntos.sql`.

**Archivos modificados:** `Models/Entities/Adjunto.cs`, `Data/AppDbContext.cs`,
`Data/Interfaces/ISolucionRepository.cs`, `Data/EfCore/EfSolucionRepository.cs`,
`Data/InMemory/InMemorySolucionRepository.cs`, `Models/ViewModels/SolucionViewModels.cs`,
`Controllers/SolucionController.cs`, `Views/Solucion/Create.cshtml`,
`Views/Solucion/Details.cshtml`, `Program.cs`.

## 8. Dos controles nuevos en el validador estático

Esta ronda agregó dos controles a `herramientas/validar_codigo.py`, ambos por riesgos reales
que aparecieron al construirla:

- **C-14 — interfaces implementadas por completo.** Agregar métodos a `ISolucionRepository`
  rompe la compilación de sus **dos** implementaciones, la de EF Core y la de memoria. Es un
  error fácil de cometer y que el compilador solo revela al final del ciclo.
- **C-15 — `enctype` en formularios con campo de archivo.** Sin
  `enctype="multipart/form-data"` el navegador envía solo el nombre del archivo, no su
  contenido, y el servidor recibe nulo. No hay error: los archivos simplemente no llegan. Es
  de las fallas más difíciles de diagnosticar justamente por lo silenciosa que es.

Estado de la batería de regresión tras esta ronda: 21 casos, 0 fallas.

## 9. Pruebas a realizar tras publicar

Antes de la prueba funcional hay que **correr la migración en PERMS02**; sin ella todo lo
relativo a adjuntos falla. Después conviene verificar: que al registrar una solución con un
PDF o una imagen el archivo quede listado en el detalle; que la descarga entregue el archivo
íntegro y con su nombre original; que subir un archivo con una extensión no permitida muestre
el mensaje de rechazo **y que la solución se registre igual**; que un archivo renombrado —por
ejemplo un `.txt` renombrado a `.pdf`— sea rechazado por la verificación de firma; que un
usuario que no es dueño ni Administrador no vea las opciones de adjuntar ni eliminar; y que
al eliminar una solución con adjuntos no queden filas huérfanas en `dbo.AdjuntoContenido`.
