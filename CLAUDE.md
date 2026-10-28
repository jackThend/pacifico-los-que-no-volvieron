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

**Última actualización:** 2026-10-28.

* **Completado:** todas las tareas del roadmap (fases 0 a 6, ocho capítulos jugables) salvo la **0.3**, que está `[EN PROCESO]`: el proyecto ya compila en **Unity 6.6 (6000.6.3f1)**, pasa el Test Runner EditMode (430/430) y construye toda la campaña sin errores. Detalle por tarea en `DEV_LOG.md`.
* **Sonido (2026-10-28):** efectos y ambientes de Sonniss GameAudioGDC en `Assets/Resources/Sonido/<grupo>` (créditos en su `CREDITOS.md`), preparados con `tools/audio/preparar_sonidos.py`; código en `Runtime/Audio` (`GameAudio.Play(Sfx.X, punto)`, `SoundLoop`, `Ambience` con capas por escena). Faltan fusiles de pólvora negra, tambores y cornetas.
* **Pendiente de 0.3:** jugar la campaña en el editor, voces y música. Rendimiento medido el 2026-10-28 en una Intel Iris Xe a 1080p: todos los capítulos entre 52 y 60 imágenes/s (`AdaptiveQuality`, buques unidos; detalle en `DEV_LOG.md`). Si el usuario reporta errores de consola, corregirlos antes que nada.
* **Notas narrativas:** `NOTAS_EQUIPO_NARRATIVO.md` lista lo que el equipo narrativo debe decidir (sobre todo, que el Quiroz real sobrevivió a Miraflores). No cambiar decisiones de historia sin su visto bueno.
* **Arte (fase 7), en curso:** modelos CC-BY de Sketchfab en `Assets/Art/CCBY/Sketchfab/` (GLB con glTFast), con autor y uso en `Assets/Art/CCBY/CREDITOS.md`. Ya colocados: buques (Huáscar, Esmeralda, Independencia, Cochrane, Blanco Encalada, O'Higgins, botes; `ShipModelLibrary`), fusiles en primera persona (Chassepot y Comblain; `RifleModelLibrary`), escenario (olivos, sauces secos, locomotoras, cajones, parapetos de sacos; `PropModelLibrary`), cañones de campaña (caps. 3, 4, 5 y 8; `DressFieldGun`) y caballos de Cáceres y Ugarte (`DressHorse`, de pie: su clip de 60 s mezcla todos los aires; recortar un tramo de paso o galope requiere verlo en el editor). **Sin colocar:** cañón Armstrong, Remington y Winchester (sin uso). **Soldados (2026-10-25):** los dos modelos venían sin huesos y Mixamo no los aceptó; `SoldierModelLibrary` (editor) une sus piezas, las aligera con los niveles de detalle de Unity 6.2+, les pone esqueleto y pesos por distancia a los huesos y guarda `Assets/Art/CCBY/Soldados/Soldado_{Chileno,Peruano}.prefab`; `SoldierFigure` (runtime) los mueve por código (paso, carrera, rodilla en tierra, cuerpo a tierra, fusil terciado o encarado con IK de brazos) leyendo la postura de la «Figura». Fotos: `SoldierPreview`. Uniformes por unidad hechos (parte 6). Aligerados a ~28 000 triángulos. **Sin modelo que encaje:** Covadonga, Unión, Gatling, casas de adobe, iglesias y tiendas de época.
* **Arte realista por partes (pedido del usuario, 2026-10-25):** 1) cielo, luz y posproceso — hecho (`SceneAtmosphere`, fotos con `AtmospherePreview`); 2) terreno y suelos — hecho (`GroundDressing`, texturas «aplanadas» con `tools/arte/aplanar_texturas.py`); 3) agua y mar — hecho (shader `Pacifico/Mar`, `SeaDressing`, texturas de `tools/arte/generar_agua.py`); 4) naturaleza — hecho (`NatureDressing`: matorral, ramas, piedras y peñascos de Namaqualand, arbustos y caña); 5) edificios y objetos — hecho (`BuildingDressing`: texturas por material de construcción y objetos de época junto a las casas; faltan puertas y ventanas); 6) personajes — hecho (uniformes teñidos por unidad, civiles y culíes, tamborcillo, oficiales y jinetes con `SoldierModelLibrary.Prefab/Dress/Attach`; reconstruir sin `-nographics`); 7) buques — hecho (`ShipDressing`: humo de chimenea `FunnelSmoke`, estela `ShipWake`, velas de lona; fotos con `ShipDressing.PreviewBatch`). Plan de arte completo; pendiente: puertas y ventanas, Covadonga y Unión. Siempre con recursos gratuitos (Poly Haven CC0, Sketchfab CC-BY con crédito).
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
* **Un MonoBehaviour por archivo y con su mismo nombre:** si no, queda como «script perdido» en la escena guardada y el juego compilado dice «levelN is corrupted».
* **Compilar el juego (no solo escenas):** la coma de la ruta rompe UnityLinker; compilar desde el enlace `C:\Users\caosd\PacificoUnity` con `GameBuilder.BuildWindowsPlayerOnlyBatch` y reconstruir las escenas desde la ruta original.
* `GetInstanceID()` es error en Unity 6.6: usar la extensión `StableId()` (`Runtime/UnityObjectIds.cs`).

### Convenciones de arquitectura
* La lógica de juego va en `Pacifico.Core` (C# puro, `noEngineReferences`), con tests en `Assets/Tests/EditMode`. Los MonoBehaviours de `Pacifico.Runtime` solo conectan Core con la escena.
* Los datos históricos se definen en catálogos de Core con sus fuentes. Los ScriptableObjects se generan desde el editor; no se escribe YAML a mano. Los textos de las misiones se leen del guion (`GuionQuotes`), no se copian.
* Las escenas se construyen por código (`Editor/*Builder.cs`) con primitivas greybox; los modelos de arte se añaden encima y la primitiva conserva su colisionador.
* Valores de balance marcados como tales en comentarios y en `DEV_LOG.md`.
* Commits en español con prefijo `feat(x.y):`.
