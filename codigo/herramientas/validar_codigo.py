#!/usr/bin/env python3
"""
Validador estatico para la App Catalogo Citizen Development IA (y cualquier App .NET
de Yanbal con el mismo stack: ASP.NET Core MVC + Razor + Entra ID + EF Core).

POR QUE EXISTE
--------------
El codigo de esta App se escribe en un entorno que NO tiene compilador ni salida a
NuGet, asi que "compila o no compila" solo se sabe cuando Luis Castro corre
dotnet publish en su maquina. Cada ida y vuelta por un error de sintaxis cuesta un
ciclo completo. Este script atrapa, sin compilador, las clases de error que YA se
produjeron al menos una vez en este proyecto (ver herramientas/README-validacion.md
y claude/12-errores-y-controles.md).

USO
---
    python3 herramientas/validar_codigo.py [ruta-del-proyecto-web]

Sin argumento asume src/CatalogoCitizenDevIA.Web relativo al directorio actual.
Codigo de salida: 0 si no hay ERRORes, 1 si hay al menos uno.
"""

import os
import re
import shutil
import subprocess
import sys
import json
import xml.dom.minidom


def despojar_comentarios_js(txt):
    txt = re.sub(r"/\*.*?\*/", "", txt, flags=re.S)
    return re.sub(r"//[^\n]*", "", txt)

# --------------------------------------------------------------------------
# Infraestructura de reporte
# --------------------------------------------------------------------------
ERRORES = []
AVISOS = []
INFO = []


def error(archivo, linea, mensaje, control):
    ERRORES.append((archivo, linea, mensaje, control))


def aviso(archivo, linea, mensaje, control):
    AVISOS.append((archivo, linea, mensaje, control))


