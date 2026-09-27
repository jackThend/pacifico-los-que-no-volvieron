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

### [2026-09-27] - Tarea 0.1: Estructura de Proyecto Unity 6 (URP) — [X]
* **Responsable:** Claude Code (sesión autónoma)
* **Entorno:** Contenedor Linux sin Editor de Unity. Se instaló .NET SDK 8.0 (paquete de Ubuntu) para verificar compilación y pruebas.
* **Archivos creados:**
  * `src/UnityProject/` con `Packages/manifest.json` (URP 17, Test Framework, Input System, AI Navigation, Timeline, uGUI) y `ProjectSettings/ProjectVersion.txt` (Unity 6000.0 LTS).
  * `Assets/Scripts/{Core,Runtime,Editor}` + `Assets/Tests/EditMode` con sus `.asmdef`. `Pacifico.Core` usa `noEngineReferences: true`: toda la simulación (balística, hidrodinámica, blindaje) es C# puro y probable fuera del motor.
  * `Assets/{Prefabs,Scenes,ScriptableObjects,Audio,Materials,UI}`.
  * `tools/verify/` — arnés de verificación: compila Core (netstandard2.1, C# 9), Runtime contra `UnityEngine.dll` y Editor contra `UnityEditor.dll` (paquete NuGet `Unity3D.SDK` 2021.1, solo como referencia de API), y ejecuta las pruebas EditMode con NUnit 3.
  * `.gitignore`: reglas de Unity ancladas a `src/UnityProject/`, excepción para los `.csproj` del arnés y `assets_cache/`.
* **Verificación:** `tools/verify/verify.sh` → 3 ensamblados compilan con 0 advertencias (advertencias tratadas como errores); 8/8 pruebas OK. Prueba negativa: una llamada inexistente sobre `Rigidbody` hace fallar la compilación del Runtime (el arnés detecta errores de API reales).
* **Incidencias y solución:**
  * `builds.dotnet.microsoft.com` bloqueado por el proxy → se instaló `dotnet-sdk-8.0` desde los repositorios de Ubuntu.
  * `NU1701` (paquete de Unity dirigido a .NET Framework) tratado como error → suprimido solo en esa referencia.
* **Limitación conocida:** las referencias de API corresponden a Unity 2021.1; el código evita APIs renombradas en Unity 6 (`Rigidbody.velocity` → `linearVelocity`, `FindObjectOfType`) o las protege con `#if UNITY_6000_0_OR_NEWER`. La verificación definitiva sigue siendo abrir el proyecto en Unity 6 y ejecutar el Test Runner.

---

### [2026-09-27] - Tarea 0.2: Pipeline de Manifiesto de Assets y Google Drive — [X]
* **Archivos creados:**
  * `tools/assets_manager.py` (solo biblioteca estándar): comandos `scan`, `status`, `verify`, `sync`, `add`, `push`, `test`.
    * Descargas en streaming a `.part`, verificación SHA-256 y renombrado atómico: un hash incorrecto nunca deja un archivo corrupto.
    * Orígenes: `gdrive` (maneja la página de confirmación de archivos grandes), `http`, `wikimedia` (reutiliza la tabla de `download_historical_archive.py`) y `local`.
    * `push` sube `assets_cache/` a Google Drive con `rclone` (`PACIFICO_GDRIVE_REMOTE`).
    * Aplica la regla de AGENTS.md §4.B: rechaza binarios > 50 MB en git y exige que los assets de caché tengan un origen descargable.
  * `assets_manifest.json`: 48 archivos del Archivo Histórico con tamaño, SHA-256 y ficha de Commons.
  * `tools/tests/test_assets_manager.py`: 17 pruebas (`python -m unittest discover -s tools/tests`).
* **Verificación:** 17/17 pruebas OK. `python tools/assets_manager.py test` → 48 assets, 32,7 MB, todos `OK`, con hashes impresos.
* **Incidencias y solución:**
  * `print_report` fijaba `sys.stdout` al definirse y no respetaba `redirect_stdout` (lo detectó una prueba) → se resuelve al llamar.
  * El primer borrador marcaba todo el archivo como «dominio público». Es incorrecto: hay fotos modernas (réplica de uniforme, monumento con la carta de Grau) que suelen estar bajo CC BY-SA. Ahora la licencia queda «por verificar» con el enlace a la ficha de Commons (`source.page`).
* **Pendiente fuera del alcance del agente:** `sync` contra Google Drive/Commons no se pudo probar con red real (el proxy del entorno bloquea esos dominios); está cubierto con un downloader simulado.

---

### [2026-09-27] - Tarea 1.1: Definición de Armamento Histórico (WeaponDataSO) — [X]
* **Archivos creados:**
  * `Core/Common/HistoricalSource.cs`: referencias, campos estimados y notas de cada dato (trazabilidad histórica).
  * `Core/Weapons/WeaponSpec.cs`: especificación con **datos históricos** (cartucho, calibre, velocidad en boca, masa, alza) separados de los **ajustes de juego** (recarga, daño, dispersión MOA, alcance eficaz, cuerpo a cuerpo). Incluye caída de daño con la distancia, radio de dispersión, cadencia sostenida y `Validate()`.
  * `Core/Weapons/WeaponCatalog.cs`: Comblain II, Chassepot 1866, Gras 1874, Remington Rolling Block, Winchester 1873 (Quintín, cap. 7), corvo y bayoneta triangular.
  * `Runtime/Data/WeaponDataSO.cs` (+ `SerializableHistoricalSource.cs`): asset editable con `ToSpec()`/`CopyFrom()` y validación en `OnValidate`.
  * `Editor/HistoricalDataAssetGenerator.cs`: menú «Pacífico/Datos/Generar ScriptableObjects históricos» (idempotente, invocable en modo batch).
  * Pruebas: `Tests/EditMode/Weapons/WeaponCatalogTests.cs` (recargas del GDD 2,0/2,2/2,2/2,1 s, validez, jerarquía de daño y precisión, bandos, copias independientes) y `Tests/EditModeUnity/DataAssetSerializationTests.cs` (ida y vuelta por `JsonUtility`; solo corre dentro de Unity).
* **Fuentes consultadas:** Chassepot 410 m/s, 4,635 kg y 1.200 m; Gras 450 m/s y 4,2 kg; .43 Spanish ≈389 m/s; Comblain belga 4,3 kg y 1.300 m. Los valores sin fuente directa quedan listados en `EstimatedFields`.
* **Verificación:** `tools/verify/verify.sh` → 5 pasos en verde, 26/26 pruebas OK.
* **Cambio en el arnés:** nuevo paso que compila `Pacifico.Tests.Unity` (pruebas que necesitan el motor).
