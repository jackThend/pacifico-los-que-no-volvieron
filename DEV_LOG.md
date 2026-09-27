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

---

### [2026-09-27] - Tarea 1.2: Definición de Buques y Blindajes (ShipDataSO) — [X]
* **Archivos creados:**
  * `Core/Naval/ShipSpec.cs`: `ShipSpec` con `ArmorLayout` por zona (cinturón central/extremos, torre o reducto, torre de mando, cubierta, respaldo de teca, chapa), `GunMount`, `TurretSpec` (giro, elevación, sector ciego) y `ShipHandling` (aceleración, pérdida de arrancada, caída del timón). Validación de coherencia (madera sin coraza, cinturón de extremos ≤ central, velocidad de 1879 ≤ diseño, piezas en torre ⇔ `TurretSpec`...).
  * `Core/Naval/ShipCatalog.cs`: *Huáscar*, *Esmeralda*, *Covadonga*, *Independencia*, *Almirante Cochrane* y *Blanco Encalada*.
  * `Runtime/Data/ShipDataSO.cs` y `Editor/HistoricalDataAssetGenerator.Ships.cs`.
  * Pruebas: `Tests/EditMode/Naval/ShipCatalogTests.cs` (12 pruebas) y ida y vuelta de `ShipDataSO` por `JsonUtility` en `Tests/EditModeUnity`.
* **Datos verificados en fuentes:** *Huáscar* 59,4 × 10,6 m, cinturón 114,3/63,5 mm, torre 139,7 mm, torre de mando 76,2 mm, 2 × Armstrong de 300 lb; *Esmeralda* 850 t, 64 × 9,75 m, 201 hombres, 8 nudos de diseño y 2 en el combate; *Covadonga* 412 t, 48,5 m, 2 × 70 lb; *Independencia* 2.004 t, 4,5" sobre 10" de teca, 2 × 150 + 12 × 70; *Cochrane* 3.540 t, cinturón 4,5–9", 6 × 9".
* **Estimaciones declaradas:** calados, dotaciones, velocidades en boca de la artillería naval, respaldos de teca y reducto del *Cochrane* figuran en `Source.EstimatedFields`; una prueba exige que las velocidades de artillería se declaren estimadas.
* **Decisiones de diseño:** la torre Coles se giraba a mano (se usan 6 °/s como licencia jugable); la *Esmeralda* navega a 3 nudos (las fuentes citan 2–3) para que la maniobra sea jugable.
* **Verificación:** `tools/verify/verify.sh` → 5 pasos en verde, 38/38 pruebas OK.

---

### [2026-09-27] - Tarea 1.3: Coleccionables «La Memoria Rota» (CollectibleDataSO) — [X]
* **Archivos creados:**
  * `Core/Narrative/ArchiveMarkdownParser.cs` + `ArchiveDocument.cs`: analizador de los tres formatos del archivo (transcripción con «(Firmado)», cartas numeradas en cita con firma en negrita, despachos con línea de atribución). Extrae metadatos de cabecera (incluidas listas), título y fecha del encabezado, párrafos limpios de Markdown, firma, atribución y notas de diseño; retira solo las comillas « » que envuelven todo el texto.
  * `Core/Narrative/CollectibleRecord.cs` + `CollectibleCatalog.cs`: curaduría de 8 coleccionables (carta de Grau, 3 cartas de Quiroz, 4 despachos) con remitente, destinatario, capítulo (numeración del GDD §5), facsímil y clave de audio. **El texto siempre se lee de los Markdown del archivo; no se duplica en código.**
  * `Runtime/Data/CollectibleDataSO.cs`: texto, dedicatoria del reverso (GDD §4.1), facsímil `Texture2D`, `AudioClip` de [Escuchar Carta] y duración estimada si aún no hay grabación.
  * `Editor/HistoricalDataAssetGenerator.Collectibles.cs`: importa los Markdown y copia el facsímil a `Assets/ArchivoImportado/` (ignorado por git) para usarlo como textura.
  * Pruebas: 10 pruebas sobre los archivos reales del repositorio y casos sintéticos (comillas internas, CRLF, documento vacío, índice fuera de rango) + ida y vuelta de `CollectibleDataSO` en Unity.