def leer(ruta):
    with open(ruta, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def archivos(raiz, extensiones):
    for base, dirs, nombres in os.walk(raiz):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj", "lib", "node_modules", ".git")]
        for n in sorted(nombres):
            if n.lower().endswith(extensiones):
                yield os.path.join(base, n)


def numero_de_linea(texto, indice):
    return texto.count("\n", 0, indice) + 1


def rel(ruta, raiz):
    try:
        return os.path.relpath(ruta, raiz)
    except ValueError:
        return ruta


# --------------------------------------------------------------------------
# 1. XML: .csproj y .config
# --------------------------------------------------------------------------
def validar_xml(raiz):
    """C-01 comentarios XML con '--'   C-02 XML mal formado   C-03 versiones flotantes."""
    for ruta in archivos(raiz, (".csproj", ".config")):
        txt = leer(ruta)
        nombre = rel(ruta, raiz)

        for m in re.finditer(r"<!--(.*?)-->", txt, re.S):
            cuerpo = m.group(1)
            ln = numero_de_linea(txt, m.start())
            if "--" in cuerpo:
                error(nombre, ln,
                      "Comentario XML con '--' adentro. XML lo prohibe (MSB4025 al compilar). "
                      "Reescribir el texto sin dos guiones seguidos.", "C-01")
            if cuerpo.endswith("-"):
                error(nombre, ln,
                      "Comentario XML que termina en '-'. XML lo prohibe.", "C-01")

        try:
            xml.dom.minidom.parseString(txt)
        except Exception as ex:
            error(nombre, 0, "XML mal formado: %s" % ex, "C-02")

        if ruta.lower().endswith(".csproj"):
            for m in re.finditer(r'PackageReference\s+Include="([^"]+)"\s+Version="([^"]+)"', txt):
                paquete, version = m.group(1), m.group(2)
                if "*" in version:
                    error(nombre, numero_de_linea(txt, m.start()),
                          "PackageReference '%s' con version flotante '%s'. Fijar la version exacta "
                          "(hallazgo H-09 de la auditoria de seguridad)." % (paquete, version), "C-03")


# --------------------------------------------------------------------------
# 2. Secretos fuera del codigo
# --------------------------------------------------------------------------
CLAVES_SECRETAS = ("clientsecret", "password", "pwd", "secret", "apikey", "accountkey")


def _recorrer_json(nodo, ruta_actual, reportar):
    if isinstance(nodo, dict):
        for clave, valor in nodo.items():
            _recorrer_json(valor, ruta_actual + [str(clave)], reportar)
    elif isinstance(nodo, list):
        for i, valor in enumerate(nodo):
            _recorrer_json(valor, ruta_actual + [str(i)], reportar)
    else:
        reportar(ruta_actual, nodo)


def validar_secretos(raiz):
    """C-04: appsettings*.json nunca debe traer un secreto con valor.

    Se evalua por RUTA de clave, no por regex suelta: '"Default": "Information"' dentro de
    Logging:LogLevel es un nivel de log, no una cadena de conexion (falso positivo real de
    la primera version de este validador).
    """
    for ruta in archivos(raiz, (".json",)):
        if not os.path.basename(ruta).lower().startswith("appsettings"):
            continue
        nombre = rel(ruta, raiz)
        try:
            datos = json.loads(leer(ruta))
        except Exception as ex:
            error(nombre, 0, "JSON mal formado: %s" % ex, "C-04")
            continue

        def reportar(ruta_clave, valor, nombre=nombre):
            if not isinstance(valor, str) or not valor.strip():
                return
            if ruta_clave and ruta_clave[0].strip().startswith("//"):
                return  # clave-comentario documental, no configuracion real
            ultima = ruta_clave[-1].lower() if ruta_clave else ""
            en_conexiones = any(p.lower() == "connectionstrings" for p in ruta_clave[:-1])
            if ultima in CLAVES_SECRETAS or en_conexiones:
                error(nombre, 0,
                      "'%s' trae un valor en %s. Los secretos y cadenas de conexion van SIEMPRE "
                      "por variable de entorno." % (":".join(ruta_clave), os.path.basename(ruta)),
                      "C-04")

        _recorrer_json(datos, [], reportar)


# --------------------------------------------------------------------------
# 3. Content-Security-Policy declarada en Program.cs
# --------------------------------------------------------------------------
def leer_csp(raiz):
    program = os.path.join(raiz, "Program.cs")
    if not os.path.exists(program):
        return None
    txt = leer(program)
    # Se busca la ASIGNACION real, no la primera mencion del texto: el comentario que
    # explica la cabecera tambien contiene la palabra "script-src" y hacia que la
    # primera version de este validador leyera la CSP equivocada.
    m = re.search(r'\[\s*"Content-Security-Policy"\s*\]\s*=', txt)
    if not m:
        return None
    resto = txt[m.end():]
    fin = resto.find(";")
    asignacion = resto[:fin if fin > 0 else 2000]
    # concatena todos los literales de cadena de la asignacion
    politica = "".join(re.findall(r'"((?:[^"\\]|\\.)*)"', asignacion))
    directivas = {}
    for parte in politica.split(";"):
        parte = parte.strip()
        if not parte:
            continue
        nombre_dir, _, valor = parte.partition(" ")
        directivas[nombre_dir.strip().lower()] = valor.strip()
    script_src = directivas.get("script-src", "")
    return {"permite_inline": "unsafe-inline" in script_src,
            "politica": politica,
            "directivas": directivas}


# --------------------------------------------------------------------------
# 4. Vistas Razor
# --------------------------------------------------------------------------
RE_SCRIPT = re.compile(r"<script\b([^>]*)>(.*?)</script>", re.S | re.I)
RE_MANEJADOR_INLINE = re.compile(r"\son(click|change|submit|load|input|keyup|keydown|focus|blur|mouseover|error)\s*=", re.I)
RE_COMENTARIO_HTML = re.compile(r"<!--(.*?)-->", re.S)


def arrobas_peligrosas(fragmento):
    """Devuelve los offsets de '@' que estan dentro de un comentario de JavaScript.

    Razor interpreta '@' como transicion a codigo TAMBIEN dentro de un comentario
    de JavaScript: el compilador de vistas no entiende sintaxis JS. Escribir
    '// ejemplo: @s.Nombre' en un <script> produce un error de compilacion real
    (CS0103) porque 's' no existe en ese contexto. Para texto literal se escapa '@@'.
    """
    encontrados = []
    i = 0
    n = len(fragmento)
    while i < n:
        c = fragmento[i]
        if c == "/" and i + 1 < n and fragmento[i + 1] == "/":
            fin = fragmento.find("\n", i)
            fin = n if fin == -1 else fin
            for m in re.finditer(r"@", fragmento[i:fin]):
                pos = i + m.start()
                if not (fragmento[pos:pos + 2] == "@@" or (pos > 0 and fragmento[pos - 1] == "@")):
                    encontrados.append(pos)
            i = fin
        elif c == "/" and i + 1 < n and fragmento[i + 1] == "*":
            fin = fragmento.find("*/", i)
            fin = n if fin == -1 else fin + 2
            for m in re.finditer(r"@", fragmento[i:fin]):
                pos = i + m.start()
                if not (fragmento[pos:pos + 2] == "@@" or (pos > 0 and fragmento[pos - 1] == "@")):
                    encontrados.append(pos)
            i = fin
        elif c in "'\"`":
            comilla = c
            i += 1
            while i < n and fragmento[i] != comilla:
                if fragmento[i] == "\\":
                    i += 1
                i += 1
            i += 1
        else:
            i += 1
    return encontrados


def validar_vistas(raiz, csp):
    """C-05 '@' en comentario JS/HTML   C-06 script inline vs CSP   C-07 manejador inline
       C-08 secciones desbalanceadas   C-09 archivo estatico inexistente."""
    wwwroot = os.path.join(raiz, "wwwroot")
    permite_inline = csp["permite_inline"] if csp else True

    for ruta in archivos(raiz, (".cshtml",)):
        txt = leer(ruta)
        nombre = rel(ruta, raiz)

        # --- comentarios HTML: Razor tambien evalua '@' adentro ---
        for m in RE_COMENTARIO_HTML.finditer(txt):
            for mm in re.finditer(r"@(?!@)", m.group(1)):
                error(nombre, numero_de_linea(txt, m.start() + mm.start()),
                      "'@' dentro de un comentario HTML de una vista Razor. Razor lo evalua como "
                      "codigo igual. Escapar como '@@' o quitarlo.", "C-05")

        for m in RE_SCRIPT.finditer(txt):
            atributos, cuerpo = m.group(1), m.group(2)
            inicio_cuerpo = m.start(2)
            tiene_src = "src=" in atributos.lower()

            if not tiene_src and cuerpo.strip():
                if not permite_inline:
                    error(nombre, numero_de_linea(txt, m.start()),
                          "Bloque <script> inline, pero la CSP declara script-src sin 'unsafe-inline': "
                          "el navegador NO lo va a ejecutar. Mover el codigo a wwwroot/js/.", "C-06")
                for pos in arrobas_peligrosas(cuerpo):
                    error(nombre, numero_de_linea(txt, inicio_cuerpo + pos),
                          "'@' dentro de un comentario de JavaScript en un <script> de Razor. "
                          "Razor lo compila como codigo (error CS0103 tipico). Escapar como '@@'.", "C-05")

            if tiene_src:
                ms = re.search(r'src\s*=\s*"~?/([^"]+)"', atributos)
                if ms:
                    destino = os.path.join(wwwroot, ms.group(1).split("?")[0])
                    if not os.path.exists(destino):
                        error(nombre, numero_de_linea(txt, m.start()),
                              "La vista referencia '%s' pero ese archivo no existe en wwwroot."
                              % ms.group(1), "C-09")

        # --- manejadores de evento inline ---
        for m in RE_MANEJADOR_INLINE.finditer(txt):
            ln = numero_de_linea(txt, m.start())
            dentro_de_comentario = any(
                c.start() < m.start() < c.end() for c in RE_COMENTARIO_HTML.finditer(txt))
            dentro_de_script = any(
                s.start() < m.start() < s.end() for s in RE_SCRIPT.finditer(txt))
            if dentro_de_comentario or dentro_de_script:
                continue
            if not permite_inline:
                error(nombre, ln,
                      "Manejador de evento inline (on%s=). La CSP sin 'unsafe-inline' TAMBIEN bloquea "
                      "los manejadores inline, no solo los bloques <script>: esto se rompe en silencio. "
                      "Usar una clase y addEventListener desde wwwroot/js/." % m.group(1), "C-07")
            else:
                aviso(nombre, ln,
                      "Manejador de evento inline (on%s=). Impide endurecer la CSP." % m.group(1), "C-07")

        # --- C-15: un formulario con <input type="file"> necesita enctype ---
        # Sin enctype="multipart/form-data" el navegador envia solo el NOMBRE del
        # archivo, no su contenido, y el servidor recibe null. No hay error: los
        # archivos simplemente no llegan. Es de las fallas mas dificiles de
        # diagnosticar justamente por lo silenciosa que es.
        for m in re.finditer(r"<form\b([^>]*)>(.*?)</form>", txt, re.S | re.I):
            atributos, cuerpo = m.group(1), m.group(2)
            if re.search(r'type\s*=\s*"file"', cuerpo, re.I) and "multipart/form-data" not in atributos:
                error(nombre, numero_de_linea(txt, m.start()),
                      'Formulario con <input type="file"> pero sin enctype="multipart/form-data". '
                      "El archivo no llegara al servidor y no habra ningun mensaje de error.", "C-15")

        if "javascript:" in txt:
            error(nombre, numero_de_linea(txt, txt.index("javascript:")),
                  "URL 'javascript:' en una vista. Bloqueada por CSP y mala practica.", "C-07")

        # --- C-08: llaves balanceadas en TODA la vista ---
        # La primera version contaba solo desde @section hacia el final, y por eso NO
        # detecto un bloque @if (...) { ... } sin cerrar ubicado ANTES de la seccion
        # de scripts (error E-06). Ahora se cuenta el archivo completo, sobre el texto
        # sin comentarios HTML, sin comentarios Razor y sin valores entre comillas
        # dobles, que es donde viven las llaves que no son codigo.
        cuerpo = RE_COMENTARIO_HTML.sub("", txt)
        cuerpo = re.sub(r"@\*.*?\*@", "", cuerpo, flags=re.S)
        cuerpo = re.sub(r'"(?:[^"\\\n]|\\.)*"', '""', cuerpo)
        if cuerpo.count("{") != cuerpo.count("}"):
            error(nombre, 0,
                  "Llaves desbalanceadas en la vista: %d '{' contra %d '}'. Suele ser un "
                  "bloque @if, @foreach o @section sin su llave de cierre."
                  % (cuerpo.count("{"), cuerpo.count("}")), "C-08")


# --------------------------------------------------------------------------
# 5. C#
# --------------------------------------------------------------------------
def despojar_csharp(txt):
    """Quita comentarios, cadenas y caracteres literales para poder contar llaves."""
    salida = []
    i, n = 0, len(txt)
    while i < n:
        c = txt[i]
        dos = txt[i:i + 2]
        if dos == "//":
            fin = txt.find("\n", i)
            i = n if fin == -1 else fin
        elif dos == "/*":
            fin = txt.find("*/", i)
            i = n if fin == -1 else fin + 2
        elif dos in ('@"', '$@', '@$') or (c == "$" and txt[i:i + 2] == '$"'):
            # cadena verbatim y/o interpolada: se salta hasta la comilla de cierre
            j = txt.find('"', i)
            if j == -1:
                break
            verbatim = "@" in txt[i:j + 1]
            j += 1
            while j < n:
                if txt[j] == '"':
                    if verbatim and j + 1 < n and txt[j + 1] == '"':
                        j += 2
                        continue
                    if not verbatim and txt[j - 1] == "\\":
                        j += 1
                        continue
                    break
                j += 1
            i = j + 1
        elif c == '"':
            j = i + 1
            while j < n and not (txt[j] == '"' and txt[j - 1] != "\\"):
                j += 1
            i = j + 1
        elif c == "'":
            j = i + 1
            while j < n and not (txt[j] == "'" and txt[j - 1] != "\\"):
                j += 1
            i = j + 1
        else:
            salida.append(c)
            i += 1
    return "".join(salida)


USINGS_REQUERIDOS = [
    (r"\[EnableRateLimiting\(", "Microsoft.AspNetCore.RateLimiting"),
    (r"\[Authorize\b", "Microsoft.AspNetCore.Authorization"),
    (r"\bX509Certificate2\b", "System.Security.Cryptography.X509Certificates"),
    (r"\bRateLimitPartition\b", "System.Threading.RateLimiting"),
    (r"\bSqlParameter\b", "Microsoft.Data.SqlClient"),
    (r"\bIClaimsTransformation\b", "Microsoft.AspNetCore.Authentication"),
]


def validar_csharp(raiz):
    """C-10 llaves/parentesis desbalanceados   C-11 using faltante para un tipo usado."""
    for ruta in archivos(raiz, (".cs",)):
        txt = leer(ruta)
        nombre = rel(ruta, raiz)
        limpio = despojar_csharp(txt)

        for abre, cierra, etiqueta in (("{", "}", "llaves"), ("(", ")", "parentesis")):
            if limpio.count(abre) != limpio.count(cierra):
                error(nombre, 0,
                      "%s desbalanceados: %d '%s' contra %d '%s'."
                      % (etiqueta.capitalize(), limpio.count(abre), abre, limpio.count(cierra), cierra),
                      "C-10")

        for patron, espacio in USINGS_REQUERIDOS:
            if re.search(patron, limpio) and ("using %s;" % espacio) not in txt:
                error(nombre, 0,
                      "El archivo usa un tipo de '%s' pero no declara ese using." % espacio, "C-11")


def _bloque_llaves(txt, desde):
    """Devuelve el contenido balanceado entre la primera llave tras 'desde' y su cierre."""
    inicio = txt.find("{", desde)
    if inicio == -1:
        return ""
    nivel = 0
    for i in range(inicio, len(txt)):
        if txt[i] == "{":
            nivel += 1
        elif txt[i] == "}":
            nivel -= 1
            if nivel == 0:
                return txt[inicio + 1:i]
    return ""


def validar_implementaciones(raiz):
    """C-14: toda clase que declara una interfaz debe implementar todos sus metodos.

    Agregar un metodo a una interfaz rompe la compilacion de TODAS sus
    implementaciones, y en este proyecto hay dos por cada repositorio (EF Core y
    en memoria). Es un error facil de cometer y que el compilador solo revela al
    final del ciclo.
    """
    interfaces = {}
    for ruta in archivos(raiz, (".cs",)):
        limpio = despojar_csharp(leer(ruta))
        for m in re.finditer(r"\binterface\s+(I\w+)", limpio):
            cuerpo = _bloque_llaves(limpio, m.end())
            metodos = set(re.findall(r"\b(\w+)\s*\([^;{}]*\)\s*;", cuerpo))
            if metodos:
                interfaces[m.group(1)] = metodos

    if not interfaces:
        return

    for ruta in archivos(raiz, (".cs",)):
        limpio = despojar_csharp(leer(ruta))
        nombre = rel(ruta, raiz)
        for m in re.finditer(r"\bclass\s+(\w+)\s*:\s*([^{]+)\{", limpio):
            clase, bases = m.group(1), m.group(2)
            cuerpo = _bloque_llaves(limpio, m.end() - 1)
            for base in re.split(r"[,\s]+", bases.strip()):
                base = base.split("<")[0].strip()
                if base not in interfaces:
                    continue
                for metodo in sorted(interfaces[base]):
                    if not re.search(r"\b" + re.escape(metodo) + r"\s*\(", cuerpo):
                        error(nombre, 0,
                              "La clase '%s' declara '%s' pero no implementa '%s'."
                              % (clase, base, metodo), "C-14")


# Tabla de usos de API mal escritos que YA se cometieron al menos una vez en este
# proyecto. Es deliberadamente una lista curada y no una heuristica: una regla
# generica sobre nombres de miembros produce falsos positivos, y un validador con
# falsos positivos entrena a ignorar su salida (leccion del error E-04).
#
# Cada vez que un error de compilacion sea "el miembro X no existe en el tipo Y",
# se agrega aqui la forma incorrecta con su correccion.
API_MAL_ESCRITA = [
    ("X509FindType.X509FindBy",
     "X509FindType.FindBy... — los miembros de este enum NO repiten el prefijo del tipo"),
    ("StoreName.Personal",
     "StoreName.My — el almacen personal de Windows se llama 'My' en la API"),
    ("CookieSecurePolicy.Secure",
     "CookieSecurePolicy.Always"),
]


def validar_api_conocida(raiz):
    """C-16: usos de API que ya rompieron la compilacion antes."""
    for ruta in archivos(raiz, (".cs", ".cshtml")):
        txt = leer(ruta)
        nombre = rel(ruta, raiz)
        for incorrecto, correccion in API_MAL_ESCRITA:
            desde = 0
            while True:
                i = txt.find(incorrecto, desde)
                if i == -1:
                    break
                error(nombre, numero_de_linea(txt, i),
                      "Uso de API incorrecto '%s'. Lo correcto es: %s" % (incorrecto, correccion),
                      "C-16")
                desde = i + 1


def validar_politicas(raiz):
    """C-12: toda politica referenciada debe estar registrada en Program.cs."""
    program = os.path.join(raiz, "Program.cs")
    if not os.path.exists(program):
        return
    txt_program = leer(program)
    registradas = set(re.findall(r'AddPolicy(?:<[^>]+>)?\(\s*"([^"]+)"', txt_program))

    for ruta in archivos(raiz, (".cs",)):
        txt = leer(ruta)
        nombre = rel(ruta, raiz)
        usadas = re.findall(r'\[Authorize\(Policy\s*=\s*"([^"]+)"', txt)
        usadas += re.findall(r'\[EnableRateLimiting\(\s*"([^"]+)"', txt)
        for politica in set(usadas):
            if politica not in registradas:
                error(nombre, 0,
                      "Referencia la politica '%s' pero Program.cs no la registra con AddPolicy. "
                      "En ejecucion esto lanza InvalidOperationException al entrar a la accion."
                      % politica, "C-12")


# --------------------------------------------------------------------------
# 6. JavaScript propio
# --------------------------------------------------------------------------
def validar_javascript(raiz):
    """C-13: sintaxis de los .js propios (node --check si esta disponible)."""
    js_dir = os.path.join(raiz, "wwwroot", "js")
    if not os.path.isdir(js_dir):
        return
    node = shutil.which("node")
    for ruta in archivos(js_dir, (".js",)):
        nombre = rel(ruta, raiz)
        if node:
            r = subprocess.run([node, "--check", ruta], capture_output=True, text=True)
            if r.returncode != 0:
                error(nombre, 0, "Error de sintaxis JavaScript: %s" % r.stderr.strip().splitlines()[0:1], "C-13")
        else:
            txt = leer(ruta)
            limpio = despojar_csharp(txt)  # sirve igual para JS: quita cadenas y comentarios
            for abre, cierra, etiqueta in (("{", "}", "llaves"), ("(", ")", "parentesis")):
                if limpio.count(abre) != limpio.count(cierra):
                    error(nombre, 0, "%s desbalanceados en el archivo .js." % etiqueta.capitalize(), "C-13")
        # Caracteres no ASCII en codigo ejecutable (no en comentarios): el .js se sirve
        # como estatico sin charset explicito, asi que el navegador puede interpretarlo
        # con otra codificacion. En comentarios es inofensivo.
        codigo = despojar_comentarios_js(leer(ruta))
        try:
            codigo.encode("ascii")
        except UnicodeEncodeError:
            no_ascii = sorted({c for c in codigo if ord(c) > 127})
            aviso(nombre, 0,
                  "Caracteres no ASCII en codigo ejecutable (%s). Usar escapes \\uXXXX para el "
                  "texto visible evita depender de la codificacion con que se sirva el archivo."
                  % " ".join(no_ascii), "C-13")


# --------------------------------------------------------------------------
# Main
# --------------------------------------------------------------------------
def main():
    raiz = sys.argv[1] if len(sys.argv) > 1 else os.path.join("src", "CatalogoCitizenDevIA.Web")
    raiz = os.path.abspath(raiz)
    if not os.path.isdir(raiz):
        print("No existe el directorio del proyecto: %s" % raiz)
        return 2

    print("Validando: %s" % raiz)
    print("-" * 78)

    csp = leer_csp(raiz)
    if csp is None:
        AVISOS.append(("Program.cs", 0, "No se encontro cabecera Content-Security-Policy.", "C-06"))
    else:
        INFO.append("CSP script-src %s 'unsafe-inline'" % ("PERMITE" if csp["permite_inline"] else "NO permite"))

    validar_xml(raiz)
    validar_secretos(raiz)
    validar_vistas(raiz, csp)
    validar_csharp(raiz)
    validar_implementaciones(raiz)
    validar_api_conocida(raiz)
    validar_politicas(raiz)
    validar_javascript(raiz)

    for linea in INFO:
        print("  info    %s" % linea)
    for archivo, ln, mensaje, control in AVISOS:
        print("  AVISO   [%s] %s:%s  %s" % (control, archivo, ln or "-", mensaje))
    for archivo, ln, mensaje, control in ERRORES:
        print("  ERROR   [%s] %s:%s  %s" % (control, archivo, ln or "-", mensaje))

    print("-" * 78)
    print("Resultado: %d error(es), %d aviso(s)." % (len(ERRORES), len(AVISOS)))
    return 1 if ERRORES else 0


if __name__ == "__main__":
    sys.exit(main())
