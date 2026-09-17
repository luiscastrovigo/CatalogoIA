---
title: Identidad visual — Catálogo Citizen Development IA
proyecto: Catalogo App IA
version: "1.1"
fecha: 2026-09-07
autor: Luis Castro (Arquitectura de Aplicaciones) — con Claude
depende_de: 03-alcance-funcional.md
canvas_de_referencia: https://claude.ai/code/artifact/c50301f0-3c65-48c1-a0ec-e9a0e464be5a
---

# Identidad visual aprobada

De las 4 direcciones exploradas (Comité Ejecutivo, Studio Claro, Centro de Control, Editorial Institucional), se aprobó **Studio Claro** como base de alcance y distribución de pantallas, con un pase de refinamiento profesional sobre su ejecución visual. Este documento fija los tokens de diseño para que la implementación en ASP.NET Core sea consistente en todas las pantallas, sin dejarlo a interpretación de cada desarrollador.

## Layout y navegación

- Shell de aplicación con **barra lateral fija** (220px) a la izquierda: logo, navegación (Catálogo, Registrar solución, Dashboard ejecutivo — este último marcado "ADMIN" y solo visible/habilitado para administradores) y bloque de usuario (avatar + nombre + rol) anclado abajo.
- Contenido principal con cabecera de página (título + descripción + acción primaria a la derecha), franja de KPIs, fila de filtros y tabla o tarjetas de contenido.

## Paleta de color

| Token | Uso | Hex |
|---|---|---|
| `color-ink` | Texto principal, logo, botones oscuros | `#171512` |
| `color-text-secondary` | Texto secundario | `#6B6558` |
| `color-text-muted` | Texto terciario / metadatos | `#9A9382` |
| `color-page-bg` | Fondo de página | `#FAFAF8` |
| `color-surface` | Tarjetas, tabla, sidebar | `#FFFFFF` |
| `color-border` | Bordes hairline | `rgba(23,21,18,0.07–0.10)` |
| `color-primary` (naranja Yanbal) | Logo, acción primaria, acentos de marca | `#B5541F` |
| `color-gold` (dorado Yanbal) | Acentos secundarios, resaltes puntuales | `#B8912B` / `#D9A73B` |
| Estado "bueno" (Nivel 1 / Aprobado) | Tag verde | fondo `#E9F3E7` · texto `#2E7D32` |
| Estado "medio" (Nivel 2 / En revisión) | Tag ámbar | fondo `#FCF1DA` · texto `#96690F` |
| Estado "alto" (Nivel 3 / Rechazado) | Tag rojo | fondo `#FBEAEA` · texto `#A03434` |
| Estado neutro (Registrado) | Tag gris | fondo `#F2EEE3` · texto `#6B6558` |
| Estado "Reclasificado" | Tag violeta | texto `#6B5B95` |

El naranja y el dorado son los dos colores de marca de Yanbal usados aquí (además del negro/tinta como color de texto e íconos oscuros); el logo de la barra lateral y del login usa el naranja de marca (`#B5541F`) sobre un badge redondeado, con el glifo en blanco para contraste.

## Tipografía

- **Geist** (Google Fonts) para toda la interfaz — títulos, cuerpo, botones, navegación. Pesos 400/500/600/700.
- **Geist Mono** para datos tabulares y técnicos: IDs de solución (`CD-2026-001`), cifras de KPIs y tablas, fechas relativas en el dashboard. Evita que un ID o una cifra "baile" visualmente al alinearse con texto proporcional.
- Tamaños de referencia: título de página 20–21px/600, subtítulo 12.5px/400 (muted), texto de tabla 13px/400, etiquetas de campo 11px/500 mayúscula-sutil, KPI destacado 21px Geist Mono/600.

## Elevación y bordes

