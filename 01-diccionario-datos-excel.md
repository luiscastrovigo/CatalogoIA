---
title: Diccionario de datos — Catálogo Citizen Development IA (origen Excel)
proyecto: Catalogo App IA
version: 1.0
fecha: 2026-09-03
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
fuente: Catalogo_Citizen_Development_IA.xlsx
---

# Diccionario de datos — archivo fuente

Este documento traduce a Markdown, sin pérdida de información, la estructura del Excel `Catalogo_Citizen_Development_IA.xlsx` que hoy se usa como plantilla manual de registro. Sirve como línea base para el modelo de datos de la nueva App y como referencia rápida de consulta. El archivo tiene tres hojas: **Inventario** (registro principal), **Catálogo de plataformas** (maestro de herramientas de IA aprobadas) y **Resumen** (tablero de indicadores calculado a mano).

## Hoja "Inventario"

Instrucción original de la plantilla: *"complete una fila por cada solución de Citizen Development. Las columnas J a O activan el cálculo automático de la columna 'Nivel de riesgo' (P) — no editar P ni T manualmente."*

| Col | Campo | Tipo de dato | Valores permitidos / origen | Notas |
|---|---|---|---|---|
| A | ID | Texto, correlativo | Formato `CD-AAAA-###` (ej. `CD-2026-001`) | Identificador único de la solución |
| B | Nombre de la solución | Texto libre | — | — |
| C | Tipo | Lista desplegable | Dashboard, Reporte, Automatización, Agente de IA, App, Bot, Otro | — |
| D | Descripción breve | Texto libre | — | — |
| E | Usuario / dueño | Texto libre | — | Nombre de la persona que registra/posee la solución |
| F | Área | Texto libre | — | Área organizacional del dueño |
| G | País | Lista desplegable | Peru, Colombia, Ecuador, Bolivia, Mexico, Guatemala, España, Italia, Corporativo | — |
| H | Herramienta utilizada | Lista desplegable (dependiente) | Referencia al rango con nombre `PlataformasAprobadas` (ver hoja "Catálogo de plataformas") | Hoy es una lista fija; ver propuesta de maestro editable en el documento de modelo ampliado |
| I | Fecha de creación | Fecha | — | — |
| J | ¿Es parte de un proceso crítico de negocio? | Lista desplegable | Si, No | Entra en el cálculo de riesgo (P) |
| K | Alcance de uso | Lista desplegable | Personal, Departamental, Multi-área, Multi-país | Entra en el cálculo de riesgo (P) |
| L | ¿Requiere conexión a sistema central / Data Lake? | Lista desplegable | Si, No | Entra en el cálculo de riesgo (P) |
| M | ¿Usa datos personales (PII)? | Lista desplegable | Si, No | Entra en el cálculo de riesgo (P) |
| N | ¿Requiere publicación a Internet? | Lista desplegable | Si, No | Entra en el cálculo de riesgo (P) |
| O | ¿Requiere infraestructura dedicada? | Lista desplegable | Si, No, Entorno compartido, Sandbox personal | Entra en el cálculo de riesgo (P) |
| P | Nivel de riesgo | **Calculado, no editable** | Nivel 1, Nivel 2, Nivel 3 | Ver fórmula abajo |
| Q | Estado | Lista desplegable | Registrado, En revisión, Aprobado - Comité Citizen Dev, Aprobado - Comité N-1, Rechazado, Reclasificado | Flujo de aprobación |
| R | Revisado por | Lista desplegable | N/A - autoregistro, Comité de Citizen Development, Comité N-1 | — |
| S | Fecha de última revisión | Fecha | — | — |
| T | Próxima revisión periódica | **Calculado, no editable** | Fecha o "N/A" | Ver fórmula abajo |
| U | Comentarios / observaciones | Texto libre | — | — |

### Fórmula del Nivel de riesgo (columna P)

```
SI J="Si" O L="Si" O M="Si" O N="Si" O O="Si"  →  Nivel 3
SI NO, Y K en {Departamental, Multi-área, Multi-país}  →  Nivel 2
EN CUALQUIER OTRO CASO  →  Nivel 1
```

