#!/usr/bin/env bash
# Verificación completa fuera de Unity: compila Core/Runtime/Editor y ejecuta las pruebas EditMode.
# Uso: tools/verify/verify.sh            (desde cualquier directorio)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "==> [1/5] Pacifico.Core (sin referencias al motor)"
dotnet build "$HERE/Pacifico.Core/Pacifico.Core.csproj" -c Release -nologo -v q
echo "==> [2/5] Pacifico.Runtime (contra UnityEngine)"
dotnet build "$HERE/Pacifico.Runtime/Pacifico.Runtime.csproj" -c Release -nologo -v q
echo "==> [3/5] Pacifico.Editor (contra UnityEditor)"
dotnet build "$HERE/Pacifico.Editor/Pacifico.Editor.csproj" -c Release -nologo -v q
echo "==> [4/5] Pruebas que requieren el motor (solo compilación; se ejecutan en Unity)"
dotnet build "$HERE/Pacifico.Tests.Unity/Pacifico.Tests.Unity.csproj" -c Release -nologo -v q
echo "==> [5/5] Pruebas EditMode (NUnit)"
dotnet test "$HERE/Pacifico.Tests/Pacifico.Tests.csproj" -c Release -nologo -v q \
  --logger "console;verbosity=normal" --results-directory "$HERE/TestResults"
echo "==> Verificación completada sin errores."
