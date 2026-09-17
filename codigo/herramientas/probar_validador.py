#!/usr/bin/env python3
"""
Bateria de regresion del validador estatico.

Un validador que devuelve "0 errores" no vale nada si nadie comprobo que sabe
detectar algo. Este script arma proyectos sinteticos minimos en un directorio
temporal, le inyecta a cada uno UN defecto conocido -- todos ellos errores que
realmente ocurrieron en este proyecto o que romperian la App en produccion -- y
verifica que validar_codigo.py lo reporte con el codigo de control correcto.

Incluye ademas casos de CONTROL: codigo correcto que se parece al defectuoso y
que NO debe generar hallazgo (asi se evitan falsos positivos como los dos que
tuvo la primera version del propio validador).

    python3 herramientas/probar_validador.py
"""

import os
import shutil
import subprocess
import sys
import tempfile

AQUI = os.path.dirname(os.path.abspath(__file__))
VALIDADOR = os.path.join(AQUI, "validar_codigo.py")

CSPROJ_OK = '''<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Identity.Web" Version="3.15.1" />
  </ItemGroup>
</Project>
'''

PROGRAM_OK = '''using Microsoft.AspNetCore.Authorization;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EsAdministrador", policy => policy.RequireClaim("x", "True"));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'";
    await next();
});
app.Run();
'''

APPSETTINGS_OK = '''{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "AllowedHosts": "*",
  "// ConnectionStrings": "Default viene de variable de entorno."
}
'''

VISTA_OK = '''@model string
<h1>Hola</h1>
@{
    var x = 1;
}
@if (x == 1)
{
    <p>uno</p>
}
@section Scripts {
<script src="~/js/app.js"></script>
}
'''

JS_OK = '''(function () {
    'use strict';
    var a = 1;
    console.log(a);
})();
'''


def armar_proyecto(destino):
    os.makedirs(os.path.join(destino, "Views", "Home"), exist_ok=True)
    os.makedirs(os.path.join(destino, "wwwroot", "js"), exist_ok=True)
    escribir(os.path.join(destino, "App.csproj"), CSPROJ_OK)
    escribir(os.path.join(destino, "Program.cs"), PROGRAM_OK)
    escribir(os.path.join(destino, "appsettings.json"), APPSETTINGS_OK)
    escribir(os.path.join(destino, "Views", "Home", "Index.cshtml"), VISTA_OK)
    escribir(os.path.join(destino, "wwwroot", "js", "app.js"), JS_OK)


def escribir(ruta, contenido):
    with open(ruta, "w", encoding="utf-8") as f:
        f.write(contenido)


def correr(destino):
    r = subprocess.run([sys.executable, VALIDADOR, destino], capture_output=True, text=True)
    return r.stdout + r.stderr


# --------------------------------------------------------------------------
# Cada caso: (nombre, funcion que muta el proyecto, control esperado o None)
# --------------------------------------------------------------------------
def caso_c01(d):
    escribir(os.path.join(d, "App.csproj"),
             CSPROJ_OK.replace("<ItemGroup>",
                               "<!-- correr dotnet list package --vulnerable -->\n  <ItemGroup>"))


def caso_c02(d):
    escribir(os.path.join(d, "App.csproj"), CSPROJ_OK.replace("</Project>", ""))


def caso_c03(d):
    escribir(os.path.join(d, "App.csproj"), CSPROJ_OK.replace('Version="3.15.1"', 'Version="3.*"'))


def caso_c04(d):
    escribir(os.path.join(d, "appsettings.json"),
             '{ "ConnectionStrings": { "Default": "Server=PERMS02;Password=x;" } }')


def caso_c04_control(d):
    pass  # APPSETTINGS_OK ya trae "Default": "Information" bajo Logging:LogLevel


def caso_c05(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"), VISTA_OK.replace(
        '<script src="~/js/app.js"></script>',
        '<script>\n// antes se armaba con confirm(\'@s.Nombre\')\nvar a = 1;\n</script>'))


def caso_c05_control(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"), VISTA_OK.replace(
        '<script src="~/js/app.js"></script>',
        '<script>\n// antes se armaba con confirm(\'@@s.Nombre\')\nvar a = 1;\n</script>'))


def caso_c06(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"), VISTA_OK.replace(
        '<script src="~/js/app.js"></script>', '<script>var a = 1;</script>'))


def caso_c07(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"), VISTA_OK.replace(
        "<h1>Hola</h1>", '<select onchange="this.form.submit()"></select>'))


def caso_c08(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"),
             VISTA_OK.rstrip()[:-1])  # se come la llave de cierre de @section


def caso_c08_if(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"), VISTA_OK.replace(
        "<h1>Hola</h1>",
        '@if (true)\n{\n    <p>sin cerrar</p>\n'))


def caso_c09(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"),
             VISTA_OK.replace("app.js", "no-existe.js"))


def caso_c10(d):
    escribir(os.path.join(d, "Extra.cs"), "public class A { public void B() { } ")


def caso_c10_control(d):
    escribir(os.path.join(d, "Extra.cs"),
             'public class A { public void B() { var s = "{ llave en cadena {"; var c = \'}\'; } }')


def caso_c11(d):
    escribir(os.path.join(d, "Ctrl.cs"),
             'public class C { [EnableRateLimiting("OperacionesSensibles")] public void A() { } }')


def caso_c12(d):
    escribir(os.path.join(d, "Ctrl.cs"),
             'using Microsoft.AspNetCore.Authorization;\n'
             'public class C { [Authorize(Policy = "PolicyInexistente")] public void A() { } }')


