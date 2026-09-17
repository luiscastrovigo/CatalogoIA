# Catálogo Citizen Development & IA — código fuente

## Estado actual: integración real escrita, PENDIENTE DE COMPILAR Y VALIDAR

Este código pasó por dos rondas de construcción:

1. **Primera ronda** (sandbox sin salida de red hacia NuGet.org ni hacia la red interna de
   Yanbal): una implementación 100% funcional usando solo lo que trae el SDK de .NET 8 —
   login de desarrollo, repositorios en memoria, notificación por archivo. Probada de punta a
   punta (32/32 verificaciones) y ya desplegada en PERMS220 como primera prueba de
   infraestructura.
2. **Segunda ronda (código actual de `Program.cs`, `Data/EfCore/*`, `GraphNotificacionService`,
   `RoleClaimsTransformation`)**: la integración real —
   - `Data/AppDbContext.cs` + `Data/EfCore/*` — EF Core contra SQL Server (PERMS02), mapeado al
     esquema de `06-diseno-bd-sql-server.md` / `sql/01_schema_YanbalCitizenDevIA.sql`.
   - `Microsoft.Identity.Web` — login real contra la App Registration de Entra ID de Yanbal.
   - `Services/GraphNotificacionService.cs` — comprobante de registro real por Microsoft Graph
     (`Mail.Send`).

   **Este código NO se pudo compilar ni ejecutar en el sandbox donde se escribió** — el mismo
   sandbox de la primera ronda sigue sin salida a NuGet.org, y esta ronda sí depende de paquetes
   reales (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.Identity.Web`, `Microsoft.Graph`,
   `Azure.Identity`, `Microsoft.Data.SqlClient` — ver el `.csproj`). Se escribió con el máximo
   cuidado posible siguiendo los patrones estándar de cada librería, pero **hay que validarlo
   antes de considerarlo listo** — ver la sección siguiente.

## Validación pendiente (hacer esto ANTES de desplegar a producción)

En una máquina con salida a internet (o directamente en PERMS220, una vez que tenga el SDK de
.NET 8, no solo el runtime):

```bash
cd src/CatalogoCitizenDevIA.Web
dotnet restore
dotnet build
```

Si algún paquete `"X.*"` no resuelve (versión retirada o rango sin match), correr
`dotnet add package <nombre>` sin especificar versión para tomar la última estable, y ajustar
el `.csproj`.

Con el build en verde, probar contra infraestructura de **prueba**, no de producción, en este
orden:

1. **Base de datos**: una instancia de SQL Server de prueba (puede ser local) con el esquema de
   `sql/01_schema_YanbalCitizenDevIA.sql` aplicado. Configurar `ConnectionStrings__Default` como
   variable de entorno y confirmar que el catálogo carga, que se puede registrar una solución
   (esto ejercita `dbo.sp_GenerarCodigoSolucion` vía `EfSolucionRepository.GenerarCodigoSolucion`)
   y que `NivelRiesgo`/`ProximaRevision` quedan calculados por SQL Server (no por C#) al leer de
   vuelta el registro guardado.
2. **Entra ID**: una App Registration de prueba (o la ya creada, apuntando primero a un ambiente
   de UAT) con `AzureAd__TenantId/ClientId/ClientSecret` como variables de entorno. Confirmar
   login, que el primer ingreso da de alta el `Usuario` automáticamente
   (`RoleClaimsTransformation`), y que "Gestión de administradores" resuelve el rol en vivo.
3. **Microsoft Graph**: con el permiso `Mail.Send` (aplicación) consentido — ver
   `10-guia-despliegue-perms220.md` sección 5.1 — registrar una solución y confirmar que el
   correo de comprobante llega de verdad a la bandeja del destinatario.

Solo después de estos tres puntos en verde tiene sentido apuntar esta build a
PERMS02/Entra ID/Graph de **producción**.

## Cómo correrlo (una vez validado)

```bash
cd src/CatalogoCitizenDevIA.Web
dotnet run
```

Requiere las variables de entorno de `10-guia-despliegue-perms220.md` sección 4
(`ConnectionStrings__Default`, `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientSecret`,
`Notificaciones__RemitenteCorreo`) ya configuradas — sin ellas la app falla al arrancar
(`AddDbContext`/el `GraphServiceClient` las requieren).

## Cómo correr las pruebas funcionales

El harness HTTP (`CatalogoCitizenDevIA.Tests`, sin dependencias externas) se escribió contra el
login de desarrollo de la primera ronda, así que **tal cual solo sirve mientras la app corra con
ese login** (por ejemplo, contra una build vieja, o adaptándolo para autenticarse contra el flujo
real de Entra ID):

```bash
dotnet run --project src/CatalogoCitizenDevIA.Tests -- <url-de-la-app>
```

Última corrida completa (primera ronda, login de desarrollo): **32/32 verificaciones OK** — ver
`09-estado-implementacion-pruebas.md`.

## Qué reemplazó a qué

| Pieza | Primera ronda (sandbox, ya retirada de `Program.cs`) | Ahora (segunda ronda, código actual) |
|---|---|---|
| Persistencia | `Data/InMemory/*` | `Data/EfCore/*` contra SQL Server (PERMS02) — mismas interfaces (`IUsuarioRepository`, `IPlataformaRepository`, `ISolucionRepository`), ningún controlador/vista cambió |
| Autenticación | `AccountController.Login` (cookie + formulario que simulaba Azure AD) — **retirado** | `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApp`) contra la App Registration real de Entra ID |
| Alta automática de usuario y resolución de rol | `RoleResolutionService` (login) + `RoleClaimsTransformation` (cada request) — **`RoleResolutionService` retirado** | Ambas responsabilidades fusionadas en `RoleClaimsTransformation`, que ahora corre igual con el login real |
| Notificación de comprobante | `DevNotificacionService` (archivo .txt) | `GraphNotificacionService` vía Microsoft Graph `Mail.Send` |
| `RiesgoCalculator` | Sin cambios | Sin cambios — sigue siendo la misma clase de `08-reglas-negocio-calculo-riesgo.md`, usada solo para el recálculo en vivo del formulario; lo que queda persistido siempre lo calcula SQL Server (`fn_NivelRiesgo`) |

## Módulo "Gestión de administradores" — invitar por correo

Se agregó `AdministradorController.Invitar` (y el formulario correspondiente en la vista) para
poder asignar el rol Administrador a alguien que **todavía no inició sesión nunca** en la App —
antes solo se podía alternar el rol de gente que ya existía en `dbo.Usuario`. El registro se
precarga con `EsAdministrador = true` y sin `AzureAdObjectId` vinculado; `RoleClaimsTransformation`
completa el vínculo automáticamente en el primer login de esa persona, sin perder el rol.
