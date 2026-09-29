# CLAUDE.md - Directivas para Claude Code y Agentes Autónomos

Consulta y sigue estrictamente las directivas maestras de desarrollo autónomo definidas en:
* **Protocolo de Agentes Autónomos:** [AGENTS.md](AGENTS.md)
* **Plan Maestro y Estado de Tareas:** [ROADMAP_DE_DESARROLLO.md](ROADMAP_DE_DESARROLLO.md)
* **Bitácora de Sesión y Diagnóstico:** [DEV_LOG.md](DEV_LOG.md)
* **Diseño y Mecánicas del Juego:** [GDD_Narrativo_y_Misiones.md](GDD_Narrativo_y_Misiones.md)
* **Guion Completo y Diálogos:** [Historia_Completa_Guion.md](Historia_Completa_Guion.md)

### Reglas Clave:
1. Revisa siempre la siguiente tarea pendiente en `ROADMAP_DE_DESARROLLO.md` y márcala como `[EN PROCESO]`.
2. No intentes modelar mallas 3D complejas en texto (ahorra tokens; usa primitivas o assets CC0 según `AGENTS.md`).
3. Verifica y compila cada cambio antes de marcar una tarea como completada `[X]`.
4. Si encuentras errores, corrígelos inmediatamente en el bucle autónomo y registra la solución en `DEV_LOG.md`.

---

## 📍 Estado actual y cómo continuar (actualizar al cerrar cada sesión)

**Última actualización:** 2026-09-29.

* **Completado y verificado fuera del motor:** todas las tareas del roadmap (fases 0 a 6: datos, módulo naval, FPS, RTS, sistema narrativo, capítulos jugables 1, 4, 5, 6 y 8 con epílogo, rendimiento y empaquetado) salvo la **0.3**. Detalle por tarea en `DEV_LOG.md`.
* **Pendiente (requiere Unity):** tarea 0.3. Abrir el proyecto, dejar que `FirstRunSetup` construya la campaña, jugarla, pasar el Test Runner y medir con `-pacifico-perf`. Grabar voces y música. Si el usuario reporta errores de consola, corregirlos antes que nada.
* Los capítulos 2 (Angamos), 3 (Pisagua/Dolores) y 7 (Huamachuco) no tienen escena: están en `CampaignCatalog` como no implementados.

### Sobre el usuario
* Habla español y **no es técnico**: explicar en lenguaje llano, sin jerga, con pasos cortos. Tiene lesionadas las manos: acompañar el trabajo con páginas HTML de verificación visual en lugar de pedirle pruebas.
* Prueba el juego con los **3 pasos** de la sección "Cómo probar" del `README.md` (descargar ZIP de `main`, abrir `src/UnityProject` en Unity Hub, Play). No le pidas usar git, menús de Unity ni la consola salvo para copiar errores.
* `Editor/FirstRunSetup.cs` construye el menú y todos los capítulos al abrir el proyecto (`GameBuilder.BuildAllScenes`). Al añadir un capítulo, añadir su `BuildScene` a `GameBuilder.BuildAllScenes` y su entrada a `CampaignCatalog`.

### Entorno de trabajo (nube, sin Unity)
* El hook `SessionStart` (`.claude/settings.json` → `tools/setup_cloud_env.sh`) instala .NET 8 si falta.
* **Verificación obligatoria antes de marcar `[X]`:** `bash tools/verify/verify.sh`. Compila Core, Runtime y Editor contra las DLL reales de UnityEngine/UnityEditor (paquete `Unity3D.SDK`, avisos como errores) y ejecuta las pruebas EditMode con NUnit.
* La entrada de teclado y ratón pasa siempre por `Runtime/Input/GameInput.cs` (Input System o gestor clásico). No usar `Keyboard.current` ni `UnityEngine.Input` directamente.

### Convenciones de arquitectura
* La lógica de juego va en `Pacifico.Core` (C# puro, `noEngineReferences`), con tests en `Assets/Tests/EditMode`. Los MonoBehaviours de `Pacifico.Runtime` solo conectan Core con la escena.
* Los datos históricos se definen en catálogos de Core con sus fuentes. Los ScriptableObjects se generan desde el editor; no se escribe YAML a mano. Los textos de las misiones se leen del guion (`GuionQuotes`), no se copian.
* Las escenas se construyen por código (`Editor/*Builder.cs`) con primitivas greybox.
* Valores de balance marcados como tales en comentarios y en `DEV_LOG.md`.
* Commits en español con prefijo `feat(x.y):`.
