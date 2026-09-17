---
title: Propuesta de modelo de datos ampliado — Catálogo Citizen Development IA
proyecto: Catalogo App IA
version: "1.1"
fecha: 2026-09-07
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
depende_de: 01-diccionario-datos-excel.md
---

# Propuesta de modelo de datos ampliado

El Excel actual es un formulario plano de 21 columnas pensado para llenado manual. Al pasar a una App con base de datos propia (SQL Server en **PERMS02**, decisión ya tomada), conviene normalizar el modelo en varias tablas y agregar campos que hoy no existen pero que un repositorio único de Citizen Development necesita para ser realmente útil como control y no solo como listado. Todo el contenido del Excel se preserva; lo que sigue es **qué se propone agregar y por qué**, para que el Comité de Citizen Development lo valide antes de construir.

## 1. Entidades propuestas (visión lógica, se detalla en el documento de arquitectura)

- **Solucion** — el registro principal, equivalente a una fila de "Inventario".
- **Plataforma** — maestro de plataformas de IA aprobadas (hoy la hoja "Catálogo de plataformas"), ahora tabla editable por el Administrador, no lista fija.
- **HistorialEstado** — bitácora de auditoría: cada cambio de Estado de una Solución, quién lo hizo y cuándo.
- **Adjunto** — evidencia (capturas, actas de comité, documentos de aprobación) asociada a una Solución.
- **Usuario** — usuarios sincronizados desde Azure AD, con su(s) rol(es) en la App (Administrador / Aprobador / Usuario registrante — ver `03-alcance-funcional.md`) y su historial de soluciones registradas. Administrador y Aprobador son columnas booleanas independientes (`EsAdministrador`, `EsAprobador`): una persona puede tener una, la otra, ambas, o ninguna.

## 2. Campos nuevos propuestos sobre "Solucion"

| Campo propuesto | Tipo | Justificación |
|---|---|---|
| Correo del dueño (Owner Email) | Texto (AD) | Hoy solo hay "Usuario / dueño" como texto libre. Para notificaciones automáticas de revisión y trazabilidad real, el dueño debe resolverse contra Azure AD, no ser texto libre. |
| Enlace / URL de acceso a la solución | URL | El catálogo hoy no dice *dónde* está la solución (link al reporte Power BI, a la app, al bot). Sin esto, "consultar el repositorio" no permite realmente llegar a la solución. |
| Plataforma (FK) | Relación a Plataforma | Reemplaza el texto libre de "Herramienta utilizada" por una relación real contra el maestro editable, evitando duplicados tipo "Power BI" / "PowerBI" / "Power Bi". |
| Nivel de madurez / adopción | Lista: Piloto, En producción, Descontinuado | El Excel no distingue si algo sigue vivo. Sin esto, el catálogo acumula soluciones muertas mezcladas con las activas. |
| Clasificación de la información | Lista: Pública, Interna, Confidencial, Restringida | El campo actual "¿Usa datos personales (PII)?" es binario. Una clasificación de datos más granular es el estándar de gobierno de datos y complementa (no reemplaza) el cálculo de riesgo. |
| Audiencia estimada (nº de usuarios) | Numérico | Ayuda a priorizar revisión: no es lo mismo un reporte que ve 1 persona que uno que ve 200. |
| Sponsor / responsable de TI (si aplica) | Texto (AD, opcional) | Para soluciones Nivel 2/3 que requieren acompañamiento técnico, útil tener un contacto de TI además del dueño de negocio. |
| Fecha de baja / descontinuación | Fecha (opcional) | Cierra el ciclo de vida de la solución sin borrar el registro histórico. |
| Justificación de override de riesgo | Texto + usuario + fecha (opcional) | El nivel de riesgo se sigue calculando automáticamente (se preserva la regla del Excel), pero se agrega la posibilidad de que el Comité *documente* una excepción justificada, sin permitir que cualquiera edite el campo calculado directamente — igual que hoy P y T no son editables a mano. |
| Etiquetas / palabras clave | Texto multivalor | Mejora la búsqueda en el catálogo (ej. "clientes", "logística", "RRHH"). |

