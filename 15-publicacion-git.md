---
title: Publicación en GitHub — repositorio CatalogoIA
proyecto: Catalogo App IA
version: 1.0
fecha: 2026-09-17
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
depende_de: 10-guia-despliegue-perms220.md, 11-auditoria-seguridad.md, 12-errores-y-controles.md
---

# Publicación en GitHub — repositorio `CatalogoIA`

Documenta cómo el proyecto pasa a control de versiones en el repositorio privado **`CatalogoIA`** de la cuenta **`luiscastrovigo`**, y qué mecanismo impide que un secreto de producción entre al repositorio.

La guía operativa paso a paso vive en el propio proyecto, en `PREPARAR-GIT.md`.

---

## 1. Validación previa: los 15 documentos del proyecto

Verificado el 2026-09-17: los documentos `00-README.md` a `14-reasignacion-de-dueno.md` existen, están numerados de forma consecutiva y sin vacíos. Se detectó que la copia en disco estaba desactualizada respecto a la versión vigente:

| Situación en disco antes de la publicación | Documentos |
|---|---|
| Desactualizados (versión del 2026-09-03) | `02`, `03`, `04`, `06`, `09`, `10` |
| Ausentes por completo | `11`, `12`, `13`, `14` |
| Vigentes | `00`, `01`, `05`, `07`, `08` |

Los 15 se reescribieron en disco con su versión vigente antes de inicializar el repositorio, de modo que lo que se sube coincide exactamente con la documentación del proyecto.

---

## 2. Hallazgo de seguridad que condiciona el diseño del repositorio

`codigo\web.config` — y su copia en `codigo\publish\web.config` — contienen, en texto plano, dentro del bloque `<environmentVariables>`:

- la cadena de conexión a `YanbalCitizenDevIA` en PERMS02, **con la contraseña del login de aplicación**;
- el `TenantId` y el `ClientId` de la App Registration;
- el **`ClientSecret`** de Entra ID;
- el buzón remitente de notificaciones.

Los mismos valores viajan dentro de `CatalogoCitizenDevIA-codigo-fuente.zip` y de `Claude outputs\CatalogoCitizenDevIA_src.zip`, que son copias comprimidas del árbol de código.

Subir cualquiera de esos archivos a GitHub sería una exposición de credenciales de producción, irreversible en la práctica: aunque se borre después, el valor queda en el historial del repositorio.

De ahí la regla del repositorio: **`web.config` se ignora en cualquier carpeta**, igual que `publish/`, `bin/`, `obj/`, `.vs/` y todos los comprimidos.

---

## 3. Artefactos incorporados al proyecto