def caso_c13(d):
    escribir(os.path.join(d, "wwwroot", "js", "app.js"), "(function () { var a = 1;\n")


def caso_c14(d):
    escribir(os.path.join(d, "Repo.cs"),
             "public interface IRepo { void Guardar(int x); void Borrar(int x); }\n"
             "public class RepoEf : IRepo { public void Guardar(int x) { } }")


def caso_c14_control(d):
    escribir(os.path.join(d, "Repo.cs"),
             "public interface IRepo { void Guardar(int x); void Borrar(int x); }\n"
             "public class RepoEf : IRepo { public void Guardar(int x) { } public void Borrar(int x) { } }")


def caso_c15(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"), VISTA_OK.replace(
        "<h1>Hola</h1>",
        '<form method="post"><input type="file" name="archivo" /></form>'))


def caso_c16(d):
    escribir(os.path.join(d, "Cert.cs"),
             "public class C { public void A() { var f = X509FindType.X509FindByThumbprint; } }")


def caso_c16_control(d):
    escribir(os.path.join(d, "Cert.cs"),
             "public class C { public void A() { var f = X509FindType.FindByThumbprint; } }")


def caso_c15_control(d):
    escribir(os.path.join(d, "Views", "Home", "Index.cshtml"), VISTA_OK.replace(
        "<h1>Hola</h1>",
        '<form method="post" enctype="multipart/form-data"><input type="file" name="archivo" /></form>'))


CASOS = [
    ("C-01 comentario XML con '--'", caso_c01, "C-01"),
    ("C-02 XML mal formado", caso_c02, "C-02"),
    ("C-03 version NuGet flotante", caso_c03, "C-03"),
    ("C-04 cadena de conexion en appsettings", caso_c04, "C-04"),
    ("C-04 CONTROL: LogLevel:Default no es secreto", caso_c04_control, "!C-04"),
    ("C-05 '@' en comentario JS dentro de Razor", caso_c05, "C-05"),
    ("C-05 CONTROL: '@@' escapado es valido", caso_c05_control, "!C-05"),
    ("C-06 script inline con CSP estricta", caso_c06, "C-06"),
    ("C-07 manejador on*= con CSP estricta", caso_c07, "C-07"),
    ("C-08 @section sin cerrar", caso_c08, "C-08"),
    ("C-08 bloque @if sin cerrar antes de @section", caso_c08_if, "C-08"),
    ("C-09 referencia a estatico inexistente", caso_c09, "C-09"),
    ("C-10 llaves desbalanceadas en C#", caso_c10, "C-10"),
    ("C-10 CONTROL: llaves dentro de cadenas", caso_c10_control, "!C-10"),
    ("C-11 using faltante", caso_c11, "C-11"),
    ("C-12 politica no registrada", caso_c12, "C-12"),
    ("C-13 JavaScript invalido", caso_c13, "C-13"),
    ("C-14 interfaz implementada a medias", caso_c14, "C-14"),
    ("C-14 CONTROL: interfaz completa", caso_c14_control, "!C-14"),
    ("C-15 form con archivo sin enctype", caso_c15, "C-15"),
    ("C-15 CONTROL: form con enctype", caso_c15_control, "!C-15"),
    ("C-16 miembro de enum inexistente", caso_c16, "C-16"),
    ("C-16 CONTROL: miembro correcto", caso_c16_control, "!C-16"),
]


def main():
    base = tempfile.mkdtemp(prefix="prueba-validador-")
    fallos = 0
    try:
        # Cordura: el proyecto limpio debe pasar sin hallazgos
        limpio = os.path.join(base, "limpio")
        os.makedirs(limpio)
        armar_proyecto(limpio)
        salida = correr(limpio)
        if "0 error(es), 0 aviso(s)" not in salida:
            print("  FALLA   proyecto de referencia limpio genera hallazgos:")
            print(salida)
            fallos += 1
        else:
            print("  ok      proyecto de referencia limpio: sin hallazgos")

        for nombre, mutar, esperado in CASOS:
            d = os.path.join(base, nombre.split()[0] + "-" + str(abs(hash(nombre)) % 10000))
            os.makedirs(d)
            armar_proyecto(d)
            mutar(d)
            salida = correr(d)
            if esperado.startswith("!"):
                # Caso de control: codigo correcto que se parece al defectuoso. Se exige
                # que NO dispare ESE control en particular (otros hallazgos del mismo
                # fixture son legitimos y no invalidan la prueba).
                prohibido = esperado[1:]
                if ("[%s]" % prohibido) not in salida:
                    print("  ok      %s -> no dispara %s, correcto" % (nombre, prohibido))
                else:
                    print("  FALLA   %s -> disparo %s y NO debia" % (nombre, prohibido))
                    print("          " + " / ".join(l.strip() for l in salida.splitlines()
                                                    if prohibido in l))
                    fallos += 1
            else:
                if ("[%s]" % esperado) in salida:
                    print("  ok      %s -> detectado como %s" % (nombre, esperado))
                else:
                    print("  FALLA   %s -> NO se detecto (esperaba %s)" % (nombre, esperado))
                    print("          " + salida.strip().replace("\n", "\n          "))
                    fallos += 1
    finally:
        shutil.rmtree(base, ignore_errors=True)

    print("-" * 78)
    print("Bateria de regresion: %d caso(s), %d falla(s)." % (len(CASOS) + 1, fallos))
    return 1 if fallos else 0


if __name__ == "__main__":
    sys.exit(main())
