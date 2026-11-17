"""Crea el instalador de Windows del juego ya compilado.

Pasos previos: compilar el juego con GameBuilder.BuildWindowsPlayerOnlyBatch (ver CLAUDE.md, «Compilar el juego»).
Este programa lee la versión de GameBuilder.Version, junta los créditos de los recursos gratuitos en Creditos.txt
(los modelos CC-BY exigen citar a sus autores) y llama a Inno Setup 6.

Resultado: tools/instalador/salida/Pacifico_Instalador_<versión>.exe   (la carpeta salida/ no se sube a git)

Uso: python tools/instalador/crear_instalador.py   (desde la raíz del repositorio)
"""
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
UNITY = os.path.join(ROOT, "src", "UnityProject")
BUILD = os.path.join(UNITY, "Builds", "StandaloneWindows64")
ISCC = r"C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
CREDITS = [
    ("Modelos 3D (Sketchfab, licencia CC-BY)", os.path.join(UNITY, "Assets", "Art", "CCBY", "CREDITOS.md")),
    ("Texturas y cielos (Poly Haven, CC0)", os.path.join(UNITY, "Assets", "Art", "CC0", "CREDITOS.md")),
    ("Sonido", os.path.join(UNITY, "Assets", "Resources", "Sonido", "CREDITOS.md")),
]


def version():
    with open(os.path.join(UNITY, "Assets", "Scripts", "Editor", "GameBuilder.cs"), encoding="utf-8") as f:
        match = re.search(r'const string Version = "([0-9.]+)"', f.read())
    if not match:
        sys.exit("No encuentro GameBuilder.Version")
    return match.group(1)


def credits_text(ver):
    lines = [
        "PACÍFICO: LOS QUE NO VOLVIERON — versión " + ver,
        "",
        "Este juego usa recursos gratuitos de otros autores. Gracias a todos ellos.",
        "",
    ]
    for title, path in CREDITS:
        lines += ["=" * 78, title, "=" * 78, ""]
        with open(path, encoding="utf-8") as f:
            lines.append(f.read().strip())
        lines.append("")
    return "\r\n".join(lines) + "\r\n"


def main():
    if not os.path.isfile(os.path.join(BUILD, "Pacifico.exe")):
        sys.exit("Falta el juego compilado en " + BUILD)
    if not os.path.isfile(ISCC):
        sys.exit("Falta Inno Setup 6 en " + ISCC)
    ver = version()
    prepared = os.path.join(HERE, "salida", "preparado")
    os.makedirs(prepared, exist_ok=True)
    # Con marca de orden de bytes para que el Bloc de notas muestre bien las tildes.
    with open(os.path.join(prepared, "Creditos.txt"), "w", encoding="utf-8-sig", newline="") as f:
        f.write(credits_text(ver))
    result = subprocess.run([ISCC, "/Q", "/DVersion=" + ver, os.path.join(HERE, "Pacifico.iss")], cwd=HERE)
    if result.returncode != 0:
        sys.exit("Inno Setup falló (código %d)" % result.returncode)
    out = os.path.join(HERE, "salida", "Pacifico_Instalador_%s.exe" % ver)
    print("Instalador creado: %s (%.0f MB)" % (out, os.path.getsize(out) / 1048576))


if __name__ == "__main__":
    main()
