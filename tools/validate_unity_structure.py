#!/usr/bin/env python3
"""Valida la estructura del proyecto Unity 6 (URP) y las reglas de .gitignore.

Uso: python3 tools/validate_unity_structure.py
Devuelve código de salida 0 si todo es correcto, 1 si hay errores.
"""
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = os.path.join(ROOT, "src", "UnityProject")

REQUIRED_DIRS = [
    "Assets/Scripts/Core",
    "Assets/Scripts/Runtime",
    "Assets/Scripts/Editor",
    "Assets/Prefabs",
    "Assets/Scenes",
    "Assets/ScriptableObjects",
    "Assets/Audio",
    "Assets/Materials",
    "Assets/UI",
    "Assets/Settings",
    "Assets/Tests/EditMode",
    "Packages",
    "ProjectSettings",
]

ASMDEFS = [
    "Assets/Scripts/Core/Pacifico.Core.asmdef",
    "Assets/Scripts/Runtime/Pacifico.Runtime.asmdef",
    "Assets/Scripts/Editor/Pacifico.Editor.asmdef",
    "Assets/Tests/EditMode/Pacifico.Tests.EditMode.asmdef",
]

REQUIRED_PACKAGES = [
    "com.unity.render-pipelines.universal",
    "com.unity.inputsystem",
    "com.unity.test-framework",
    "com.unity.ai.navigation",
]

# Rutas que deben quedar ignoradas / versionadas (relativas a la raíz del repo).
MUST_IGNORE = [
    "src/UnityProject/Library/x",
    "src/UnityProject/Temp/x",
    "src/UnityProject/Logs/x",
    "src/UnityProject/UserSettings/x",
    "src/UnityProject/Assembly-CSharp.csproj",
    "tools/ci/CoreTests/bin/x",
    "tools/ci/UnityCompileCheck/obj/x",
    "assets_cache/x",
]
MUST_TRACK = [
    "src/UnityProject/Assets/Scripts/Core/ProjectInfo.cs",
    "src/UnityProject/Packages/manifest.json",
    "src/UnityProject/ProjectSettings/ProjectVersion.txt",
    "tools/ci/CoreTests/Pacifico.Core.Tests.csproj",
    "tools/ci/UnityCompileCheck/Pacifico.Unity.CompileCheck.csproj",
]


def is_ignored(path):
    result = subprocess.run(
        ["git", "check-ignore", "-q", "--no-index", path], cwd=ROOT
    )
    return result.returncode == 0


def main():
    errors = []

    for rel in REQUIRED_DIRS:
        if not os.path.isdir(os.path.join(UNITY, rel)):
            errors.append(f"Falta carpeta: {rel}")

    for rel in ASMDEFS:
        path = os.path.join(UNITY, rel)
        try:
            with open(path, encoding="utf-8") as fh:
                data = json.load(fh)
            if data.get("name") != os.path.basename(rel)[: -len(".asmdef")]:
                errors.append(f"Nombre de asmdef no coincide con archivo: {rel}")
        except (OSError, ValueError) as exc:
            errors.append(f"asmdef inválido {rel}: {exc}")

    try:
        with open(os.path.join(UNITY, "Packages/manifest.json"), encoding="utf-8") as fh:
            deps = json.load(fh).get("dependencies", {})
        for pkg in REQUIRED_PACKAGES:
            if pkg not in deps:
                errors.append(f"Falta paquete en manifest.json: {pkg}")
    except (OSError, ValueError) as exc:
        errors.append(f"manifest.json inválido: {exc}")

    try:
        with open(os.path.join(UNITY, "ProjectSettings/ProjectVersion.txt"), encoding="utf-8") as fh:
            if "m_EditorVersion: 6000." not in fh.read():
                errors.append("ProjectVersion.txt no apunta a Unity 6 (6000.x)")
    except OSError as exc:
        errors.append(f"ProjectVersion.txt ilegible: {exc}")

    for rel in MUST_IGNORE:
        if not is_ignored(rel):
            errors.append(f".gitignore no excluye: {rel}")
    for rel in MUST_TRACK:
        if is_ignored(rel):
            errors.append(f".gitignore excluye por error: {rel}")

    if errors:
        for err in errors:
            print(f"[ERROR] {err}")
        return 1
    print("[OK] Estructura Unity 6 (URP) y .gitignore verificados.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