* **Incidencia y solución:** en las crónicas la atribución va pegada a la cita sin línea en blanco y el analizador las fundía en un solo párrafo (lo detectó la prueba de despachos). Ahora el paso de texto normal a cita también separa párrafos.
* **Verificación:** `tools/verify/verify.sh` → 5 pasos en verde, 48/48 pruebas OK.
* **Observaciones para el equipo narrativo:**
  * README/ROADMAP y GDD numeran distinto los capítulos (p. ej. Tacna es el 6 en el README y el 5 en el GDD). El catálogo usa el GDD.
  * El texto de `Carta_Miguel_Grau_a_Carmela_Carvajal_1879.md` se presenta como «auténtico», pero es una versión abreviada y parafraseada de la carta publicada. Conviene cotejarlo con la transcripción original antes de grabar la voz en off.

---

### [2026-09-27] - Tarea 2.1: Controlador de Navegación e Inercia Hidrodinámica — [X]
* **Archivos creados:**
  * `Core/Naval/EngineTelegraph.cs`: órdenes Atrás media / Detener / Avante 1/4 / media / toda.
  * `Core/Naval/ShipMotionModel.cs`: modelo plano independiente de la tasa de fotogramas. Respuesta exponencial de la velocidad con constantes distintas para ganar arrancada (ficha `AccelerationTimeSeconds`) y perderla (`CoastDownTimeSeconds`); frenado con máquina atrás; servomotor de timón con tiempo de caída; guiñada con inercia rotacional; gobierno proporcional a la arrancada (sin arrancada no hay giro); pérdida de velocidad con el timón a la banda; ganchos de avería (`PropulsionFactor`, `RudderJammed`).
  * `Runtime/Input/GameInput.cs`: fachada de teclado/ratón compatible con el Input Manager clásico y con el Input System.
  * `Runtime/Naval/ShipController.cs` (Rigidbody cinemático, escora y cabeceo visuales, gizmo del círculo de giro), `ShipCameraRig.cs` (tercera persona, órbita y zoom), `NavalHud.cs` (HUD IMGUI de prototipo).
  * `Tests/EditMode/Naval/ShipMotionModelTests.cs`: 12 pruebas.
* **Verificación:** 60/60 pruebas OK. Casos cubiertos: aceleración gradual al 95 % en el tiempo de la ficha; al cortar máquina conserva > 80 % de la velocidad a los 10 s; radio de giro a toda fuerza = 2 × el de media (proporcional a la velocidad); sin arrancada el timón no gobierna; resultados equivalentes con pasos de 0,01 s y 0,1 s.
* **Incidencia:** `CS0649` en campos `[SerializeField]` (advertencia tratada como error). Unity la suprime para esos campos porque los asigna el Inspector; el arnés ahora hace lo mismo.

---

### [2026-09-27] - Tarea 2.2: Sistema de Torreta Giratoria Coles (Huáscar) — [X]
* **Archivos creados:**
  * `Core/Naval/Ballistics.cs`: tiro parabólico (alcance ↔ elevación de tiro tenso, tiempo de vuelo, rumbo) y cálculo de adelanto sobre blanco en movimiento.
  * `Core/Naval/ColesTurretModel.cs`: giro independiente del casco con perfil trapezoidal (aceleración limitada, se detiene exactamente en la orden), camino angular más corto, topes de elevación de la ficha, sector ciego de popa que impide disparar, convergencia ajustable de las dos piezas, recarga por pieza y dispersión gaussiana con semilla (determinista en pruebas).
  * `Runtime/Naval/ColesTurretController.cs`: apuntado con el ratón sobre el mar, disparo con clic/Espacio, convergencia con R/F, zoom de telémetro con Mayús y retícula de convergencia (caída prevista de cada pieza, dispersión y avisos de SECTOR CIEGO / FUERA DE ALCANCE).
  * `Runtime/Naval/NavalShell.cs`: proyectil balístico con raycast continuo entre pasos (sin túneles a 400 m/s) e interfaz `IShellTarget`; hereda la velocidad del buque.
  * `Tests/EditMode/Naval/ColesTurretTests.cs`: 14 pruebas.
