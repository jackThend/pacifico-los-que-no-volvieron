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

---

### [2026-09-28] - Tarea 1.2: Definición de Buques y Blindajes (ShipDataSO)
* **Responsable:** Claude Code (sesión autónoma en la nube)
* **Archivos creados / modificados:**
  * `Core/Faction.cs`: el enum `Faction` pasa de `Pacifico.Core.Weapons` a `Pacifico.Core` (lo comparten armas y buques).
  * `Core/Ships/`: `ShipEnums.cs` (tipo, material de casco, zona de coraza, montaje), `ArmorPlate.cs` y `GunBattery.cs` (clases `[Serializable]` con campos públicos), `ShipSpec.cs`, `ShipValidator.cs`, `HistoricalShips.cs`.
  * Catálogo: *Huáscar* (monitor de hierro, cinturón 4.5", torre Coles 5.5" con 2 × Armstrong de 300 lb, espolón), *Esmeralda* (corbeta de madera, sin coraza, 16 × 40 lb, ~4 nudos por calderas averiadas), *Cochrane* (reducto central, cinturón 9", 6 × Armstrong de 250 lb) e *Independencia* (fragata blindada, 4.5", Vavasseur 150 lb + 12 × 70 lb).
  * `Runtime/Data/ShipDataSO.cs`: ScriptableObject con arrays de `ArmorPlate`/`GunBattery`, copias profundas en `ApplySpec`/`ToSpec` y validación en `OnValidate`.
  * `Editor/HistoricalDataGenerator.cs`: generador genérico; nuevos menús *Generar buques históricos* y *Generar todo*.
  * Tests: `ShipDataTests.cs` (9 casos) y `DataAssetSerializationTests.cs` (instanciación con `CreateInstance`, conservación de datos del catálogo, ausencia de instancias compartidas y reglas del serializador de Unity por reflexión para `ShipDataSO` y `WeaponDataSO`).
  * `tools/ci/`: `CoreTests/` → `EditModeTests/` (ahora compila también Runtime contra stubs, para probar los ScriptableObjects); stubs movidos a `tools/ci/UnityStubs/` compartidos por ambos proyectos. El asmdef de tests EditMode referencia `Pacifico.Runtime`.
* **Verificación:** `bash tools/verify_all.sh` → estructura OK, assets OK, 29/29 tests, compilación Runtime/Editor 0 errores / 0 warnings.
  * Prueba de mutación: quitar `[SerializeField]` de `hasRam` hace fallar `CamposSonSerializablesPorUnity` con el mensaje esperado.
* **Problemas y soluciones:**
  * El stub `ScriptableObject.CreateInstance<T>()` exigía `new()`, restricción que Unity no tiene; el generador genérico no compilaba. Solución: stub con `Activator.CreateInstance<T>()` y la misma firma que Unity.
* **Notas de diseño:** Coraza, calibres y dimensiones son aproximaciones históricas; integridad de casco y recargas de artillería son balance inicial. Los límites angulares de la torre Coles se definirán en la tarea 2.2. El *Blanco Encalada* y la *Covadonga* (citados en el guion) se añadirán al catálogo cuando se integren sus capítulos.
* **Pendiente en editor:** ejecutar *Pacífico/Datos/Generar todo* y los tests EditMode en Unity 6.
* **Próximos pasos:** Tarea 1.3 (`CollectibleDataSO`).

---

### [2026-09-28] - Tarea 1.3: Coleccionables "La Memoria Rota" (CollectibleDataSO)
* **Responsable:** Claude Code (sesión autónoma en la nube)
* **Archivos creados / modificados:**
  * `Core/Collectibles/`: `CollectibleType.cs`, `CollectibleSpec.cs`, `CollectibleValidator.cs` (incluye detección de restos de Markdown en el texto mostrado), `DocumentSource.cs` (registro de fuentes con los datos que el Markdown no declara: bando, facsímil, remitente/destinatario por defecto), `CollectibleMarkdownParser.cs` y `CollectibleLibrary.cs` (carga desde disco y localización de la raíz del repositorio).
  * El parser admite los dos formatos existentes: documento único (sección `### ... Transcrito` + metadato `**Fecha:**`) y epistolario (`#### Carta N: Título (Lugar, Fecha)` con texto en citas `>`). Extrae remitente, destinatario, lugar, fecha, año, capítulo (del encabezado `(CAPÍTULO N)`), contexto, fuente, notas de juego y transcripción limpia (sin `*`, `>`, `«»`, respetando párrafos).
  * `Runtime/Data/CollectibleDataSO.cs`: datos textuales + referencias `Texture2D` (facsímil), `GameObject` (modelo 3D) y `AudioClip` (narración para [Escuchar Carta]). `ApplySpec` no toca esas referencias, así que reimportar no pierde lo asignado a mano.
  * `Editor/HistoricalDataGenerator.cs`: menú *Pacífico/Datos/Importar cartas históricas*, incluido en *Generar todo*.
  * Tests: `CollectibleDataTests.cs` (4 unitarios del parser con Markdown sintético y 3 de integración contra los archivos reales) y ampliación de `DataAssetSerializationTests.cs` (reglas de serialización y conservación de datos de `CollectibleDataSO`).
  * Stubs: `Texture`, `Texture2D`, `AudioClip`, `Application.dataPath` y `TextArea(int, int)`.
