# 📝 BITÁCORA DE DESARROLLO (DEV_LOG)
## «Pacífico: Los que no volvieron»
> **Registro de sesiones, iteraciones autónomas, diagnósticos de errores y estado del proyecto.**

---

### [2026-09-23] - Inicialización y Preparación del Repositorio
* **Responsable:** Antigravity / jackThend
* **Acciones Realizadas:**
  * Creación y publicación del repositorio base en GitHub: `https://github.com/jackThend/pacifico-los-que-no-volvieron`.
  * Integración del catálogo histórico documental con 48 archivos (imágenes de buques, fotografías de soldados, planos y documentos).
  * Redacción del Game Design Document (`GDD_Narrativo_y_Misiones.md`) y del Guion Narrativo Completo (`Historia_Completa_Guion.md`).
  * Creación de directivas de operación autónoma para Agentes de IA (`AGENTS.md`).
  * Estructuración del plan pormenorizado en `ROADMAP_DE_DESARROLLO.md`.
  * Configuración de exclusiones en `.gitignore` para Python, IDEs y motor Unity.
* **Estado Actual:**
  * Fase 0 iniciada. Listo para arrancar el bucle autónomo con la tarea `0.1`.
* **Próximos Pasos:**
  * Iniciar implementación de la Fase 0 en Unity 6 (URP).

---