* **Verificación:** 74/74 pruebas OK. La velocidad de giro nunca supera 6 °/s ni sobrepasa la orden; arranque suave; las piezas se cruzan a la distancia de convergencia; el adelanto hace coincidir proyectil y blanco.
* **Correcciones durante la revisión:** se eliminó `??` sobre objetos de Unity (no respeta el «null falso» del motor) y la búsqueda del HUD con `Resources.FindObjectsOfTypeAll` (devuelve también prefabs). Ahora el HUD se registra en `NavalHud.Active`.

---

### [2026-09-27] - Tarea 2.3: Sistema de Blindaje Angular y Balística — [X]
* **Archivos creados:**
  * `Core/Naval/ArmorPenetrationModel.cs`: perforación de hierro forjado con fórmula empírica tipo De Marre, `T = (v·√m / (K·d^0,75))^(1/0,7)`, con K = 1.350 calibrada contra el dato de referencia de un Palliser de 10" y 400 lb (~416 m/s → ~11"). Espesor efectivo = (hierro + teca × 0,1) / cos θ. Ángulo de rebote de 65° (55° si la plancha iguala al calibre; 82° en madera). Pérdida de velocidad exponencial según la densidad seccional. Resultados: rebote, sin perforar, perforación y perforación crítica (madera ≥ 150 mm), con daño estructural y probabilidad de incendio.
  * `Runtime/Naval/ArmoredHull.cs` + `ArmorZoneMarker.cs`: resolución en escena con la normal real de la superficie, la zona del colisionador y la distancia recorrida (nuevo campo `ShellHit.DistanceTravelled`).
  * `Tests/EditMode/Naval/ArmorPenetrationTests.cs`: 15 pruebas.
* **Verificación (criterio del roadmap):** 89/89 pruebas OK.
  * 40 lb de la *Esmeralda* contra el cinturón del *Huáscar*: **nunca perfora** a 200–1.200 m y 0–75°; a 70° **rebota** con daño mínimo.
  * 300 lb del *Huáscar* contra la *Esmeralda*: **perforación crítica** a 0°, 45° y 70°; bastan de 2 a 6 impactos para destruirla.
  * Coherencia histórica adicional: los 250 lb del *Cochrane* perforan cinturón y torre de mando del *Huáscar* a 1.000 m (Angamos); el *Huáscar* no perfora el cinturón central de 9" del *Cochrane*, pero sí sus extremos; presentarse a 60° convierte esa perforación en no-perforación (guion, cap. 2: «posicionar el blindado a 45 grados»).
* **Corrección de robustez:** `NavalShell` buscaba el blanco con `GetComponentInParent<IShellTarget>()?.` (en el Editor puede devolver un «null falso» de Unity). Ahora recorre `GetComponentsInParent<MonoBehaviour>()`.
* **Nota de diseño:** la trayectoria visual no aplica rozamiento (tiro tenso a < 2 km); la pérdida de velocidad solo se aplica al calcular la perforación. Es una simplificación documentada.

---

### [2026-09-27] - Tarea 2.4: Mecánica de Espolonazo y Control de Averías — [X]
* **Archivos creados:**
  * `Core/Naval/RamModel.cs`: energía ½·m·v² con la velocidad de cierre sobre la proa del atacante y el seno del ángulo de cruce; umbral de velocidad crítica (1,5 m/s ≈ 3 nudos) bajo el cual solo hay roce; cascos de madera ×2,5; cuadernas partidas si un solo golpe supera el 15 % del desplazamiento; daño propio mayor sin espolón; pérdida de arrancada del atacante.
  * `Core/Naval/ShipDamageState.cs`: estructura, incendios (crecen, consumen el casco, se apagan solos muy despacio), inundación (vías de agua contra bombas; hundimiento al agotar la flotabilidad de reserva del 30 %), calderas (reducen la potencia) y **una sola brigada** para incendios, achique o vapor (15 s activa + 25 s de descanso). Azar con semilla.
  * `Runtime/Naval/ShipDamageController.cs` (teclas 1/2/3, líneas de HUD, escora por inundación y hundimiento), `RamBow.cs` (trigger en la roda, con rearme por blanco), y en `ShipController` la API `SetSinkPose`, que evita que dos componentes peleen por la rotación.
  * `Tests/EditMode/Naval/RamAndDamageControlTests.cs`: 16 pruebas.