**Nota sobre la revisión periódica (columna T):** se recomienda llevar al Comité la posibilidad de que Nivel 3 también tenga una fecha de próxima revisión (por ejemplo cada 90 días) en vez de "N/A", ya que hoy el nivel más riesgoso es el único que no dispara un recordatorio automático. Esto es una recomendación, no un cambio aplicado — el cálculo original del Excel se mantiene como base.

## 3. Nueva tabla: HistorialEstado (no existe en el Excel)

El Excel solo guarda el *último* estado y la *última* fecha de revisión (columnas Q, R, S) — pierde el histórico de cómo llegó una solución de "Registrado" a "Rechazado", por ejemplo. Se propone una bitácora con: Solución (FK), Estado anterior, Estado nuevo, Usuario que hizo el cambio (AD), Fecha/hora, Comentario. Esto es lo que permite auditar decisiones del comité y es la base de datos real detrás del dashboard del Administrador — incluyendo, en el detalle de cada solución, mostrar qué usuario ejecutó cada cambio (retroalimentación de usuarios, 2026-09).

## 4. Nueva tabla: Adjunto (no existe en el Excel)

Campo: Solución (FK), Nombre de archivo, Tipo (Captura de pantalla, Acta de comité, Otro documento de soporte), Subido por, Fecha. Permite que el registro de una solución incluya evidencia real (por ejemplo, la captura del dashboard de Power BI, o el acta de aprobación del Comité N-1 para un Nivel 3), en vez de depender de la columna "Comentarios / observaciones" en texto libre.

## 5. Plataforma como maestro editable (confirmado con el usuario)

Se confirmó que **toda la información vive en base de datos**; el Excel es solo referencia de diseño. Por tanto la hoja "Catálogo de plataformas" (Plataforma, Categoría, Estado, Comentario) se implementa como tabla `Plataforma` con CRUD completo desde el módulo de Administración, y la columna "Herramienta utilizada" de cada Solución pasa a ser una relación (FK) contra esa tabla en vez de una lista de validación de Excel.

## 6. Usuario: rol Aprobador (2026-09)

Retroalimentación de usuarios en producción agregó un segundo rol interno sobre `Usuario`, independiente de `EsAdministrador`:

| Campo | Tipo | Justificación |
|---|---|---|
| EsAprobador | Bit | Permite que alguien participe en "Revisión y aprobación" (cambiar el Estado de una solución) sin necesitar los demás privilegios de Administrador (Plataformas, Dashboard ejecutivo, Gestión de Usuarios). Antes, el único rol distinto de "Usuario registrante" era Administrador — todo o nada. |

Ver `06-diseno-bd-sql-server.md` sección 7 para el script de migración (`ALTER TABLE`) y `03-alcance-funcional.md` para el detalle funcional del rol.

## 7. Resumen de decisiones ya tomadas (para trazabilidad)

- Todo el registro vive en SQL Server sobre **PERMS02** (base de datos nueva dedicada a esta App); el Excel deja de ser la fuente de verdad.
- El catálogo de plataformas de IA aprobadas es un maestro editable por el Administrador, no una lista fija.
- Stack: **ASP.NET Core MVC / Razor Pages** (monolito), consistente con el patrón on-prem de las demás apps de Yanbal.
- Autenticación: **Azure AD / Entra ID (OIDC / OAuth2)**.
- Rol Aprobador (2026-09): segunda columna booleana independiente sobre `Usuario`, gestionada desde "Gestión de Usuarios" junto con `EsAdministrador`.

Estas decisiones se detallan y diagraman en el documento de arquitectura, que se elabora **después** de aprobar la dirección visual de la App (ver propuestas de diseño).
