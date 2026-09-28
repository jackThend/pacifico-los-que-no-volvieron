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

**Última actualización:** 2026-09-28.

* **Completado y verificado:** Fase 0 (0.1, 0.2), Fase 1 (1.1–1.3) y Fase 2 (2.1–2.4, módulo naval completo). Detalle por tarea en `DEV_LOG.md`.
* **Siguiente tarea:** **3.1 Controlador de Primera Persona Inmersivo** (Fase 3, prototipo Pisagua).
* **Pendiente de probar en el editor real:** nada se ha compilado aún en Unity. Todo está verificado fuera del motor. Si el usuario reporta errores de consola, corregirlos antes de seguir con el roadmap.

### Sobre el usuario
* Habla español y **no es técnico**: explicar en lenguaje llano, sin jerga, con pasos cortos.
* Prueba el juego con los **3 pasos** de la sección "Cómo probar" del `README.md` (descargar ZIP de `main`, abrir `src/UnityProject` en Unity Hub, Play). No le pidas usar git, menús de Unity ni la consola salvo para copiar errores.
* `FirstRunSetup.cs` (editor) genera datos y la escena de Iquique al abrir el proyecto: mantener ese flujo automático cuando se añadan escenas nuevas (p. ej. la de Pisagua).

### Entorno de trabajo (nube, sin Unity)
* El hook `SessionStart` (`.claude/settings.json` → `tools/setup_cloud_env.sh`) instala .NET 8 si falta.
* **Verificación obligatoria antes de marcar `[X]`:** `bash tools/verify_all.sh`, que comprueba estructura, assets, tests EditMode con NUnit (`tools/ci/EditModeTests`) y compilación de Runtime/Editor (`tools/ci/UnityCompileCheck`).
* Los scripts de Runtime/Editor compilan contra **stubs** mínimos de Unity en `tools/ci/UnityStubs/`. Al usar una API nueva de Unity, añadirla al stub **con la firma real** (si no, compila aquí y falla en Unity).
* La entrada de teclado y ratón pasa siempre por `Runtime/Input/GameInput.cs`, que funciona con el Input System nuevo y con el clásico. No usar `Keyboard.current` ni `UnityEngine.Input` directamente.

### Convenciones de arquitectura
* La lógica de juego va en `Pacifico.Core` (C# puro, `noEngineReferences`), con tests en `Assets/Tests/EditMode`. Los MonoBehaviours de `Pacifico.Runtime` solo conectan Core con la escena.
* Los datos históricos se definen en catálogos de Core (`HistoricalWeapons`, `HistoricalShips`, `HistoricalDocumentSources`). Los ScriptableObjects se generan desde el menú del editor (*Pacífico/Datos/Generar todo*); no se escribe YAML a mano.
* Las escenas se construyen por código (`Editor/NavalPrototypeSceneBuilder.cs`) con primitivas greybox.
* Valores de balance (daño, velocidades de puntería, umbrales) marcados como tales en comentarios y en `DEV_LOG.md`.
* Commits en español con prefijo `feat(x.y):`; trabajo integrado en `main`.
