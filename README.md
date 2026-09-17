# Catálogo de Soluciones Citizen Development

Aplicación web corporativa de Yanbal para registrar, clasificar por riesgo, revisar y aprobar las soluciones construidas por usuarios de negocio (Citizen Development) y con plataformas de IA.

Reemplaza el registro manual en Excel (`Catalogo_Citizen_Development_IA.xlsx`) por una aplicación con identidad corporativa, cálculo automático de nivel de riesgo, flujo de revisión, comprobante de registro por correo y trazabilidad completa.

---

## 1. Stack

| Componente | Tecnología |
|---|---|
| Aplicación | ASP.NET Core MVC — .NET 8 (LTS) |
| Autenticación | Microsoft Entra ID (OIDC / Authorization Code + PKCE) vía `Microsoft.Identity.Web` |
| Acceso a datos | Entity Framework Core 8 |
| Base de datos | SQL Server — servidor **PERMS02**, base `YanbalCitizenDevIA` |
| Notificaciones | Microsoft Graph (`Mail.Send`) |
| Hosting | IIS en **PERMS220**, ruta `/CatalogoIA/`, detrás del reverse proxy `proxynp.unique-yanbal.com` |

Los roles (Administrador, Aprobador) se gestionan **dentro de la aplicación** (`Usuario.EsAdministrador`, `Usuario.EsAprobador`), no por grupos de Entra ID. Entra ID solo autentica.

---

## 2. Estructura del repositorio

```
.
├── 00-README.md … 15-publicacion-git.md          Documentación funcional y técnica (16 documentos)
├── PREPARAR-GIT.md                               Guía de publicación en GitHub
├── Catalogo_Citizen_Development_IA.xlsx          Excel original que originó el proyecto
├── sql/                                          Scripts de base de datos
│   ├── 01_schema_YanbalCitizenDevIA.sql          Creación completa desde cero
│   ├── 02_seed_inicial.sql                       Administrador inicial
│   ├── migracion_adjuntos.sql                    Incremental: adjuntos
│   └── migracion_registrador.sql                 Incremental: registrador / owner
└── codigo/
    ├── CatalogoCitizenDevIA.sln
    ├── README-DEV.md                             Guía para el desarrollador
    ├── web.config.ejemplo                        Plantilla SIN secretos (ver sección 4)
    ├── herramientas/                             Validación estática previa a compilar
    │   ├── validar_codigo.py
    │   ├── probar_validador.py
    │   └── verificar_antes_de_subir.ps1
    └── src/
        ├── CatalogoCitizenDevIA.Web/             Aplicación
        └── CatalogoCitizenDevIA.Tests/           Pruebas
```

### Mapa de la documentación

| # | Documento | Contenido |
|---|---|---|
| 00 | [Índice del proyecto](00-README.md) | Índice y contexto general del proyecto |
| 01 | [Diccionario de datos del Excel](01-diccionario-datos-excel.md) | Diccionario del Excel de origen |
| 02 | [Modelo de datos ampliado](02-modelo-datos-ampliado.md) | Entidades, atributos y relaciones |
| 03 | [Alcance funcional](03-alcance-funcional.md) | Módulos, roles y permisos |
| 04 | [Identidad visual](04-identidad-visual.md) | Paleta, tipografía y componentes Yanbal |
| 05 | [Documento cero — prerrequisitos](05-documento-cero-prerequisitos.md) | Condiciones habilitantes antes de construir |
| 06 | [Diseño de base de datos](06-diseno-bd-sql-server.md) | DDL de SQL Server en PERMS02 |
| 07 | [Arquitectura y componentes](07-arquitectura-despliegue-componentes.md) | Arquitectura de despliegue |
| 08 | [Reglas de negocio y riesgo](08-reglas-negocio-calculo-riesgo.md) | Cálculo del nivel de riesgo |
| 09 | [Estado de implementación](09-estado-implementacion-pruebas.md) | Avance y bitácora de pruebas |
| 10 | [Guía de despliegue PERMS220](10-guia-despliegue-perms220.md) | Publicación en IIS paso a paso |
| 11 | [Auditoría de seguridad](11-auditoria-seguridad.md) | Hallazgos y remediación |
| 12 | [Errores y controles](12-errores-y-controles.md) | Errores históricos y validación estática |
| 13 | [Adjuntos](13-adjuntos-diseno-y-seguridad.md) | Diseño y seguridad de archivos adjuntos |
| 14 | [Registrador y owner](14-reasignacion-de-dueno.md) | Separación de roles y reasignación |
| 15 | [Publicación en GitHub](15-publicacion-git.md) | Control de versiones y puerta de secretos |

