#!/usr/bin/env bash
# Prepara el entorno de Claude Code en la nube: instala el SDK de .NET 8 si
# falta (necesario para tools/verify_all.sh). En máquinas locales no hace nada.
set -u
[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0
command -v dotnet >/dev/null 2>&1 && exit 0
echo "[setup] Instalando dotnet-sdk-8.0…"
(apt-get install -y --no-install-recommends dotnet-sdk-8.0 >/tmp/setup_dotnet.log 2>&1 \
  || (apt-get update >>/tmp/setup_dotnet.log 2>&1 && apt-get install -y --no-install-recommends dotnet-sdk-8.0 >>/tmp/setup_dotnet.log 2>&1)) \
  && echo "[setup] dotnet instalado." || echo "[setup] No se pudo instalar dotnet (ver /tmp/setup_dotnet.log)."
exit 0
