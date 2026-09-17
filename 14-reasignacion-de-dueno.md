---
title: Registrador y Owner — dos personas distintas en cada registro
proyecto: Catalogo App IA
version: "2.1"
fecha: 2026-09-14
autor: Claude — a pedido de Luis Castro
depende_de: 03-alcance-funcional.md, 06-diseno-bd-sql-server.md, 11-auditoria-seguridad.md
---

# Registrador y Owner

Este documento cubre tres rondas consecutivas sobre el mismo tema. La 1.0 implementó que un
Administrador pudiera reasignar el dueño de un registro. La 2.0 corrigió el modelo de datos de
fondo, a propuesta de Luis Castro: **registrador y owner son dos personas distintas y deben ser
dos columnas distintas.** La 2.1 corrigió cómo se comunica todo esto en pantalla.

## 1. El problema que había en el modelo

`dbo.Solucion` tenía una sola columna de persona, `UsuarioDuenioId`, que respondía dos
preguntas diferentes: *¿a quién llamo por esta solución?* y *¿quién cargó este dato?*.

La evidencia de que estaban confundidas estaba en el propio código: el catálogo mostraba esa
columna con la etiqueta "Dueño", mientras que el comprobante de registro mostraba **la misma
columna** con la etiqueta "Registrado por". Mientras ambas cosas fueron la misma persona nadie
lo notó. Desde que un Administrador puede registrar a nombre de otro, el modelo quedó ambiguo.

Peor aún: en la versión 1.0, al reasignar el owner, la información de quién había registrado
originalmente **desaparecía de la fila** y sobrevivía solo dentro del texto de un comentario
del historial. Un dato de auditoría viviendo en prosa: no consultable, no filtrable, y frágil
ante cualquier cambio de formato de ese comentario.

## 2. El modelo actual

| Columna | Significado | Editable |
|---|---|---|
| `UsuarioDuenioId` | **Owner**: responsable de la solución en el negocio | Sí, por Administrador |
| `RegistradoPorId` | **Registrador**: quién cargó el registro en la App | No. Hecho histórico |

El owner es el dato **principal**: es lo que muestra el catálogo. El registrador aparece **solo
en el detalle y en el comprobante**, que es donde alguien lo busca.

No se renombró `UsuarioDuenioId`. "Dueño" y "owner" son la misma palabra, y un rename cuesta una
migración y toca todo el código para cero beneficio funcional. En la interfaz, donde ambos
conceptos conviven, se etiquetan "Dueño (owner)" y "Registrado por"; en el catálogo, donde solo
aparece uno, se mantiene "Dueño".

### 2.1 Por qué el owner es una llave foránea y no texto libre

Se evaluó guardar el owner como texto en la propia solución. Se descartó porque el permiso de
edición necesita una identidad real contra la cual comparar, porque la misma persona terminaría
escrita de varias formas distintas, y porque siendo un usuario de la App el owner puede entrar y
ver sus propias soluciones. Queda abierta la puerta a agregar más adelante columnas de atributos
del owner —cargo, gerencia— de forma aditiva. La entidad también conserva `SponsorTiId`,
diseñado en su momento y todavía sin uso, por si se quiere un tercer rol técnico.

## 3. Migración de base de datos

Script en `codigo/sql/migracion_registrador.sql`, idempotente. Agrega `RegistradoPorId`, rellena
los registros existentes, la vuelve obligatoria, crea la llave foránea y su índice.

> **Orden crítico.** El relleno es `RegistradoPorId = UsuarioDuenioId`, exacto **solo mientras
> nadie haya reasignado el owner de un registro**. Debe correrse **antes de que la reasignación
> se use en producción**; si se corriera después, los registros reasignados quedarían con el
> registrador equivocado y sin forma de recuperarlo.

## 4. Comportamiento

Al registrar, el registrador es siempre quien tiene la sesión abierta. Un Administrador puede,
además, indicar el owner —con una lista desplegable de los usuarios existentes— y si esa persona
todavía no ha ingresado nunca a la App, su usuario se precrea sin ningún rol. Al editar, un
Administrador puede cambiar el owner; el registrador se muestra como dato de solo lectura, con
una nota que aclara que cambiar el owner no reescribe quién cargó el registro.

**Permiso de edición:** owner o Administrador. Consecuencia a tener presente: tras una
reasignación, quien cargó el registro deja de poder corregirlo y debe pedírselo al owner o a un
Administrador.

**Comprobante por correo:** va **solo al registrador**, que es quien necesita la constancia de
haber hecho el trámite. Consecuencia: el owner no recibe aviso de que hay una solución
registrada a su nombre, y se enterará solo al entrar al catálogo. Revertirlo es agregar un
segundo envío en `SolucionService`.

