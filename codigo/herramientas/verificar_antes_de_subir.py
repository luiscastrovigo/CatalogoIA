#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verificar_antes_de_subir.py
Catalogo de Soluciones Citizen Development (repositorio CatalogoIA)

Puerta de seguridad previa a `git add` / `git commit` / `git push`.

Revisa los archivos que Git LLEVARIA al repositorio (rastreados + nuevos no
ignorados) y falla si encuentra un secreto o un artefacto que no debe
versionarse.

Uso, desde la raiz del repositorio:

    python codigo/herramientas/verificar_antes_de_subir.py

Codigos de salida:
    0 = limpio, se puede subir
    1 = hay hallazgos CRITICO o ALTO, NO subir
    2 = error de ejecucion (no es un repositorio Git, etc.)

Este archivo NO contiene ningun secreto: solo patrones genericos.
Solo usa la biblioteca estandar de Python 3.8+.
"""

import os
import re
import subprocess
import sys

# ---------------------------------------------------------------------------
# Configuracion
# ---------------------------------------------------------------------------

EXTENSIONES_TEXTO = {
    ".cs", ".cshtml", ".json", ".config", ".xml", ".md", ".sql", ".py",
    ".ps1", ".js", ".css", ".csproj", ".sln", ".txt", ".yml", ".yaml",
    ".ejemplo", ".example", ".template",
}

# Si la linea contiene alguno de estos marcadores, se considera plantilla
# y no un secreto real.
RE_MARCADOR = re.compile(
    r"REEMPLAZAR|TU_[A-Z]|XXXX|\.\.\.|<[A-Za-z_ ]+>|EJEMPLO|PLACEHOLDER|"
    r"\{\{|\$\(|%[A-Z_]+%",
    re.IGNORECASE,
)

# --- patrones de contenido -------------------------------------------------

RE_PASSWORD = re.compile(r"""(?:Password|Pwd)\s*=\s*[^;"'\s]{3,}""", re.IGNORECASE)

# Cubre las tres formas en que aparece un valor asignado:
#   ClientSecret=xxx          (cadena / linea de comandos)
#   "ClientSecret": "xxx"     (JSON)
#   name="AzureAd__ClientSecret" value="xxx"   (web.config)
RE_CLIENT_SECRET = re.compile(
    r"""(?:ClientSecret|client_secret)["']?\s*(?:=|:|value\s*=)\s*["']?[^"'\s,;<>]{8,}""",
    re.IGNORECASE,
)

# Forma tipica de un secreto de Entra ID: segmento, tilde, segmento largo.
RE_SECRETO_ENTRA = re.compile(r"[A-Za-z0-9._-]{3,}~[A-Za-z0-9._~-]{20,}")

RE_GUID_ASIGNADO = re.compile(
    r"""(?:TenantId|ClientId)["']?\s*(?:=|:|value\s*=)\s*["']?"""
    r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
    re.IGNORECASE,
)

RE_DIR_PROHIBIDO = re.compile(r"(?:^|/)(?:bin|obj|publish|\.vs)/", re.IGNORECASE)

NOMBRES_PROHIBIDOS = {
    "web.config": ("CRITICO",
                   "web.config lleva el bloque <environmentVariables> con la clave "
                   "de SQL Server y el ClientSecret de Entra ID."),
    "appsettings.production.json": ("CRITICO",
                                    "Configuracion de produccion: suele contener secretos."),
    "secrets.json": ("CRITICO", "Archivo de secretos."),
}

EXT_CERTIFICADO = {".pfx", ".p12", ".pem", ".key", ".snk"}
EXT_COMPRIMIDO = {".zip", ".7z", ".rar"}

SEVERIDADES_BLOQUEANTES = {"CRITICO", "ALTO"}
ORDEN_SEVERIDAD = {"CRITICO": 0, "ALTO": 1, "MEDIO": 2}


# ---------------------------------------------------------------------------
# Motor
# ---------------------------------------------------------------------------

class Hallazgo:
    def __init__(self, severidad, ubicacion, detalle):
        self.severidad = severidad
        self.ubicacion = ubicacion
        self.detalle = detalle

    def __str__(self):
        return "[{0:<8}] {1}\n            {2}".format(
            self.severidad, self.ubicacion, self.detalle)


def listar_candidatos(raiz):
    """Archivos rastreados por Git mas los nuevos que .gitignore no excluye."""
    try:
        salida = subprocess.run(
            ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
            cwd=raiz, capture_output=True, text=True, check=True,
        )
    except FileNotFoundError:
        print("ERROR: no se encontro el ejecutable 'git' en el PATH.")
        sys.exit(2)
    except subprocess.CalledProcessError:
        print("ERROR: este directorio no es un repositorio Git.")
        print("       Ejecuta 'git init' primero, desde la raiz del proyecto.")
        sys.exit(2)

    return [linea for linea in salida.stdout.splitlines() if linea.strip()]