### [2026-09-28] - Tarea 0.1: Estructura de Proyecto Unity 6 (URP)
* **Responsable:** Claude Code (sesión autónoma en la nube)
* **Entorno:** Sin editor Unity disponible. Se instaló .NET SDK 8 para compilar y testear la lógica C# fuera del motor.
* **Archivos creados:**
  * `src/UnityProject/Assets/` con `Scripts/{Core,Runtime}`, `Prefabs/`, `Scenes/`, `ScriptableObjects/`, `Audio/`, `Materials/`, `UI/`, `Settings/`, `Tests/EditMode/`.
  * `src/UnityProject/ProjectSettings/ProjectVersion.txt` (Unity 6000.0.23f1) y `Packages/manifest.json` (URP 17, Input System, Test Framework, AI Navigation, Timeline, uGUI).
  * Ensamblados: `Pacifico.Core` (C# puro, `noEngineReferences: true`), `Pacifico.Runtime` (MonoBehaviours) y `Pacifico.Tests.EditMode` (NUnit).
  * `Core/GameMode.cs`, `Core/ProjectInfo.cs`, `Runtime/GameBootstrap.cs`, `Tests/EditMode/ProjectInfoTests.cs`.
  * `tools/ci/Pacifico.Core.Tests.csproj`: arnés .NET que compila `Pacifico.Core` + tests EditMode fuera de Unity.
  * `tools/validate_unity_structure.py`: valida carpetas, asmdefs, paquetes, versión y reglas de `.gitignore`.
  * `src/UnityProject/.gitignore` (Library/Temp/Logs/UserSettings/csproj relativos al proyecto Unity).
* **Verificación:**
  * `dotnet test tools/ci/Pacifico.Core.Tests.csproj` (movido luego a `tools/ci/CoreTests/`) → 5/5 tests OK, sin warnings (TreatWarningsAsErrors).
  * `python3 tools/validate_unity_structure.py` → OK.
* **Problemas y soluciones:**
  * Las reglas Unity del `.gitignore` raíz usan rutas ancladas (`/Library/`) que no cubren `src/UnityProject/`. Solución: `.gitignore` propio dentro del proyecto Unity.
  * El `.gitignore` raíz ignora `*.csproj`, lo que bloqueaba el arnés de CI. Solución: excepción `!tools/ci/*.csproj`.
  * `com.unity.textmeshpro` está integrado en `com.unity.ugui` 2.0 en Unity 6; se retiró del manifiesto.
* **Decisión de arquitectura:** toda lógica de dominio (balística, datos históricos, reglas) irá en `Pacifico.Core` sin dependencias de UnityEngine, para poder verificarla con `dotnet test` en entornos sin editor. Los `.meta` los generará Unity al abrir el proyecto por primera vez.
* **Próximos pasos:** Tarea 0.2 (manifiesto de assets y `tools/assets_manager.py`).

---

### [2026-09-28] - Tarea 0.2: Pipeline de Manifiesto de Assets y Google Drive
* **Responsable:** Claude Code (sesión autónoma en la nube)
* **Archivos creados:**
  * `tools/assets_manager.py` (solo biblioteca estándar). Comandos: `status`, `sync [--only ID]`, `add`, `scan-repo`, `test`.
    * Fuentes soportadas: `repo` (versionado en Git), `cache` (solo local), `gdrive` (por `gdrive_id`, descarga vía `drive.google.com/uc?export=download`) y `url` (enlace directo CC0).
    * Descargas atómicas a `assets_cache/` (archivo `.part` + verificación SHA-256 antes de reemplazar). Un hash distinto (p. ej. Drive devolviendo HTML) aborta sin corromper la caché.
  * `assets_manifest.json`: versión 1, registra los 48 archivos de `Archivo_Historico/` con SHA-256, tamaño y licencia.
* **Verificación:**
  * `python3 tools/assets_manager.py test` → 8/8 comprobaciones OK (registro, hash, estado FALTA → descarga vía `file://` → OK, detección y reparación de corrupción, persistencia, URL de Drive).
  * `python3 tools/assets_manager.py status` → 48 assets OK.
* **Notas:** La sincronización real con Google Drive requiere que el archivo sea público (enlace compartido); para carpetas privadas se podrá añadir `rclone` más adelante. La licencia de cada imagen de Wikimedia debe confirmarse en su ficha de origen.
* **Próximos pasos:** Fase 1, tarea 1.1 (`WeaponDataSO`).

---

### [2026-09-28] - Tarea 1.1: Definición de Armamento Histórico (WeaponDataSO)
* **Responsable:** Claude Code (sesión autónoma en la nube)
* **Archivos creados:**
  * `Core/Weapons/`: `WeaponEnums.cs` (clase, mecanismo, bando), `WeaponSpec.cs` (especificación inmutable), `WeaponValidator.cs` (reglas de coherencia), `HistoricalWeapons.cs` (catálogo: Comblain 2.0 s, Chassepot 2.2 s, Gras 2.2 s, Remington Rolling Block 2.1 s, Corvo chileno, Bayoneta triangular).
  * `Runtime/Data/WeaponDataSO.cs`: ScriptableObject editable con `ToSpec()`/`ApplySpec()` y validación en `OnValidate`.
  * `Editor/Pacifico.Editor.asmdef` + `Editor/HistoricalDataGenerator.cs`: menú *Pacífico/Datos/Generar armas históricas* que crea los `.asset` en `ScriptableObjects/Weapons/` desde el catálogo (evita escribir YAML y GUIDs a mano).
  * `Tests/EditMode/WeaponDataTests.cs`: 10 casos (recargas del GDD, validez del catálogo, ids únicos, monotiro 11 mm, rasgos de diseño: Remington mayor daño, Chassepot/Gras menor dispersión, Comblain recarga más rápida; validador rechaza datos incoherentes).
  * `tools/ci/UnityCompileCheck/`: proyecto netstandard2.1 que compila **todos** los scripts (Core + Runtime + Editor) contra stubs mínimos de UnityEngine/UnityEditor.
  * `tools/verify_all.sh`: verificación completa en un comando.
* **Verificación:** `bash tools/verify_all.sh` → estructura OK, assets OK, 15/15 tests, compilación Runtime/Editor 0 errores / 0 warnings.
* **Problemas y soluciones:**
  * Dos `.csproj` en `tools/ci/` compartían `obj/project.assets.json` (restauraciones cruzadas). Solución: un subdirectorio por proyecto (`CoreTests/`, `UnityCompileCheck/`) y reglas de `.gitignore` con `**`.
* **Notas de diseño:** Recargas = valores del GDD. Calibre y velocidad de boca = aproximaciones históricas. Daño, dispersión y alcances = balance inicial, a ajustar en playtesting. Los stubs de Unity solo contienen lo que el código usa; ampliarlos cuando se usen nuevas APIs.
* **Pendiente en editor:** abrir el proyecto en Unity 6, ejecutar el menú de generación y correr los tests EditMode con el Test Runner.
* **Próximos pasos:** Tarea 1.2 (`ShipDataSO`).
