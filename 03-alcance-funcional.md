---
title: Alcance funcional (borrador) — Catálogo Citizen Development IA
proyecto: Catalogo App IA
version: "1.1"
fecha: 2026-09-07
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
depende_de: 01-diccionario-datos-excel.md, 02-modelo-datos-ampliado.md, 08-reglas-negocio-calculo-riesgo.md
---

# Alcance funcional (borrador)

Este documento define, a alto nivel, los módulos y roles de la App para poder construir las propuestas visuales. Es un borrador de trabajo: el documento funcional completo se formaliza junto con la arquitectura, una vez aprobada la dirección de diseño.

## Objetivo de la App

Repositorio único donde los equipos de Yanbal registran las soluciones de Citizen Development / IA que están construyendo (dashboards, reportes, agentes de IA, apps, bots), para que TI y el Comité de Citizen Development puedan **consultar, clasificar por riesgo y aprobar** esas construcciones, reemplazando el control manual hoy llevado en Excel.

## Roles

### 1. Usuario registrante (cualquier colaborador autenticado con Azure AD)

- Registra una nueva solución (formulario equivalente a una fila de "Inventario", ver `02-modelo-datos-ampliado.md`). Al registrar exitosamente, la pantalla muestra un mensaje de confirmación y queda en blanco lista para un nuevo registro, con un enlace opcional al comprobante del registro recién hecho.
- Consulta el catálogo de soluciones ya registradas (con filtros: país, área, tipo, plataforma, nivel de riesgo, estado).
- Ve el detalle de sus propias soluciones y su historial de estado (incluyendo qué usuario ejecutó cada cambio de estado).
- **Edita una solución solo si es quien la registró originalmente** (dueño) — desde "Ver Detalle" del Catálogo, con un botón "Editar" que abre un formulario pre-cargado y un botón "Grabar" que actualiza el registro (recalculando el nivel de riesgo si cambia algún campo de riesgo). No puede eliminar registros — eso es exclusivo de Administrador.

### 2. Aprobador (rol nuevo, 2026-09)

Rol independiente de Administrador, pensado para quien participa en el Comité de aprobación sin necesitar los demás privilegios administrativos:

- Todo lo del rol Usuario registrante.
- Acceso al módulo **Revisión y aprobación**: puede cambiar el Estado de una solución (Registrado → En revisión → Aprobado / Rechazado / Reclasificado), quedando registrado en `HistorialEstado` junto con su propio usuario como quien ejecutó el cambio.
- **No** tiene acceso a Administración de plataformas, Dashboard ejecutivo, ni Gestión de Usuarios — esos siguen siendo exclusivos de Administrador.
- El rol se otorga/retira desde "Gestión de Usuarios" (antes "Gestión de administradores"), igual que Administrador — son dos casillas independientes, una persona puede tener una, la otra, ambas, o ninguna.

### 3. Administrador (Comité de Citizen Development / Comité N-1)

Todo lo del rol Usuario registrante y de Aprobador, más:

- **Identificación de administrador**: a diferencia de un enfoque basado en grupos de Azure AD, el rol Administrador se gestiona **dentro de la propia App** (columna `Usuario.EsAdministrador` en base de datos, ver `06-diseno-bd-sql-server.md`). Azure AD solo confirma la identidad (login corporativo); quién tiene el rol de Administrador lo decide la App. El primer administrador se precarga por script de base de datos (`sql/02_seed_inicial.sql`); a partir de ahí, cualquier Administrador puede otorgar o quitar el rol de Administrador y/o de Aprobador a otros usuarios desde el módulo "Gestión de Usuarios" (módulo 7).
- Revisa soluciones pendientes y cambia su Estado (mismo flujo que Aprobador).
- **Puede editar cualquier solución** desde "Ver Detalle" del Catálogo, sin importar quién la registró (a diferencia del Usuario registrante, que solo edita las propias).
- **Puede eliminar registros del Catálogo** — acción exclusiva de Administrador, con confirmación explícita antes de borrar (borra en cascada el historial de estado, notificaciones y adjuntos asociados).
- Administra el maestro de **Plataformas de IA aprobadas** (alta, edición, baja/estado) — este maestro puede empezar vacío e irse completando conforme se registran soluciones reales (ver `05-documento-cero-prerequisitos.md`, sección 4).
- Accede al **Dashboard ejecutivo** (exclusivo de administrador) con las gráficas consolidadas del catálogo.
- Puede registrar una solución en nombre de otro usuario (para migrar datos históricos del Excel).
- Otorga o retira el rol de Administrador y/o de Aprobador a otros usuarios registrados (módulo 7) — con una validación de seguridad: no se puede auto-revocar el rol de Administrador si es el único activo, para no dejar la App sin nadie que administre (esta misma protección no aplica a Aprobador, que puede quedar en cero sin bloquear la operación).