def revisar_nombres(rutas):
    hallazgos = []
    for ruta in rutas:
        nombre = os.path.basename(ruta).lower()
        extension = os.path.splitext(nombre)[1]
        ruta_barras = ruta.replace("\\", "/")

        if nombre in NOMBRES_PROHIBIDOS:
            severidad, detalle = NOMBRES_PROHIBIDOS[nombre]
            hallazgos.append(Hallazgo(severidad, ruta, detalle))

        if re.match(r"appsettings\..*local\.json$", nombre):
            hallazgos.append(Hallazgo(
                "CRITICO", ruta, "Configuracion local: suele contener secretos."))

        if extension in EXT_CERTIFICADO:
            hallazgos.append(Hallazgo(
                "CRITICO", ruta, "Certificado o llave privada."))

        if RE_DIR_PROHIBIDO.search(ruta_barras):
            hallazgos.append(Hallazgo(
                "ALTO", ruta,
                "Artefacto de compilacion o publicacion: no debe versionarse."))

        if extension in EXT_COMPRIMIDO:
            hallazgos.append(Hallazgo(
                "MEDIO", ruta,
                "Archivo comprimido: puede contener una copia del codigo con secretos."))
    return hallazgos


def revisar_contenido(raiz, rutas):
    hallazgos = []
    for ruta in rutas:
        extension = os.path.splitext(ruta)[1].lower()
        if extension not in EXTENSIONES_TEXTO:
            continue

        completa = os.path.join(raiz, ruta)
        if not os.path.isfile(completa):
            continue
        # El propio verificador contiene los patrones: no se audita a si mismo.
        if os.path.basename(ruta) == os.path.basename(__file__):
            continue

        try:
            with open(completa, "r", encoding="utf-8", errors="replace") as manejador:
                lineas = manejador.readlines()
        except OSError:
            continue

        for numero, linea in enumerate(lineas, start=1):
            ubicacion = "{0}:{1}".format(ruta, numero)
            es_plantilla = bool(RE_MARCADOR.search(linea))

            if RE_PASSWORD.search(linea) and not es_plantilla:
                hallazgos.append(Hallazgo(
                    "CRITICO", ubicacion,
                    "Cadena de conexion con contrasena literal."))

            if RE_CLIENT_SECRET.search(linea) and not es_plantilla:
                hallazgos.append(Hallazgo(
                    "CRITICO", ubicacion, "Posible ClientSecret literal."))

            if RE_SECRETO_ENTRA.search(linea):
                hallazgos.append(Hallazgo(
                    "CRITICO", ubicacion,
                    "Cadena con el formato de un secreto de Entra ID."))

            if RE_GUID_ASIGNADO.search(linea):
                hallazgos.append(Hallazgo(
                    "ALTO", ubicacion,
                    "TenantId o ClientId literal: debe venir de variable de entorno."))
    return hallazgos


def revisar_higiene(raiz):
    hallazgos = []
    if not os.path.isfile(os.path.join(raiz, ".gitignore")):
        hallazgos.append(Hallazgo(
            "ALTO", ".gitignore",
            "No existe .gitignore en la raiz del repositorio."))
    return hallazgos


def main():
    raiz = sys.argv[1] if len(sys.argv) > 1 else "."
    raiz = os.path.abspath(raiz)

    print("")
    print("=== Verificacion previa a subir a Git ===")
    print("Repositorio: {0}".format(raiz))
    print("")

    candidatos = listar_candidatos(raiz)
    if not candidatos:
        print("No hay archivos candidatos. Nada que verificar.")
        return 0

    print("Archivos candidatos a versionar: {0}".format(len(candidatos)))

    hallazgos = []
    hallazgos.extend(revisar_nombres(candidatos))
    hallazgos.extend(revisar_contenido(raiz, candidatos))
    hallazgos.extend(revisar_higiene(raiz))

    print("")
    if not hallazgos:
        print("OK: no se encontraron secretos ni artefactos prohibidos.")
        print("Puedes continuar con git add / commit / push.")
        print("")
        return 0

    hallazgos.sort(key=lambda h: (ORDEN_SEVERIDAD.get(h.severidad, 9), h.ubicacion))

    print("HALLAZGOS: {0}".format(len(hallazgos)))
    print("")
    for hallazgo in hallazgos:
        print(hallazgo)
    print("")

    bloqueantes = [h for h in hallazgos if h.severidad in SEVERIDADES_BLOQUEANTES]
    if bloqueantes:
        print("NO SUBAS el repositorio: hay {0} hallazgo(s) bloqueante(s).".format(
            len(bloqueantes)))
        print("Si un archivo ya quedo rastreado por error:")
        print("    git rm --cached <archivo>")
        print("")
        return 1

    print("Solo hay hallazgos de severidad MEDIO. Revisalos antes de continuar.")
    print("")
    return 0


if __name__ == "__main__":
    sys.exit(main())