En criollo: **cualquier señal de criticidad, PII, conexión a Data Lake, exposición a Internet o infraestructura dedicada dispara automáticamente Nivel 3**, sin importar el alcance de uso. Si no hay ninguna señal de riesgo "duro" pero el alcance ya no es solo personal, sube a Nivel 2. Solo el uso estrictamente personal y sin señales de riesgo queda en Nivel 1.

### Fórmula de la Próxima revisión periódica (columna T)

```
SI Nivel de riesgo en {Nivel 1, Nivel 2}  →  Fecha de última revisión + 180 días
SI Nivel de riesgo = Nivel 3  →  "N/A"
```

⚠️ **Observación para validar con el Comité de Citizen Development**: tal como está la plantilla, las soluciones de **Nivel 3 (el más alto riesgo) no reciben una fecha de próxima revisión automática** — quedan en "N/A". Es probable que esto asuma que el Nivel 3 ya tiene seguimiento manual por el comité ejecutivo (N-1), pero conviene confirmarlo explícitamente: en el documento de modelo ampliado se propone una alternativa (revisión periódica más corta para Nivel 3, p. ej. 90 días, en vez de N/A).

## Hoja "Catálogo de plataformas"

Nota original: *"Catálogo de plataformas de IA corporativa aprobadas para Citizen Development. PENDIENTE DE DEFINICIÓN — la fila 3 es un ejemplo de formato, complete con el catálogo real."*

| Campo | Tipo | Notas |
|---|---|---|
| Plataforma | Texto | Ej: "Microsoft 365 Copilot" |
| Categoría | Texto | Ej: "Transversal" |
| Estado | Texto | Ej: "En evaluación" |
| Comentario | Texto libre | — |

Esta hoja alimenta la lista desplegable de la columna H (Herramienta utilizada) en "Inventario" vía el rango con nombre `PlataformasAprobadas`, y hoy está vacía/pendiente de definición oficial. **Decisión ya tomada con el usuario**: en la nueva App este catálogo deja de vivir en Excel y pasa a ser una tabla maestra en la base de datos SQL Server, administrable desde el módulo de Administración (ver `03-alcance-funcional.md`).

## Hoja "Resumen"

Tablero de indicadores calculado manualmente sobre los datos de "Inventario":

- **Soluciones por nivel de riesgo**: conteo de filas por Nivel 1 / Nivel 2 / Nivel 3, más el total registrado.
- **Soluciones por estado**: conteo de filas por cada valor de la columna Estado (Registrado, En revisión, Aprobado - Comité Citizen Dev, Aprobado - Comité N-1, Rechazado, Reclasificado).

Este es exactamente el tipo de vista que la nueva App debe generar **en vivo y sin mantenimiento manual** en el módulo de Dashboard del Administrador — ver el documento de modelo ampliado y, más adelante, el documento de arquitectura.

## Filas de ejemplo detectadas

El Excel trae 4 filas de ejemplo con fondo gris (`CD-2026-001` a `CD-2026-004`), marcadas explícitamente para borrarse antes del uso real. Se conservan aquí solo como referencia de qué tan variado puede ser el registro real (desde un dashboard personal de Power BI sin riesgo, hasta un agente de IA con PII y conexión a CRM que requirió aprobación ejecutiva, y un chatbot público rechazado por exposición sin validación de seguridad):

| ID | Nombre | Tipo | País | Herramienta | Nivel de riesgo | Estado |
|---|---|---|---|---|---|---|
| CD-2026-001 | Dashboard personal de seguimiento de tickets | Dashboard | Peru | Power BI | Nivel 3* | Registrado |
| CD-2026-002 | Reporte de ventas por consultora - Ecuador | Reporte | Ecuador | Power BI | Nivel 2 | En revisión |
| CD-2026-003 | Agente de seguimiento comercial con datos de clientes | Agente de IA | Corporativo | Copilot Studio | Nivel 3 | Aprobado - Comité N-1 |
| CD-2026-004 | Portal público de FAQ para clientes | App | Corporativo | Claude | Nivel 3 | Rechazado |

\* CD-2026-001 califica Nivel 3 porque marca "Si" en "¿Es parte de un proceso crítico de negocio?", aun siendo de alcance personal — ilustra bien que una sola señal de riesgo basta para escalar el nivel, independientemente del alcance.
