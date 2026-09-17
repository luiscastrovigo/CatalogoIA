# Herramientas de verificación

El código de esta App se escribe en un entorno **sin compilador**. Estos dos scripts son la
primera barrera de calidad: atrapan, sin compilar, las clases de error que ya ocurrieron al
menos una vez en el proyecto.

## Antes de cada `dotnet publish`

```powershell
python herramientas\validar_codigo.py src\CatalogoCitizenDevIA.Web
```

Devuelve código de salida 1 si encuentra algún ERROR. Si sale limpio, recién ahí publicar.

## Batería de regresión del propio validador

```powershell
python herramientas\probar_validador.py
```

Arma proyectos sintéticos, les inyecta cada error conocido y verifica que el validador lo
detecte. Correrla después de tocar `validar_codigo.py`. Estado esperado: 17 casos, 0 fallas.

## Al descubrir un error nuevo

No basta con corregirlo: hay que **agregarle un control** en `validar_codigo.py` y su caso
en `probar_validador.py`. Un error sin control se repite.

El catálogo completo de errores, causas raíz y controles está en el documento del proyecto
`claude/12-errores-y-controles.md`.