Guía operativa para publicar cambios: **[PREPARAR-GIT.md](PREPARAR-GIT.md)**.

---

## 3. Puesta en marcha (desarrollo)

```powershell
# 1. Restaurar y compilar
dotnet restore codigo\CatalogoCitizenDevIA.sln
dotnet build   codigo\CatalogoCitizenDevIA.sln -c Release

# 2. Configurar los secretos locales (NUNCA en appsettings.json)
dotnet user-secrets set "ConnectionStrings:Default" "Server=...;Database=...;..." --project codigo\src\CatalogoCitizenDevIA.Web
dotnet user-secrets set "AzureAd:TenantId"     "..." --project codigo\src\CatalogoCitizenDevIA.Web
dotnet user-secrets set "AzureAd:ClientId"     "..." --project codigo\src\CatalogoCitizenDevIA.Web
dotnet user-secrets set "AzureAd:ClientSecret" "..." --project codigo\src\CatalogoCitizenDevIA.Web

# 3. Ejecutar
dotnet run --project codigo\src\CatalogoCitizenDevIA.Web
```

Antes de cada `dotnet publish`, ejecutar la validación estática (ver `12-errores-y-controles.md`):

```powershell
python codigo\herramientas\validar_codigo.py codigo\src
```

Publicación a IIS: ver `10-guia-despliegue-perms220.md`.

---

## 4. Seguridad — qué NO entra a este repositorio

Los siguientes valores son secretos de producción y **solo** viven en variables de entorno del App Pool de IIS en PERMS220:

| Variable de entorno | Contenido |
|---|---|
| `ConnectionStrings__Default` | Cadena de conexión a `YanbalCitizenDevIA` en PERMS02, con contraseña |
| `AzureAd__TenantId` | Tenant de Entra ID |
| `AzureAd__ClientId` | App Registration |
| `AzureAd__ClientSecret` | Secreto de la App Registration |
| `Notificaciones__RemitenteCorreo` | Buzón remitente del comprobante |
| `DataProtection__KeysPath` | Carpeta de llaves de Data Protection |
| `ForwardedHeaders__KnownProxies` | IP del reverse proxy |

Por eso `.gitignore` excluye **`web.config` en cualquier carpeta** (ese archivo contiene el bloque `<environmentVariables>` con los valores reales) y también `publish/`, `bin/`, `obj/` y los `.zip`. La plantilla versionada es `codigo/web.config.ejemplo`, con marcadores en lugar de valores.

Antes de cada `git push`, ejecutar:

```powershell
powershell -ExecutionPolicy Bypass -File codigo\herramientas\verificar_antes_de_subir.ps1
```

`appsettings.json` está pensado para no contener secretos nunca; el control **C-04** de `validar_codigo.py` lo verifica automáticamente.

---

## 5. Base de datos

Despliegue nuevo desde cero:

```
sql\01_schema_YanbalCitizenDevIA.sql
sql\02_seed_inicial.sql
```

Sobre una base ya existente en PERMS02, aplicar los incrementales en orden:

```
sql\migracion_adjuntos.sql
sql\migracion_registrador.sql
```

> `migracion_registrador.sql` hace un backfill `RegistradoPorId = UsuarioDuenioId`. Ese backfill solo es correcto **antes** de que se use en producción la reasignación de owner. Detalle en `14-reasignacion-de-dueno.md`.

---

## 6. Responsables

| Rol | Persona |
|---|---|
| Responsable técnico / arquitectura | Luis Castro — IT Architecture Consulting |
| Dueño de negocio | Comité de Citizen Development |