En vez de bordes planos únicamente, las tarjetas usan una sombra de dos capas muy sutil (inspirada en la escala de elevación de Fluent 2 de Microsoft, adaptada): borde hairline (`rgba(23,21,18,0.07)`) + `box-shadow: 0 1px 2px rgba(23,21,18,0.03)` en reposo, y una sombra algo más marcada (`0 2px 8px rgba(23,21,18,0.05)`) para paneles destacados (p. ej. el resumen del comprobante de registro). Radio de esquina: 8px en botones/inputs, 10–14px en tarjetas grandes, 5–6px en tags de estado (deliberadamente *no* píldora completa, para una lectura más sobria).

## Componentes clave

- **Tags de estado/riesgo**: rectángulo de radio pequeño (5–6px), fondo tenue + texto del mismo tono en versión oscura — nunca color puro de fondo con texto blanco.
- **Avatares**: círculo de 22–24px con iniciales, color de fondo variable por persona (paleta tenue, no aleatoria arcoíris) — usado en la columna "Dueño" del catálogo y en la cabecera de usuario.
- **KPIs**: número grande en Geist Mono + etiqueta descriptiva debajo, en tarjeta blanca con sombra sutil.
- **Gráficas del dashboard**: barras horizontales finas con extremos redondeados, franja segmentada para la distribución de nivel de riesgo, línea de evolución mensual con relleno de baja opacidad — sin ejes ni grillas pesadas, siguiendo principios de minimalismo de datos (ver `references/palette.md` del skill `dataviz` usado en su construcción).

## Pantallas de referencia

El canvas publicado contiene, en la página **"Diseño final"**: Login, Catálogo (pantalla de entrada / hero), Registro con comprobante, y Dashboard ejecutivo. La página **"Exploración inicial (A/B/C/D)"** conserva las 4 direcciones originalmente presentadas, incluida la versión sin refinar de Studio Claro, como historial de la decisión.

## Logo real de Yanbal — favicon (2026-09-07)

Luis Castro entregó el isotipo oficial de Yanbal (una "Y" blanca sobre fondo naranja de marca,
consistente con `color-primary` `#B5541F` de esta guía). Se usó para generar el ícono de la
pestaña del navegador (favicon), reemplazando el ícono genérico por defecto de la plantilla de
ASP.NET Core:

- `wwwroot/favicon.ico` (multi-resolución 16/32/48px).
- `wwwroot/images/favicon-16.png`, `favicon-32.png`, `apple-touch-icon-180.png`.
- `wwwroot/images/yanbal-logo-512.png` — copia a mayor resolución del mismo isotipo, disponible
  para reusar en el badge de la barra lateral/login (ver punto pendiente debajo) u otras piezas
  (ej. `og:image` si en el futuro se comparte un link de la App).
- `Views/Shared/_Layout.cshtml`: se agregaron los `<link rel="icon">`/`<link rel="apple-touch-icon">`
  explícitos en el `<head>`, en vez de depender de la convención implícita del navegador de pedir
  `/favicon.ico` en la raíz — esa convención **no funciona** para esta App porque se despliega
  bajo un subdirectorio (`/CatalogoIA/`, ver `10-guia-despliegue-perms220.md` sección 2): el
  navegador pediría `/favicon.ico` en la raíz del dominio de PERMS220, no dentro de `/CatalogoIA/`.

## Qué queda pendiente de definir con Comunicaciones/Marca de Yanbal

- **Badge/glifo de la barra lateral y del login**: sigue siendo el glifo abstracto (diamante/rombo)
  usado como marcador de posición desde el diseño inicial — a diferencia del favicon (arriba),
  todavía no se reemplazó por el isotipo oficial. El asset ya está disponible en
  `wwwroot/images/yanbal-logo-512.png` para hacerlo cuando se confirme que es el reemplazo
  deseado (es un cambio visual de una sola pieza, pendiente de decisión explícita del usuario
  antes de aplicarlo).
- Confirmación de si el naranja `#B5541F` y el dorado `#B8912B` corresponden exactamente a los
  valores Pantone/HEX oficiales de marca, o si Yanbal tiene una guía de marca digital con los
  valores exactos a usar.
