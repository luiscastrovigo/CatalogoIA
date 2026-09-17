---
title: Publicación del proyecto en GitHub (repositorio CatalogoIA)
proyecto: Catalogo App IA
version: 1.0
fecha: 2026-09-17
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
---

# Publicación del proyecto en GitHub — repositorio `CatalogoIA`

Guía de un solo uso para subir por primera vez el proyecto al repositorio privado **`CatalogoIA`** de la cuenta **`luiscastrovigo`**, con la misma configuración de conexión que ya se usa en el repositorio `YanbalRepo`.

> **Antes de ejecutar nada, lee la sección 1.** Hay un archivo en el proyecto que contiene la contraseña de SQL Server y el `ClientSecret` de Entra ID en texto plano.

---

## 1. Lo que NO debe subir — y por qué

| Archivo / carpeta | Contenido | Tratamiento |
|---|---|---|
| `codigo\web.config` | `<environmentVariables>` con la **cadena de conexión con contraseña** a PERMS02 y el **`ClientSecret`** real de Entra ID | Ignorado por `.gitignore` |
| `codigo\publish\web.config` | La misma copia, generada al publicar | Ignorado (`publish/` completo) |
| `codigo\publish\`, `bin\`, `obj\`, `.vs\` | Artefactos de compilación | Ignorados |
| `*.zip` (`CatalogoCitizenDevIA-codigo-fuente.zip`, `Claude outputs\CatalogoCitizenDevIA_src.zip`) | Copias comprimidas del código, que **incluyen el `web.config` con secretos** | Ignorados |
| `Claude outputs\` | Carpeta de salidas de trabajo (informes, comprimidos) | Ignorada |

En su lugar se versiona **`codigo\web.config.ejemplo`**: la misma plantilla, con marcadores `REEMPLAZAR_*` en vez de valores reales.

`appsettings.json` **sí** se versiona: está diseñado para no contener secretos nunca, y el control C-04 de `validar_codigo.py` lo verifica en cada iteración.

---

## 2. Archivos preparados

Quedaron escritos en el proyecto:

| Archivo | Para qué sirve |
|---|---|
| `.gitignore` | Excluye secretos, artefactos de compilación, comprimidos y ruido de Windows/OneDrive |
| `.gitattributes` | Normaliza fin de línea (LF en el repositorio, CRLF en Windows) |
| `README.md` | Portada del repositorio: stack, estructura, puesta en marcha, seguridad |
| `codigo\web.config.ejemplo` | Plantilla del `web.config` sin valores reales |
| `codigo\herramientas\verificar_antes_de_subir.py` | Puerta de seguridad previa a cada `push` |
| `00-README.md` … `14-reasignacion-de-dueno.md` | Los 15 documentos del proyecto, en su versión vigente |

---

## 3. Secuencia de comandos

Abrir **PowerShell** y ejecutar en orden. La ruta del proyecto tiene espacios, por eso va entre comillas.

### 3.1 Situarse en la raíz del proyecto

```powershell
cd "D:\Users\lecastro\OneDrive - UNIQUEYANBAL\LCV\Proyecto\2026\23 - Catalogo de Apps IA"
```

### 3.2 Inicializar el repositorio

```powershell
git init
git branch -M main
```

### 3.3 Verificar QUÉ se va a subir — antes de agregar nada

```powershell
python codigo\herramientas\verificar_antes_de_subir.py
```

Debe terminar con:

```
OK: no se encontraron secretos ni artefactos prohibidos.
```

Si aparece cualquier hallazgo **CRITICO** o **ALTO**, detente y resuélvelo. El script devuelve código de salida 1 en ese caso.

Como segunda comprobación, revisa a ojo la lista de archivos candidatos:

```powershell
git add --dry-run . | Select-String -NotMatch "\.md$"
```

No debe aparecer ningún `web.config` (salvo `web.config.ejemplo`), ninguna carpeta `bin`, `obj` o `publish`, ni ningún `.zip`.

### 3.4 Primer commit

```powershell
git add .
git status
git commit -m "Catalogo de Soluciones Citizen Development: codigo, documentacion y scripts SQL

Aplicacion ASP.NET Core MVC (.NET 8) con Entra ID, EF Core y SQL Server.
Incluye los 15 documentos del proyecto, los scripts de base de datos y las
herramientas de validacion estatica.

Los secretos (cadena de conexion, TenantId, ClientId, ClientSecret y buzon
remitente) quedan fuera del repositorio: viven solo en variables de entorno
del App Pool de IIS en PERMS220. La plantilla versionada es
codigo/web.config.ejemplo."
```

### 3.5 Conectar con GitHub y subir

```powershell
git remote add origin https://github.com/luiscastrovigo/CatalogoIA.git
git remote -v
git push -u origin main
```

Se usa la misma conexión HTTPS de `luiscastrovigo` que ya está guardada en el Administrador de credenciales de Windows por el repositorio `YanbalRepo`, así que no debería pedir usuario ni token. Si los pide, es el mismo Personal Access Token.

### 3.6 Confirmar en GitHub

Abrir `https://github.com/luiscastrovigo/CatalogoIA` y verificar:

- El repositorio está marcado como **Private**.
- No existe ningún archivo `web.config` (solo `web.config.ejemplo`).
- No existen carpetas `bin`, `obj` ni `publish`.
- El `README.md` se ve en la portada.

---

## 4. Uso diario, después del primer push

```powershell
cd "D:\Users\lecastro\OneDrive - UNIQUEYANBAL\LCV\Proyecto\2026\23 - Catalogo de Apps IA"

python codigo\herramientas\validar_codigo.py codigo\src          # validación estática del código
python codigo\herramientas\verificar_antes_de_subir.py           # puerta de seguridad

git add .
git commit -m "Descripcion del cambio"
git push
```

Los dos scripts devuelven código de salida 1 cuando encuentran un problema, así que pueden encadenarse en un `.cmd` si más adelante se quiere automatizar.

---

## 5. Dos observaciones

### 5.1 El repositorio queda dentro de OneDrive

La carpeta del proyecto está sincronizada con OneDrive. OneDrive sincroniza también la carpeta `.git`, y eso puede corromper el repositorio cuando sincroniza a mitad de una operación de Git.

Si en algún momento aparecen errores raros de Git (objetos corruptos, `index.lock` bloqueado), la solución es sacar el repositorio de OneDrive:

```powershell
# clonar fuera de OneDrive, ya con el historial subido
git clone https://github.com/luiscastrovigo/CatalogoIA.git D:\Repos\CatalogoIA
```

y trabajar desde `D:\Repos\CatalogoIA`, dejando la copia de OneDrive solo como respaldo.

### 5.2 Rotación de credenciales

La contraseña de `usr_app_catalogoia` y el `ClientSecret` de la App Registration están hoy en texto plano en `codigo\web.config` y dentro de los `.zip` del código fuente, ambos dentro de una carpeta sincronizada a OneDrive. No es una fuga —OneDrive corporativo es un almacén autorizado—, pero sí amplía innecesariamente dónde existen esos valores.

Recomendación, independiente de este cambio: rotar ambos secretos cuando haya una ventana de mantenimiento, y a partir de ahí mantenerlos únicamente en las variables de entorno del App Pool, tal como describe `10-guia-despliegue-perms220.md`, sección 4.