## 5. Cómo se comunica en pantalla (corrección 2.1)

La primera versión mostraba, cada vez que se registraba a nombre de alguien que no había usado
nunca la App, un mensaje **en rojo**: *"Fulano@yanbal.com todavía no ha ingresado a la App; su
registro se creó sin roles."*

Estaba mal por tres razones. Se mostraba en rojo, que es el color de *algo falló*, cuando en
realidad todo había salido bien. Hablaba de "roles", un detalle interno que no significa nada
para quien está registrando una solución. Y se disparaba en el **camino feliz** de la
funcionalidad: registrar a nombre de alguien que aún no usa la App es el caso normal, no una
anomalía. Una advertencia que aparece siempre que todo sale bien solo enseña a ignorar las
advertencias — el mismo razonamiento por el que el validador estático lleva casos de control
para no producir falsos positivos.

La corrección mantiene la información y cambia el envase. El dato útil se incorpora al **mensaje
de éxito**, en verde y en una sola línea: *"Solución CD-2026-018 registrada exitosamente a nombre
de Carlos.Marrou@yanbal.com, que aún no ha ingresado a la App."* Mostrar el correo asignado sigue
cumpliendo su propósito real, que es que un error de tipeo se note de inmediato. El rojo queda
reservado para lo que de verdad falla: un archivo adjunto rechazado, o un correo de dueño que no
pasa la validación de dominio, caso en el que además se aclara que **no** se cambió el dueño.

Regla general para la App: el color rojo comunica que una acción no se completó. Confirmar el
resultado de una acción que sí se completó va en verde, aunque incluya una aclaración.

## 6. Trazabilidad

Cuando un Administrador registra a nombre de otra persona, la primera entrada del historial
guarda como usuario **al Administrador**, con un comentario que lo aclara. Toda reasignación de
owner queda anotada en el historial con ambos correos y con quién la hizo.

Anotar la reasignación en `HistorialEstado` es un **uso ampliado** de esa tabla, diseñada para
cambios de estado. Se eligió así porque es la bitácora que ya se muestra en "Ver detalle" y por
lo tanto donde alguien lo buscaría, y para no agregar otra migración.

## 7. Seguridad

Los campos de owner viven en el mismo objeto de entrada que el resto del formulario, pero el
controlador los **ignora por completo si quien envía el POST no es Administrador**, aunque los
incluya armando la petición a mano — el criterio del hallazgo H-03 de la auditoría. El correo del
owner se valida contra la lista de dominios corporativos.

`AsignacionDuenioService` precrea usuarios, así que conviene dejar explícito que **no es** el
`RoleResolutionService` eliminado en el hallazgo H-02. Aquel autenticaba a cualquiera sin
contraseña. Este no autentica a nadie: resuelve o precrea una fila sin roles, y sus dos únicos
llamadores exigen rol Administrador.

Riesgo conocido: un error de tipeo en el correo del owner crea un usuario fantasma, porque el
sistema no puede distinguir un correo mal escrito de alguien que aún no ha ingresado. Se mitiga
con la lista desplegable, la validación de dominio y **el correo asignado visible en el mensaje
de confirmación**, que es donde un tipeo se detecta.

## 8. Cambios de código

**Archivos nuevos:** `Services/DominiosCorporativos.cs`, `Services/AsignacionDuenioService.cs`,
`sql/migracion_registrador.sql`.

**Archivos modificados:** `Models/Entities/Solucion.cs`, `Data/AppDbContext.cs`,
`Services/SolucionService.cs`, `Controllers/SolucionController.cs`,
`Controllers/UsuarioController.cs`, `Models/ViewModels/SolucionViewModels.cs`,
`Views/Solucion/Create.cshtml`, `Views/Solucion/Editar.cshtml`,
`Views/Solucion/Details.cshtml`, `Views/Solucion/Comprobante.cshtml`, `Program.cs`.

## 9. Pendiente de propagar

`03-alcance-funcional.md` todavía define la edición como "solo si es quien la registró
originalmente (dueño)", equiparando ambos conceptos. Esa definición quedó superada por este
documento y conviene corregirla en su próxima revisión.

## 10. Pruebas tras publicar

Correr primero la migración. Luego verificar: que un usuario sin rol Administrador no vea el
bloque de owner; que al registrar normalmente el detalle muestre el mismo nombre en "Dueño
(owner)" y en "Registrado por"; que al registrar a nombre de otro el catálogo muestre al owner,
el detalle muestre a ambos, y el mensaje de confirmación sea **uno solo, verde, con el correo del
owner**; que el comprobante llegue por correo al registrador; que al reasignar el owner el campo
"Registrado por" **no cambie**; y que un correo de dominio externo sí produzca un mensaje rojo
aclarando que no se cambió el dueño.