## Módulos

1. **Autenticación** — login corporativo vía Azure AD (OIDC/OAuth2). Azure AD solo confirma **quién es** el usuario (identidad, correo corporativo); **qué puede hacer** (Usuario registrante / Aprobador / Administrador) lo resuelve la propia App consultando `Usuario.EsAdministrador` y `Usuario.EsAprobador` en base de datos — no depende de grupos de AD.
2. **Registro de solución** — formulario de alta de una nueva solución de Citizen Development, con los campos del Excel más los campos ampliados propuestos (enlace, plataforma como maestro, clasificación de datos, etc.). El **nivel de riesgo y la próxima revisión se calculan y se muestran en vivo** mientras el usuario completa los campos que los determinan (datos personales, alcance, conexión a sistema central, decisión automatizada, impacto financiero, terceros externos) — igual que en el Excel original, el usuario **nunca edita el nivel de riesgo a mano**, solo ve el badge actualizarse. **Al culminar el registro exitosamente, se muestra un mensaje de confirmación y la pantalla vuelve a un formulario en blanco** (listo para un siguiente registro sin navegación adicional), con un enlace opcional para ver el comprobante completo (ID asignado, nivel de riesgo calculado, resumen de lo registrado) del registro recién hecho; el sistema también envía automáticamente una notificación por correo al usuario registrante como constancia del registro.
3. **Catálogo / consulta** — listado buscable y filtrable de todas las soluciones registradas, con vista de detalle por solución (incluye evidencia adjunta, historial de estado con el usuario que ejecutó cada cambio, y edición del registro para quien tenga permiso). Solo Administrador ve la opción de eliminar un registro directamente desde el listado.
4. **Administración de plataformas** — CRUD del maestro de plataformas de IA aprobadas (solo Administrador).
5. **Revisión y aprobación** — cambio de estado de una solución por Administrador o Aprobador, con comentario obligatorio, generando entrada en `HistorialEstado` que identifica al usuario que hizo el cambio.
6. **Dashboard ejecutivo (solo Administrador)** — consolidado visual equivalente a la hoja "Resumen" del Excel pero en vivo: soluciones por nivel de riesgo, por estado, y cruces adicionales que el Excel no tenía (por país, por área, por tipo de solución, por plataforma, evolución en el tiempo, soluciones próximas a revisión periódica).
7. **Gestión de Usuarios (solo Administrador)** — antes "Gestión de administradores"; renombrado al incorporar el rol Aprobador. Listado de usuarios que ya se han autenticado alguna vez en la App (o invitados por correo antes de su primer ingreso), con la posibilidad de otorgar o retirar, de forma independiente, el rol de Administrador y el rol de Aprobador. Es el módulo que reemplaza la dependencia de grupos de Azure AD: el control de quién administra y quién aprueba en el catálogo vive aquí, no en el directorio corporativo.

## Pantallas clave para las propuestas visuales

Para que las 3-4 direcciones de diseño sean comparables, se ilustran las mismas 4 pantallas en cada una:

1. **Login** (Azure AD / Entra ID)
2. **Catálogo de soluciones** (listado + filtros)
3. **Registro de solución** (formulario de alta)
4. **Comprobante de registro** (confirmación en pantalla + aviso de notificación enviada por correo, como prueba del registro)
5. **Dashboard ejecutivo** (solo Administrador, con gráficas)

## Fuera de alcance de este borrador

El diseño detallado de la plantilla de correo de notificación, la integración con Data Lake/sistema central, el diagrama de componentes lógico/físico, la presentación ejecutiva (PPT) e infografía — se desarrollan **después** de elegir la dirección visual, para que reflejen la identidad ya aprobada. La *existencia* de la notificación de comprobante sí queda confirmada como requisito desde ahora.