* **Verificación (criterio del roadmap):** 105/105 pruebas OK. El *Huáscar* a 10 nudos y 90° **parte las cuadernas** de la *Esmeralda* (daño > 30 % de su desplazamiento, vía de agua superior a las bombas, hundida en < 3 min sin achique) y recibe < 5 % de daño propio. A 2 nudos o en paralelo solo hay roce. La brigada apaga incendios, tapona vías y repara calderas; no puede hacer dos tareas a la vez.
* **Incidencia de diseño corregida:** la primera versión del hundimiento modificaba `transform.rotation` desde el control de averías, pero `ShipController` la reescribe en cada `FixedUpdate`. Ahora la postura de hundimiento pasa por `ShipController.SetSinkPose`.

---

### [2026-09-27] - Prototipo jugable de la Fase 2: «Rada de Iquique» (greybox)
* **Objetivo:** poder comprobar en el motor, en una sola escena, las cuatro tareas de la Fase 2.
* **Archivos creados:**
  * `Core/Naval/BroadsideBatteryModel.cs` (+ 4 pruebas): batería de costado por bandas con sector de ±40° alrededor del través, recarga independiente por banda y elevación resuelta por distancia.
  * `Runtime/Naval/BroadsideBatteryController.cs` y `BroadsideShipAI.cs`: la *Esmeralda* presenta el través al enemigo, se mantiene cerca de su fondeadero y dispara con adelanto.
  * `Editor/IquiquePrototypeBuilder.cs`: menú «Pacífico/Prototipos/Construir escena naval de Iquique» (también en modo batch). Genera los datos, crea materiales y el prefab del proyectil, y monta el *Huáscar* (torre Coles con cuna y dos piezas, espolón, zonas de blindaje, flotación que abre vías de agua), la *Esmeralda* con IA, el mar, la costa, la niebla, la cámara y el HUD. Guarda `Assets/Scenes/Proto_Iquique.unity` y la añade a Build Settings.
* **Controles:** W/S telégrafo · A/D timón · ratón apunta la torre · clic/Espacio dispara · R/F convergencia · Mayús telémetro · 1/2/3 brigada de averías · botón derecho + ratón orbita la cámara · rueda para el zoom.
* **Verificación:** 109/109 pruebas OK; el constructor compila contra la API de `UnityEditor`. **Pendiente de validar dentro de Unity 6** (no hay Editor en este entorno): ejecutar el constructor y jugar la escena.
* **Revisión:** un `CapsuleCollider` sobre un cilindro achatado degeneraba en una esfera enorme → se usa `BoxCollider`. Se sustituyeron los `??` restantes sobre objetos de Unity (`Shader`, `Texture2D`) por comprobaciones explícitas.

---

### [2026-09-27] - Integración continua y documentación
* `.github/workflows/ci.yml`: en cada push o PR se compila y prueba el C# (`tools/verify/verify.sh`), se ejecutan las pruebas Python y se verifica la integridad SHA-256 del Archivo Histórico.
* `README.md`: nueva sección «Desarrollo en Unity 6» (abrir el proyecto, generar datos, construir el prototipo, controles, arquitectura y verificación), estructura del repositorio actualizada y aclaración sobre licencias de las imágenes modernas.
* **Estado:** Fases 0, 1 y 2 completadas (109 pruebas C# + 17 Python).
* **Próximos pasos:** 1) abrir el proyecto en Unity 6, ejecutar el Test Runner y el constructor de la escena de Iquique para validar en el motor; 2) Fase 3.1, controlador FPS.