* **Resultado de la carga:** 4 coleccionables: `carta_grau_carmela_carvajal` (Perú, cap. 1, Pisagua, 2 de junio de 1879, con facsímil del archivo) y `carta_abraham_quiroz_01..03` (Chile, cap. 8; 1879, Dunas de Tacna 1880, Lurín 1881).
* **Verificación:** `bash tools/verify_all.sh` → estructura OK, assets OK, 38/38 tests, compilación Runtime/Editor 0 errores / 0 warnings.
* **Problemas y soluciones:**
  * CS0649 ("campo nunca asignado") en `facsimileTexture`, `facsimileModel` y `narrationClip`, tratado como error fuera de Unity. Unity no lo emite para campos `[SerializeField]` asignados desde el Inspector. Solución: `NoWarn CS0649` en los dos proyectos del arnés, replicando el comportamiento del editor.
  * Un test suponía párrafos separados por línea vacía en la carta 2 de Quiroz; el Markdown real los separa con salto simple dentro de la cita. El parser era fiel al original; se corrigió el test.
* **Notas:** las imágenes de `Archivo_Historico/` están fuera de `Assets/`, por lo que la textura del facsímil se asigna a mano (o copiándola a `Assets/UI/`) tras importar; la ruta de origen queda guardada en `facsimileImagePath`. Las 3 cartas de Quiroz heredan el capítulo 8 del documento. El daguerrotipo de Abraham Quiroz mencionado en el documento no está en el archivo.
* **Pendiente en editor:** ejecutar *Pacífico/Datos/Generar todo* y los tests EditMode en Unity 6.
* **Próximos pasos:** Fase 2, tarea 2.1 (controlador de navegación e inercia hidrodinámica).

---

### [2026-09-28] - Tarea 2.1: Controlador de Navegación e Inercia Hidrodinámica
* **Responsable:** Claude Code (sesión autónoma en la nube)
* **Archivos creados:**
  * `Core/Naval/EngineOrder.cs`: telégrafo Atrás / Detener / 1/4 / Media / Toda fuerza (GDD §3.2; "Atrás" añadido para desengancharse tras un espolonazo en 2.4).
  * `Core/Naval/ShipHandling.cs`: parámetros de maniobra derivados de `ShipSpec`: inercia = 25 s × ∛(desplazamiento/1000 t), respuesta de calderas = 0.2/s ÷ ∛(...), radio de giro mínimo = 3 esloras, creciendo hasta ×2 a toda fuerza; timón de ±35° que va de banda a banda en 2 s.
  * `Core/Naval/ShipMotionModel.cs`: simulación plana determinista. Calderas con retardo → empuje calibrado para que la velocidad terminal sea potencia × velocidad máxima; arrastre cuadrático + lineal residual (al cortar máquinas el buque conserva arrancada y se detiene poco a poco, sin invertir la marcha); guiñada = timón × velocidad / radio (sin arrancada no hay gobierno) con respuesta suavizada; subpasos de 0.02 s para estabilidad.
  * `Runtime/Naval/ShipNavigationController.cs`: W/S por pulsación (telégrafo), A/D mantenido (timón) con el Input System; mueve el buque con `Rigidbody` cinemático (`MovePosition`/`MoveRotation`, interpolado) para detectar colisiones en 2.4; evento `OrderChanged`; API para IA (`SetOrder`, `SetRudderCommand`); HUD de depuración con `OnGUI`.
  * `Runtime/Naval/ShipCameraFollow.cs`: cámara en tercera persona por la popa con seguimiento suavizado.
  * `Editor/NavalPrototypeSceneBuilder.cs`: menú *Pacífico/Escenas/Crear prototipo naval (Iquique)* que genera `Assets/Scenes/Prototipo_Naval_Iquique.unity` con primitivas: mar de 5 × 5 km, *Huáscar* (jugador, casco a escala real + torre Coles) y *Esmeralda* (IA a 1/4), cámara de seguimiento.
  * `Tests/EditMode/ShipMotionModelTests.cs`: 16 casos (telégrafo y límites, velocidad terminal por orden, aceleración gradual, **inercia al cortar máquinas** con decrecimiento monótono y parada final, sin gobierno a velocidad cero, caída a estribor/babor, velocidad de la pala, radio de giro creciente con la velocidad, marcha atrás, mayor inercia del *Cochrane*, *Esmeralda* limitada a 4 nudos, equivalencia paso grande/pequeño, rumbo normalizado, parámetros inválidos).
  * Stubs: `Vector3`, `Quaternion`, `Transform`, `Rigidbody`, `Camera`, `Time`, `GUI`, `Rect`, `PrimitiveType`, `Keyboard`/`KeyControl` (Input System) y `EditorSceneManager`.
* **Verificación:** `bash tools/verify_all.sh` → estructura OK, assets OK, 54/54 tests, compilación Runtime/Editor 0 errores / 0 warnings.
* **Problemas y soluciones:**
  * Con el arrastre lineal residual (necesario para que el buque acabe parándose) la velocidad terminal a toda fuerza quedaba en ~88 % de la máxima. Solución: el empuje incluye el mismo término lineal, de modo que la velocidad terminal es exactamente potencia × máxima.
* **Valores resultantes (*Huáscar*):** inercia ≈ 26 s, ~5 s de calderas hasta plena potencia, radio de giro ≈ 220 m a 1/4 y ≈ 350 m a toda fuerza. Son valores de balance, a ajustar jugando.
* **Pendiente en editor:** en *Project Settings › Player › Active Input Handling* debe estar activado el Input System (Unity lo ofrece al importar el paquete). Ejecutar *Pacífico/Escenas/Crear prototipo naval (Iquique)*, dar Play y comprobar W/S/A/D e inercia en el HUD.
* **Próximos pasos:** Tarea 2.2 (torreta giratoria Coles del *Huáscar*).