| Archivo | Ubicación | Propósito |
|---|---|---|
| `.gitignore` | raíz | Excluye secretos, artefactos de compilación, comprimidos y ruido de Windows/OneDrive |
| `.gitattributes` | raíz | Normaliza fin de línea: LF en el repositorio, CRLF en el árbol de trabajo Windows |
| `README.md` | raíz | Portada del repositorio: stack, estructura, puesta en marcha, sección de seguridad |
| `PREPARAR-GIT.md` | raíz | Guía operativa de publicación |
| `web.config.ejemplo` | `codigo\` | Plantilla del `web.config` con marcadores `REEMPLAZAR_*` en vez de valores reales |
| `verificar_antes_de_subir.py` | `codigo\herramientas\` | Puerta de seguridad previa a cada `push` |

`appsettings.json` **sí** se versiona: por diseño no contiene secretos, y el control C-04 de `validar_codigo.py` lo verifica en cada iteración (ver `12-errores-y-controles.md`).

---

## 4. `verificar_antes_de_subir.py` — la puerta de seguridad

Toma la lista real de archivos que Git llevaría al repositorio (`git ls-files --cached --others --exclude-standard`), no el árbol de directorios, de modo que audita exactamente lo que se va a subir. Devuelve código de salida 1 ante cualquier hallazgo CRITICO o ALTO.

**Revisiones por nombre y ruta**

| Regla | Severidad |
|---|---|
| `web.config` en cualquier carpeta | CRITICO |
| `appsettings.Production.json`, `appsettings.*Local.json`, `secrets.json` | CRITICO |
| `.pfx`, `.p12`, `.pem`, `.key`, `.snk` | CRITICO |
| Rutas bajo `bin/`, `obj/`, `publish/`, `.vs/` | ALTO |
| `.zip`, `.7z`, `.rar` | MEDIO |

**Revisiones por contenido** (sobre archivos de texto, línea por línea)

| Regla | Severidad |
|---|---|
| `Password=` / `Pwd=` con un valor literal | CRITICO |
| `ClientSecret` con valor asignado | CRITICO |
| Cadena con la forma de un secreto de Entra ID (`prefijo~sufijo-largo`) | CRITICO |
| `TenantId` / `ClientId` con un GUID literal | ALTO |
| Ausencia de `.gitignore` en la raíz | ALTO |

Una línea que contenga un marcador de plantilla (`REEMPLAZAR`, `...`, `<ALGO>`, `EJEMPLO`, `PLACEHOLDER`, `%VAR%`, `{{ }}`) no se reporta: así la documentación de despliegue y `web.config.ejemplo` pasan limpios sin debilitar la regla.

El script cubre las tres formas en que aparece un valor asignado — `Clave=valor`, `"Clave": "valor"` y `name="X__Clave" value="valor"` (web.config) — y se excluye a sí mismo del análisis de contenido, porque contiene los patrones.

### Pruebas ejecutadas

Se construyó un repositorio sintético con la misma estructura del proyecto, incluyendo un `web.config` con secretos ficticios de formato realista, `publish/`, `bin/`, un `.zip` y el `appsettings.json` real:

| Escenario | Resultado esperado | Resultado obtenido |
|---|---|---|
| Con `.gitignore` en su sitio | 21 archivos candidatos, sin hallazgos, salida 0 | Correcto |
| Forzando `git add -f` de `web.config`, `publish/web.config`, el `.zip` y un `.dll` de `bin/` | Hallazgos CRITICO y ALTO, salida 1 | Correcto — 7 hallazgos |
| Sin `.gitignore` | Hallazgo adicional por `.gitignore` ausente | Correcto — 8 hallazgos |

La primera versión del script se escribió en PowerShell y **se descartó**: no había intérprete de PowerShell disponible para probarla, y entregar una puerta de seguridad sin ejecutarla contradice el principio de elevar el nivel de verificación antes de dar algo por terminado. Se reescribió en Python — el mismo intérprete que ya requiere `validar_codigo.py` — donde sí pudo probarse.

Al reprobar la versión Python se detectaron dos defectos reales que la versión PowerShell habría arrastrado sin que nadie lo notara: los patrones de `ClientSecret` y de `TenantId` no contemplaban la forma XML `name="..." value="..."`, y el patrón de secreto de Entra ID exigía un prefijo más largo del que esos secretos tienen realmente. Ambos corregidos y reprobados.

---

## 5. Observaciones abiertas

### 5.1 El repositorio queda dentro de OneDrive

La carpeta del proyecto está sincronizada con OneDrive, que sincronizará también la carpeta `.git`. Eso puede corromper el repositorio si la sincronización ocurre a mitad de una operación de Git. Si aparecen errores de objetos corruptos o `index.lock` bloqueado, la salida es clonar fuera de OneDrive (`D:\Repos\CatalogoIA`) y trabajar desde ahí, dejando la copia de OneDrive como respaldo.

### 5.2 Rotación de credenciales — recomendada, no bloqueante

La contraseña del login de aplicación y el `ClientSecret` existen hoy en texto plano en `codigo\web.config` y dentro de los `.zip` del código fuente, ambos en una carpeta sincronizada a la nube corporativa. No constituye una fuga — OneDrive corporativo es un almacén autorizado — pero amplía sin necesidad la superficie donde esos valores existen.

Recomendación: rotar ambos secretos en la próxima ventana de mantenimiento y mantenerlos a partir de entonces únicamente en las variables de entorno del App Pool, conforme a `10-guia-despliegue-perms220.md`, sección 4.

---

## 6. Rutina a partir de ahora

```powershell
cd "D:\Users\lecastro\OneDrive - UNIQUEYANBAL\LCV\Proyecto\2026\23 - Catalogo de Apps IA"

python codigo\herramientas\validar_codigo.py codigo\src        # validación estática del código
python codigo\herramientas\verificar_antes_de_subir.py         # puerta de seguridad del repositorio

git add .
git commit -m "Descripcion del cambio"
git push
```

Ambos scripts devuelven código de salida 1 ante un problema, de modo que pueden encadenarse si más adelante se automatiza la secuencia.
