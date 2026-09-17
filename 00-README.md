---
title: Catálogo Citizen Development IA — Índice del caso
proyecto: Catalogo App IA
version: 1.0
fecha: 2026-09-03
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
---

# Catálogo de Citizen Development & IA — Índice del caso

Documentación previa a la implementación de la App para el registro único de soluciones de Citizen Development / IA construidas por los equipos de Yanbal. Sigue este orden de lectura:

| # | Documento | Contenido |
|---|---|---|
| 01 | `01-diccionario-datos-excel.md` | Estructura del Excel base convertida a Markdown: campos, listas de validación, fórmulas de Nivel de riesgo y Próxima revisión |
| 02 | `02-modelo-datos-ampliado.md` | Propuesta de campos y tablas nuevas sobre el Excel (auditoría de estados, adjuntos, maestro de plataformas en BD, etc.) |
| 03 | `03-alcance-funcional.md` | Roles (Usuario registrante / Administrador), módulos y pantallas clave — incluye el requisito de comprobante de registro |
| 04 | `04-identidad-visual.md` | Dirección de diseño aprobada: paleta (naranja/dorado Yanbal), tipografía, componentes — enlace al canvas de diseño |
| 05 | `05-documento-cero-prerequisitos.md` | **Documento Cero** — checklist de accesos e infraestructura a resolver antes de programar (servidor de app, BD en PERMS02, Azure AD, catálogo real de plataformas) |
| 06 | `06-diseno-bd-sql-server.md` | Modelo entidad-relación y script DDL completo para SQL Server en PERMS02 (ver también carpeta `sql/`) |
| 07 | `07-arquitectura-despliegue-componentes.md` | Diagrama lógico de componentes y diagrama físico de despliegue, ambientes y seguridad de la cuenta de servicio |
| 08 | `08-reglas-negocio-calculo-riesgo.md` | **Regla de negocio del Nivel de riesgo** — fórmula exacta del Excel, cómo vive en SQL (`fn_NivelRiesgo`) y en la App (`RiesgoCalculator` en C#, recálculo en vivo en el formulario), casos de prueba y punto abierto sobre Nivel 3 sin revisión periódica |
| 09 | `09-estado-implementacion-pruebas.md` | **Estado de implementación** — primera versión de la App construida en ASP.NET Core MVC (.NET 8), desplegada y probada (32/32 verificaciones), y qué falta para el entorno real de PERMS02/Entra ID |
| 10 | `10-guia-despliegue-perms220.md` | **Guía de despliegue paso a paso en PERMS220** — instalación del Hosting Bundle, sitio/App Pool en IIS, publicación, secretos, App Registration en Entra ID, reglas de red, reverse proxy y checklist de troubleshooting |

## Scripts de base de datos listos para ejecutar

Carpeta `sql/`:

- `01_schema_YanbalCitizenDevIA.sql` — creación de la base de datos, tablas, función de cálculo de riesgo, índices y procedimiento del correlativo `CD-AAAA-###`.
- `02_seed_inicial.sql` — usuario administrador inicial (reemplazar el `AzureAdObjectId` de ejemplo por el real antes de ejecutar).

## Diseño visual

Las 4 propuestas exploradas y el diseño final aprobado (Studio Claro, refinado, logo en naranja corporativo) están publicados en un canvas interactivo:

**https://claude.ai/code/artifact/c50301f0-3c65-48c1-a0ec-e9a0e464be5a**

## Stack técnico definido

- **Aplicación**: ASP.NET Core MVC / Razor Pages (.NET 8)
- **Autenticación**: Azure AD / Microsoft Entra ID (OIDC / OAuth2)
- **Base de datos**: SQL Server nueva (`YanbalCitizenDevIA`) en el servidor **PERMS02**
- **Notificaciones**: Microsoft Graph API (comprobante de registro por correo)

## Decisiones ya resueltas (ver Documento Cero, sección 8)

Ya no hay bloqueantes de negocio pendientes:

1. **Catálogo real de plataformas de IA aprobadas** — no bloqueante; el maestro `Plataforma` sale vacío y se completa progresivamente desde el módulo de Administración de plataformas.
2. **Rol Administrador** — se gestiona dentro de la propia App (`Usuario.EsAdministrador`), no vía grupos de Azure AD. Azure AD solo autentica el acceso.

## Próximos pasos sugeridos

1. Solicitar al DBA la creación de la base de datos en PERMS02 usando `sql/01_schema_YanbalCitizenDevIA.sql` y correr `sql/02_seed_inicial.sql` para dejar el primer administrador.
2. Iniciar la App Registration en Entra ID y la creación del repositorio en Azure DevOps.
3. Ejecutar la guía de despliegue en PERMS220 (`10-guia-despliegue-perms220.md`) con Infraestructura, IT Seguridad y el DBA en paralelo.
4. Reemplazar las tres piezas "Dev" del código (persistencia, autenticación, notificaciones) por sus versiones reales una vez que PERMS02, la App Registration de Entra ID y el permiso de Microsoft Graph estén listos — ver la tabla de reemplazos en `09-estado-implementacion-pruebas.md`.
