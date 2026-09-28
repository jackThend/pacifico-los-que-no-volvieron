#!/usr/bin/env bash
# Verificación completa fuera de Unity: estructura, assets, tests de Core y
# compilación de Runtime/Editor contra stubs. Uso: bash tools/verify_all.sh
set -euo pipefail
cd "$(dirname "$0")/.."
python3 tools/validate_unity_structure.py
python3 tools/assets_manager.py test > /dev/null && echo "[OK] Autoprueba del gestor de assets."
python3 tools/assets_manager.py status > /dev/null && echo "[OK] Manifiesto de assets sincronizado."
dotnet test tools/ci/CoreTests/Pacifico.Core.Tests.csproj --nologo -v q
dotnet build tools/ci/UnityCompileCheck/Pacifico.Unity.CompileCheck.csproj --nologo -v q
echo "[OK] Verificación completa."
