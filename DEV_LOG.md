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
  * `dotnet test tools/ci/Pacifico.Core.Tests.csproj` → 5/5 tests OK, sin warnings (TreatWarningsAsErrors).
  * `python3 tools/validate_unity_structure.py` → OK.
* **Problemas y soluciones:**
  * Las reglas Unity del `.gitignore` raíz usan rutas ancladas (`/Library/`) que no cubren `src/UnityProject/`. Solución: `.gitignore` propio dentro del proyecto Unity.
  * El `.gitignore` raíz ignora `*.csproj`, lo que bloqueaba el arnés de CI. Solución: excepción `!tools/ci/*.csproj`.
  * `com.unity.textmeshpro` está integrado en `com.unity.ugui` 2.0 en Unity 6; se retiró del manifiesto.
* **Decisión de arquitectura:** toda lógica de dominio (balística, datos históricos, reglas) irá en `Pacifico.Core` sin dependencias de UnityEngine, para poder verificarla con `dotnet test` en entornos sin editor. Los `.meta` los generará Unity al abrir el proyecto por primera vez.
* **Próximos pasos:** Tarea 0.2 (manifiesto de assets y `tools/assets_manager.py`).
