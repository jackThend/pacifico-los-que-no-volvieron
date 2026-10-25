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

**Última actualización:** 2026-10-25.

* **Completado:** todas las tareas del roadmap (fases 0 a 6, ocho capítulos jugables) salvo la **0.3**, que está `[EN PROCESO]`: el proyecto ya compila en **Unity 6.6 (6000.6.3f1)**, pasa el Test Runner EditMode (430/430) y construye toda la campaña sin errores. Detalle por tarea en `DEV_LOG.md`.
* **Pendiente de 0.3:** jugar la campaña en el editor, medir con `-pacifico-perf`, voces y música. Si el usuario reporta errores de consola, corregirlos antes que nada.
* **Notas narrativas:** `NOTAS_EQUIPO_NARRATIVO.md` lista lo que el equipo narrativo debe decidir (sobre todo, que el Quiroz real sobrevivió a Miraflores). No cambiar decisiones de historia sin su visto bueno.
* **Arte (fase 7), en curso:** modelos CC-BY de Sketchfab en `Assets/Art/CCBY/Sketchfab/` (GLB con glTFast), con autor y uso en `Assets/Art/CCBY/CREDITOS.md`. Ya colocados: buques (Huáscar, Esmeralda, Independencia, Cochrane, Blanco Encalada, O'Higgins, botes; `ShipModelLibrary`), fusiles en primera persona (Chassepot y Comblain; `RifleModelLibrary`) y escenario (olivos, sauces secos, locomotoras, cajones y parapetos de sacos; `PropModelLibrary`). **Sin colocar:** cañón Krupp de campaña, cañón Armstrong, caballo animado, Remington y Winchester. **Sin modelo que encaje:** Covadonga, Unión, Gatling, soldados, casas de adobe, iglesias y tiendas de época (la descargada es moderna y no se usa).
* **Estética:** todo debe parecer de 1879. Antes de dar por bueno un modelo, quitar lo impropio (banderas ajenas, bidones metálicos, faroles…) con `HiddenParts`/`hiddenParts` y comprobarlo con fotos de Unity (`ShipModelPreview`, `RifleModelPreview`, `SceneryPreview`), que se le envían al usuario.
* **Siguiente:** colocar lo que falta de arte si el usuario lo pide, y seguir con la 0.3.

### Sobre el usuario
* Habla español y **no es técnico**: explicar en lenguaje llano, sin jerga, con pasos cortos. Tiene lesionadas las manos: acompañar el trabajo con páginas HTML de verificación visual en lugar de pedirle pruebas.
* Prueba el juego con los **3 pasos** de la sección "Cómo probar" del `README.md` (descargar ZIP de `main`, abrir `src/UnityProject` en Unity Hub, Play). No le pidas usar git, menús de Unity ni la consola salvo para copiar errores.
* `Editor/FirstRunSetup.cs` construye el menú y todos los capítulos al abrir el proyecto (`GameBuilder.BuildAllScenes`). Al añadir un capítulo, añadir su `BuildScene` a `GameBuilder.BuildAllScenes` y su entrada a `CampaignCatalog`.

### Entorno de trabajo (local, Windows, con Unity)
* Desde 2026-10-22 se trabaja en la carpeta local, igualada con `main` de GitHub. Unity 6.6 en `C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe`.
* **Verificación antes de marcar `[X]`:** Test Runner en modo batch (`-batchmode -nographics -runTests -testPlatform EditMode`) y reconstrucción de la campaña (`-executeMethod Pacifico.EditorTools.GameBuilder.BuildAllScenesBatch`). Las fotos de comprobación necesitan GPU: modo batch **sin** `-nographics`. El proyecto no puede estar abierto dos veces: cerrar el editor del usuario (cierre normal) antes del modo batch y volver a abrirlo al terminar.
* `tools/verify/verify.sh` (compilación contra `Unity3D.SDK` y NUnit fuera del motor) sigue sirviendo en la nube; el código nuevo de URP va tras `PACIFICO_URP` para que compile allí.
* Con el editor abierto, un paquete nuevo puede compilarse sin recargarse: si los GLB quedan con `DefaultImporter`, cerrar el editor y abrir una vez en modo batch.
* La entrada de teclado y ratón pasa siempre por `Runtime/Input/GameInput.cs` (Input System o gestor clásico). No usar `Keyboard.current` ni `UnityEngine.Input` directamente.
* `GetInstanceID()` es error en Unity 6.6: usar la extensión `StableId()` (`Runtime/UnityObjectIds.cs`).

### Convenciones de arquitectura
* La lógica de juego va en `Pacifico.Core` (C# puro, `noEngineReferences`), con tests en `Assets/Tests/EditMode`. Los MonoBehaviours de `Pacifico.Runtime` solo conectan Core con la escena.
* Los datos históricos se definen en catálogos de Core con sus fuentes. Los ScriptableObjects se generan desde el editor; no se escribe YAML a mano. Los textos de las misiones se leen del guion (`GuionQuotes`), no se copian.
* Las escenas se construyen por código (`Editor/*Builder.cs`) con primitivas greybox; los modelos de arte se añaden encima y la primitiva conserva su colisionador.
* Valores de balance marcados como tales en comentarios y en `DEV_LOG.md`.
* Commits en español con prefijo `feat(x.y):`.
