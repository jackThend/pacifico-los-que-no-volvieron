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

---

### [2026-09-27] - Revisión de código independiente y correcciones
Se revisó toda la rama (`d0f859d..HEAD`) con una revisión automática de alto nivel de exigencia. Se aceptaron y corrigieron los 10 hallazgos:
1. **Incendios sin cota:** con dos focos la intensidad crecía exponencialmente y la brigada dejaba de dominarla. Ahora el crecimiento es logístico, con tope `MaxFireIntensity = 2`; solo los rescoldos (< ~0,23) se apagan solos y la brigada siempre supera al crecimiento máximo. Pruebas: fuego acotado tras 30 min, la brigada apaga un fuego al máximo y un rescoldo se extingue solo.
2. **El atacante atravesaba al blanco tras el espolonazo** (Rigidbody cinemáticos sin respuesta de colisión): una embestida crítica ahora hace retroceder al atacante (`RammerSpeedRetained = -0,15`) y `RamBow.OnTriggerStay` anula cualquier avance mientras la roda siga dentro de otro casco.
3. **La cubierta no oponía resistencia** (`DeckMm = 0` en todo el catálogo): ahora opone al menos la chapa del casco de hierro, o la tablazón en madera. Prueba: un 12 lb rasante no atraviesa la cubierta del *Huáscar*.
4. **«Cuadernas partidas» y el timón trabado no tenían efecto:** con las cuadernas partidas la brigada tapona al 20 % de su eficacia; las perforaciones en los extremos pueden trabar el servomotor (10 %), que la brigada de vapor libera en 5 s, y `ShipDamageController` lo traslada a `ShipMotionModel.RudderJammed`.
5. **Túnel de proyectiles:** si el primer colisionador del segmento era el del propio buque se perdía el blanco que hubiera detrás. Ahora se usa `RaycastNonAlloc` y se toma el impacto ajeno más cercano.
6. **Estado `[X]` sin validación en el motor:** nueva tarea **0.3 Validación en el Editor de Unity 6** en el ROADMAP, pendiente y explícita.
7. **`sync` sin hash previo aceptaba cualquier respuesta:** ahora rechaza páginas HTML y avisa cuando registra la huella de la primera descarga. 2 pruebas nuevas.
8. **Facsímil comparado solo por tamaño:** ahora compara tamaño y SHA-256.
9. **Constante mágica `1.944f`** sustituida por `Units.MetersPerSecondToKnots`.
10. **Lógica de disparo duplicada y `LateralOffsetM` sin uso en las baterías:** nuevo `ShellLauncher` compartido por torre y batería; `ShellLaunch.Side` indica la banda.
* **Verificación:** 115/115 pruebas C# y 19/19 Python; manifiesto 48/48 OK.

---

### [2026-09-27] - Tarea 3.1: Controlador de Primera Persona Inmersivo — [X] (verificado fuera del motor; ver 0.3)
* **Núcleo (`Pacifico.Core`, sin motor):**
  * `Infantry/InfantryMotor.cs` + `InfantryMovementSettings.cs`: caminar, correr, agacharse, apuntar (más lento), retroceder y desplazarse de lado (más lento); aceleración lineal hacia la velocidad deseada sin sobreimpulso; salto con integración de Verlet (altura exacta a cualquier tasa); **deslizamiento táctico** (solo desde la carrera, 0,9 s máximo, rozamiento, leve gobierno, enfriamiento y coste de resistencia); resistencia con agotamiento y umbral de recuperación; transición gradual de altura entre posturas; techo bajo que impide levantarse; aterrizajes filtrados por tiempo en el aire (el `isGrounded` de Unity parpadea en pendientes).
  * `Infantry/HeadBobModel.cs`: **cabeceo realista** ligado a la distancia recorrida, con paso que se alarga con la velocidad (≈2,8 pasos/s caminando y 3,6 corriendo); un valle vertical por paso y un vaivén lateral y de alabeo por zancada; se atenúa al apuntar y al frenar; golpe de aterrizaje con muelle amortiguado en subpasos fijos.
  * `Weapons/SmallArmsBallistics.cs`: balística exterior con arrastre cuadrático y **Cd dependiente del Mach** (RK2). Calibración: el Gras pasa de 450 a 430 m/s en 25 m (dato histórico → Cd ≈ 0,8 supersónico); Cd subsónico 0,20, ajustado para que el alcance máximo del Gras (~3.070 m) se logre a ~35°, como en los ensayos de Versalles (25–35°).
  * `Weapons/SightLadder.cs`: **alza graduada de época**, cada 100 m hasta el alza de la ficha (Comblain 1.300, Chassepot 1.200, Gras 1.800 m); elevación de cada graduación por bisección balística, en caché por arma.
  * `Weapons/IronSightModel.cs`: encare cuya duración depende del peso del fusil, acercamiento leve (1,25×) y deriva de la respiración en «ocho», que crece con la fatiga y el movimiento y mengua agachado.
  * `WeaponSpec.BulletMassG` (Comblain 24,7 g, Chassepot y Gras 25 g, Remington 25,7 g, Winchester 13 g a 379 m/s, dato ahora con fuente); `MathUtil.SmoothDamp` y `Vec3`.
* **Unity:** `Infantry/FirstPersonController.cs` (CharacterController en `Update`, sin desfase física/fotograma, que es la causa típica del *jitter*; cursor bloqueado; alza con rueda acumulada por muesca), `IronSightViewModel.cs` (cadera ↔ encare, carrera, deslizamiento, retraso del fusil y **alza de escalera que sube R·tan θ**, con el fusil inclinado θ al encarar), `InfantryHud.cs` (resistencia, postura, alza y contador de FPS). En `GameInput`: C, Ctrl, RePág, AvPág y `MousePosition`.
* **Editor:** `PisaguaPrototypeBuilder.cs`, con el menú **Pacífico → Prototipos → Construir escena FPS de Pisagua**: rampas de 20/35/50°, escalera, túnel de 1,35 m, parapetos y tapias, obstáculo de salto, línea de tiro con siluetas cada 100 m hasta 1.000 m y un Comblain esquemático con alza móvil. Nuevo `PrototypeSceneKit.cs`, compartido con el constructor de Iquique (se eliminó la duplicación de materiales, primitivas y Build Settings).
* **Pruebas:** 41 nuevas (156 en total), entre ellas trayectoria equivalente a 30, 60 y 144 FPS (< 8 cm tras 15 m), pasos irregulares sin oscilación, salto a la misma altura a cualquier tasa, cabeceo y deriva sin discontinuidades (segunda diferencia acotada al arrancar y frenar), la calibración del Gras y la caché del alza.
* **Incidencias resueltas durante el desarrollo:**
  * El primer Cd subsónico (0,35) daba al Gras un alcance máximo de 2.212 m, inverosímil con alza hasta 1.800 m. Se recalibró con un script de comprobación independiente.
  * El límite de 12 s de vuelo truncaba las trayectorias de elevación alta → 40 s.
  * El ritmo respiratorio multiplicado por el tiempo total provocaba saltos al cambiar la fatiga → fase acumulada y `SmoothDamp` de segundo orden.
  * La cadencia de pasos a la carrera era irreal (6,9 pasos/s) → paso proporcional a la velocidad.
  * Constructor de escena: la altura de los peldaños era incorrecta y los carteles quedaban de espaldas.
* **Revisión de código independiente (10 hallazgos, todos corregidos):** adherencia al suelo insuficiente cuesta abajo (despegues intermitentes); caída que heredaba el empuje de pegado al salir de un borde; deslizamiento que se reorientaba contra la pared; salto perdido al saltar desde un deslizamiento con C pulsada; cabeceo de pasos durante el deslizamiento; retraso del arma dependiente de la tasa de fotogramas; alza recorrida entera por un trackpad; `SightLadder` recalculada en cada cambio de arma (caché); `OverlapCapsule` que generaba basura cada fotograma (→ `NonAlloc`); y la hoja del alza, que se movía al revés.
* **Pendiente (tarea 0.3):** jugar `Proto_Pisagua_FPS` en Unity 6 y comprobar con el contador del HUD que el movimiento es fluido a 60 FPS.

---

### [2026-09-27] - Tarea 3.2: Sistema de Fusiles de Época y Recarga Dinámica — [X] (verificado fuera del motor; ver 0.3)
* **Datos:** `WeaponSpec.PowderChargeG` y `WeaponSpec.Case` (metálico o combustible). Cargas con fuente: Chassepot 5,6 g (cartucho de papel), Gras 5,2 g, Comblain 76 gr (≈4,9 g), .43 Spanish 77 gr (≈5,0 g) y .44-40 40 gr (≈2,6 g).
* **Núcleo (`Pacifico.Core/Weapons`):**
  * `RifleCycleProfile`: secuencia por mecanismo real. Comblain y Gras: percutir → abrir → expulsar → insertar → cerrar → encarar. Remington: amartilla a mano antes de abrir el bloque. **Chassepot: amartilla a mano y no expulsa vaina** (cartucho combustible). Winchester: ciclo de palanca (bajar = extrae y expulsa; subir = alimenta desde el depósito) y carga del depósito cartucho a cartucho. Las etapas de los monotiro suman **exactamente** la `ReloadSeconds` de la ficha (Comblain 2,0 s).
  * `RifleCycleModel`: máquina de estados con munición (recámara, depósito, cartucheras), vaina en la recámara, martillo montado, recarga automática o manual (R), pausa al correr o deslizarse, bloqueo del encare solo mientras se manipula el fusil (no en el ciclo de palanca), sin munición → «clic», interrupción de la carga del depósito al disparar. Eventos con desfase dentro del paso: si un fotograma largo abarca varias etapas, se emiten todos, en orden y en su instante exacto.
  * `RifleAnimationCurves`: apertura del cierre, martillo y retroceso visible derivados solo de la etapa y su progreso, sincronizados por construcción.
  * `RecoilModel`: retroceso libre por conservación del momento, `(m_bala·v + m_pólvora·1.200 m/s) / M_fusil` (Gras: 4,16 m/s), con un muelle críticamente amortiguado avanzado por su solución exacta en forma cerrada.
  * `RifleShotSolver`: elevación del alza + dispersión (encarado: el grupo de la ficha como ≈4σ; a la cadera, en movimiento o a medio encare, mucho mayor).
  * `SmallArmsBallistics.StepBullet`: integrador 3D con el mismo RK2 que el alza.
* **Unity (`Runtime/Infantry`):** `RifleController` (gatillo, R, cartucheras por calibre al cambiar de arma, `DefaultExecutionOrder(-50)`), `RifleBullet` (barrido por raycast, velocidad en el punto de impacto, `IBulletTarget`), `RifleViewModelAnimator` (palanca, martillo, cartucho visible, vaina expulsada en la capa «Ignore Raycast»; publica `Etapa` y `VelocidadEtapa` para un `Animator` futuro), `ShootingTarget`, `AmmoPickup` y HUD con cartuchos, etapa y último impacto. `FirstPersonController`: retroceso en cámara, `AimBlocked` y `HasInputFocus`.
* **Escena `Proto_Pisagua_FPS`:** Comblain con palanca articulada, cartucho y ventana de expulsión; siluetas con `ShootingTarget` (el HUD indica distancia y desviación del impacto: «400 m · 32 cm bajo»); 20 cartuchos iniciales y una caja con 40 junto a la línea de fuego.
* **Verificación (criterio del roadmap, «temporizador y animaciones sincronizados»):** 36 pruebas nuevas (192 en total).
  * El ciclo dura exactamente la recarga de la ficha: 2,0/2,2/2,2/2,1 s.
  * La línea temporal de eventos es idéntica con pasos de 1/240, 1/144, 1/60, 1/30 y 0,25 s (±0,1 ms).
  * Cada etapa empieza cuando termina la anterior, y la vaina sale y el cartucho entra en el instante de fin de su etapa.
  * Las curvas de animación no saltan entre etapas; el martillo tras el último disparo queda abatido y se monta desde abajo.
  * Retroceso con pico exacto y a 30 = 144 FPS; la bala del juego (a 50 Hz) cruza la línea de mira a ±3 cm de la distancia graduada a 200, 500 y 1.000 m.
* **Incidencias resueltas durante el desarrollo:**
  * El muelle del retroceso integrado con Euler semi-implícito daba un pico un 9 % bajo → solución exacta.
  * El martillo del Comblain saltaba de abatido a montado → en los cierres que se amartillan solos, se monta al abrir.
* **Revisión de código independiente (10 hallazgos, todos corregidos):** cambiar de arma perdía o regalaba munición (→ cartucheras por calibre; el arma nueva se empuña descargada y se carga); sin captura del ratón no se podía disparar (→ `HasInputFocus`); se podía disparar en pausa; la palanca de la Winchester bajaba las miras en cada disparo (→ `BlocksAiming` en el modelo); cambio de arma con el componente desactivado; orden de ejecución entre fusil y controlador; martillo tras el último disparo; `FreeRecoilEnergy(null)`; velocidad de impacto al final del paso; `foreach` sobre `IReadOnlyList` que generaba basura cada fotograma. Al corregir el orden de ejecución apareció un fallo nuevo (el fusil se suscribía antes del equipamiento inicial y perdía la dotación), que también quedó resuelto.

---

### [2026-09-27] - Tarea 3.3: Efectos Volumétricos de Pólvora Negra — [X] (verificado fuera del motor; ver 0.3)
* **Física (`Pacifico.Core/Effects/BlackPowderSmokeModel.cs`):** cada disparo emite una bocanada gaussiana cuya masa es la **fracción sólida de la carga** (≈56 % de los productos de combustión de la pólvora negra son sales de potasio en suspensión: el humo blanco; fuente en `BlackPowderSmokeSettings.Source`). Comblain: 4,9 g de pólvora → 2,7 g de humo.
  * El chorro sale a ~14 m/s y el aire lo frena con τ = 0,08 s (se detiene a ~1,1 m de la boca); la nube se abre hasta ~1 m de diámetro, sube por flotación, se difunde y la arrastra el viento. Movimiento integrado con la **solución exponencial exacta**: igual a 30 y a 144 FPS.
  * Profundidad óptica por el centro τ = κ·M/(π r²); con ella se mide, rayo a rayo, cuánto tapa la vista (`Transmittance`, `ViewClarity` en un cono de ±12°), integrando solo la parte de cada nube que queda delante del ojo (función error).
  * **Licencia cinematográfica documentada:** el humo real de 1879 tardaba muchos segundos en abrirse; aquí se desvanece con τ = 0,9 s, y las bocanadas pegadas a la cámara se atenúan (0,6–2,4 m), como el *near fade* de los sistemas de partículas.
  * Presupuesto de 64 bocanadas (se retira la más antigua); determinista con semilla.
* **Unity:** `Runtime/Effects/BlackPowderSmoke.cs` dibuja exactamente esas nubes con un único `ParticleSystem` (4 láminas por bocanada, opacidad derivada de τ para que K láminas sumen 1 − e^{−τ}, textura gaussiana con ruido fractal generada al vuelo, tono más oscuro al salir y blanco grisáceo después) y un fogonazo de luz de 35 ms. `RifleController` emite desde la nueva `Boca` del fusil con la velocidad del tirador. `SmokeVolleyTest` (tecla **V**): descarga de 20 fusiles a ambos lados del jugador y claridad de la vista en pantalla, para juzgar en el editor lo mismo que miden los tests.
* **Escena `Proto_Pisagua_FPS`:** humo con brisa de través (1,8 m/s), material guardado como asset (`Proto_Humo.mat`) para que la build incluya el sombreador.
* **Verificación (criterio del roadmap, «20 disparos continuos sin saturación visual»):** 19 pruebas nuevas (211 en total). Prueba de estrés con el ciclo real de cada arma a su máxima cadencia, encarado y a la cadera, **sin viento** (el peor caso):
  * El humo se ve: el cono central baja a 23–48 % de claridad justo después de cada disparo.
  * No ciega: el centro queda tapado (< 50 %) como mucho 0,43 s por disparo; al volver a encarar hay un 96–98 % de claridad con los monotiro y ~70 % con la Winchester (un tiro cada 0,45 s).
  * No se acumula: los últimos 10 disparos se ven igual que los primeros; a los 3 s del último, vista limpia (> 99 %).
  * Descarga de 20 fusiles junto al jugador: cortina de humo (mínimo ~70 % de claridad) que se abre en 2 s.
* **Incidencias resueltas:** el banco de pruebas se quedaba en bucle porque el modelo del fusil no recargaba solo (`AutoReload` desactivado por defecto) → la prueba de estrés recarga como el jugador; `pkill -f` volvió a matar la propia shell (ya anotado). En la revisión: láminas por bocanada cambiadas en el inspector con el juego en marcha desbordaban el búfer (→ fijadas en `Awake`), radio nulo con parámetros extremos (→ mínimo de 1 mm) y tamaño negativo con variación ≥ 100 %.
* **Pendiente (tarea 0.3):** comprobar en Unity 6 el aspecto del humo (URP `Particles/Unlit` transparente) y el coste con 64 bocanadas.

---

### [2026-09-27] - Tarea 3.4: Combate Cuerpo a Cuerpo (Corvo y Bayoneta) — [X] (verificado fuera del motor; ver 0.3)
* **Núcleo (`Pacifico.Core/Melee`):**
  * `MeleeAttackProfile`: estocada con bayoneta calada (Comblain, Chassepot, Gras, Remington: alcance 1,90 m), estocada y tajo con el corvo (1,10 m). La trayectoria de la hoja (empuñadura y punta en coordenadas de vista) depende de un único parámetro de carrera σ (−1 armado, 0 guardia, +1 extensión o fin del arco). La estocada acaba en el centro de la mira exactamente al alcance del arma; el tajo es un arco diagonal de 180° alrededor del hombro derecho (de arriba a la derecha a abajo a la izquierda), con el radio de la punta resuelto por bisección para que su distancia máxima al ojo sea el alcance. Tiempos: 30 % preparación, 20 % golpe, 50 % recuperación de `MeleeCycleSeconds` (bayoneta 0,9 s, corvo 0,55 s).
  * `MeleeAttackModel`: recorre la trayectoria en **pasos fijos de 1/480 s** alineados con el inicio del golpe y comprueba, en cada paso, el segmento empuñadura–punta y el recorrido de la punta contra las cápsulas de los objetivos (Ericson, segmento–segmento). Solo hiere la fase de golpe; cada objetivo una vez por golpe; la estocada se queda en el primer cuerpo y se retira desde allí; el tajo alcanza dos como máximo; congelación de 70 ms al impactar («hit-stop»). La cámara se interpola entre fotogramas, así que girar durante el tajo cambia dónde corta.
  * `MeleeProximity`: distancia al objetivo más cercano en un cono de ±35° y peso de la guardia (sube desde alcance + 1,5 m).
  * `RecoilModel.Punch`: sacudida de la vista con el mismo muelle que el retroceso.
* **Unity (`Runtime/Infantry`):** `MeleeController` (**F** estocada, **G** tajo con el corvo que el soldado lleva al cinto; convierte los colisionadores de cápsula, esfera y caja, con escala y giro, en la forma exacta del contacto; descarta lo que no está a la vista, de modo que una tapia detiene la hoja; bloquea disparo y encare y detiene la recarga mientras dura el golpe), `MeleeViewModelAnimator` (resuelve la pose del fusil para que la bayoneta visible coincida con la hoja del modelo, y mueve el corvo por el mismo arco: lo que se ve es lo que golpea), `MeleeDummy` (muñeco de paja con torso y cabeza ×1,5 que se balancea con un muelle amortiguado a pasos fijos y también recibe balas), `MeleeImpactEffect` (paja que salta en la dirección del golpe) y `IMeleeTarget`. HUD: «EN GUARDIA [F] estocada · [G] tajo» y el último golpe («Estocada · cabeza · 135 · 4 cm»).
* **Escena `Proto_Pisagua_FPS`:** bayoneta de 0,5 m en el Comblain (nodo «Esgrima» entre la raíz y el mecanismo), corvo en primera persona y **patio de esgrima** tras la línea de fuego: tres muñecos, un poste de 8 cm y un muñeco tras una tapia baja.
* **Verificación (criterio del roadmap, «colisión cuerpo a cuerpo precisa contra muñecos»):** 31 pruebas nuevas (242 en total), más 2 de conversión de colisionadores para el Test Runner de Unity.
  * **Frontera del alcance al centímetro:** la bayoneta alcanza un poste de 20 cm de radio a 2,095 m y no a 2,105 m; el impacto cae en la superficie (±2 mm) y solo dentro de la fase de golpe.
  * Estocada solo a lo que está delante (±10 cm sí, 35–50 cm no); cabeza o pecho según adónde se mire, con el multiplicador de la cabeza.
  * El tajo alcanza de lado lo que la estocada no, se detiene en el segundo cuerpo, no alcanza lo que el soldado tiene detrás ni lo que está fuera de su alcance; demasiado cerca para la bayoneta, pero no para el corvo.
  * **Mismo impacto (instante y punto) a 20, 37, 60 y 144 FPS**; el tajo (punta a más de 15 m/s) no atraviesa un poste de 3 cm ni a 20 FPS.
  * Un solo impacto por muñeco y golpe; el ciclo dura lo que dice la ficha y el impacto añade exactamente la congelación.
* **Incidencias resueltas:** el primer paso de fase se evaluaba antes de la pose final, así que la extensión completa de la estocada nunca se comprobaba (se cambió el orden); la estocada del corvo pasaba a 2 mm de un cuerpo a 45 cm porque su guardia estaba demasiado adelantada; la guardia de la bayoneta a 1,25 m no coincidía con el fusil visible (punta a ~1,7 m) → 1,55 m; el diagrama de trayectorias mostró que el tajo empezaba a la espalda del soldado (110°) → 100°, con prueba de que no hiere detrás. En la revisión: perfiles recalculados en cada fotograma si el arma no tenía cuerpo a cuerpo, y aviso de guardia sin ningún golpe disponible.
* **Pendiente (tarea 0.3):** comprobar en Unity 6 que la pose de guardia y el corvo se ven bien en pantalla, y la sensación del «hit-stop».

---

### [2026-09-27] - Tarea 4.1: Selección y Mando de Escuadras — [X] (verificado fuera del motor; ver 0.3)
* **Núcleo (`Pacifico.Core/Tactics`):**
  * `Formation`: **línea** (hombro con hombro a 1 m; con más de 8 hombres, en dos filas a 1,2 m) y **guerrilla** (orden abierto a 5 m, hombres alternos retrasados 1,5 m).
  * `SlotAssignment`: método húngaro (Kuhn–Munkres) sobre distancias. La suma mínima garantiza que dos trayectorias rectas no se cruzan, así que nadie atraviesa la formación al cambiarla.
  * `SquadMarch` / `SquadCommand`: el ancla de la escuadra marcha por las esquinas del camino del NavMesh a 1,4 m/s y frena si se quedan rezagados (cohesión). La formación gira como un bloque con la velocidad angular que pueden seguir los extremos; si esa rueda tardara más de 4 s (media vuelta, o la guerrilla con 55 m de frente), cada hombre va directo a su nuevo puesto con los puestos reasignados. En los últimos 8 m gira hacia la orientación ordenada. Las bajas hacen cerrar filas. Cada soldado persigue su puesto con la velocidad de marcha más una corrección proporcional al retraso, hasta la carrera (3,2 m/s).
  * `OrderPlanner`: órdenes a varias escuadras. Con un clic forman codo con codo mirando hacia donde avanzan; arrastrando, el trazo marca el frente (de izquierda a derecha, mirando hacia delante) y se reparten a lo largo de él con al menos el intervalo entre escuadras, asignadas sin cruzarse.
  * `SelectionLogic`: recuadro normalizado, clic (menos de 6 px) sobre el hombre más cercano, modos sustituir / añadir (Mayús) / alternar (Ctrl). La unidad de mando es la escuadra.
  * `FireModel` / `SquadFireControl` (para la orden de ataque): probabilidad de impacto erf(w/2√2σ)·erf(h/2√2σ) contra la silueta según la postura, con la dispersión de la ficha ×3 por el estrés más 0,25° de error fijo (Remington de pie: ~29 % a 100 m, ~10 % a 200 m, ~3 % a 400 m). Fuego a discreción: cada hombre recarga (tiempo de la ficha), apunta 1,5–2,5 s y dispara. Un impacto deja fuera de combate con probabilidad daño/100.
* **Unity (`Runtime/Tactics`):** `NavMeshRuntimeBaker` genera el NavMesh al cargar la escena con `NavMeshBuilder` (la API del motor que usa NavMeshSurface), sin depender de un horneado en el Editor ni de un paquete; tipo de agente propio de 0,3 m de radio y vóxel de 20 cm. `SquadController` crea los soldados (primitivas), los lleva con `NavMeshAgent` a los puestos que marca `SquadCommand` y ejecuta las órdenes de mover, atacar (se acerca hasta el 80 % de su alcance eficaz, se detiene, encara y rompe el fuego; sin línea de visión, se acerca más) y alto; parada, responde al enemigo que tenga a tiro. Humo de pólvora negra en los disparos. `RtsCameraController` (WASD, Q/E, rueda; inclinación según la altura, sin atravesar dunas), `RtsCommander` (selección, órdenes, previsualización de los puestos finales al arrastrar, panel de escuadras) y `SoldierUnit`. `GameInput.MouseReleased`.
* **Escena `Proto_Tacna_RTS`** (menú **Pacífico → Prototipos → Construir escena RTS de Tacna**): terreno de 500 × 500 m con la meseta del Intiorko (+14 m), dunas de dos escalas, zanja de 1,3 m y parapetos de sacos en el borde; camanchaca. Tres escuadras del Batallón Colorados (jugador, Remington; 12, 12 y 8 hombres) y tres chilenas (Comblain) a 355 m, justo fuera del alcance eficaz de ambos.
* **Verificación (criterio del roadmap, «mando coordinado de escuadras de 8 a 12 soldados en formación de línea y guerrilla»):** 31 pruebas nuevas (273 en total).
  * Línea y guerrilla con 8 y 12 hombres, desbandados al empezar: marchan 100 m con una esquina de 90°, llegan con el ancla a < 1 cm del destino, cada hombre a < 5 cm de su puesto, con la orientación ordenada, y sin rezagados de más de 1,5 m en marcha una vez formados.
  * Tres escuadras (12 en línea, 10 en guerrilla, 8 en línea) despliegan a la vez sobre un trazo: cada una en su tramo y con al menos 2 m entre hombres de escuadras distintas.
  * Media vuelta sin cruces; la guerrilla cambia de frente en segundos; bajas que cierran filas; paso de línea a guerrilla.
  * Húngaro óptimo frente a la fuerza bruta (720 permutaciones) y sin trayectorias cruzadas con 12 hombres.
  * Fuego: probabilidades por distancia y postura, cadencia y aciertos dentro de 4σ, esperar cargados, bajas que reducen el volumen de fuego.
* **Incidencias resueltas:** la guerrilla, con 55 m de frente, tardaba ~24 s en girar 90° en cada esquina y se detenía (→ cambio de frente directo a los puestos reasignados si la rueda pasa de 4 s); la métrica de cohesión medía también el desbandado inicial (→ se mide una vez formados); una escuadra que llegaba a su destino se quedaba en «Mover» y no respondía al fuego (→ pasa a «Alto»). El diagrama de la marcha mostró que en guerrilla los hombres de la esquina salían del gráfico: el comportamiento era correcto, se amplió el gráfico.
* **Pendiente (tarea 0.3):** comprobar en Unity 6 el tiempo de generación del NavMesh (500 × 500 m) y el comportamiento de los NavMeshAgent en las dunas.

---

### [2026-09-27] - Tarea 4.2: Sistema de Supresión y Cobertura — [X] (verificado fuera del motor; ver 0.3)
* **Núcleo (`Pacifico.Core/Tactics/Suppression.cs`):**
  * `SuppressionModel`: cada bala que llega a la escuadra, acierte o no, suma presión (0,035 × un factor de proximidad del tirador: ×1,2 a quemarropa, ×0,4 al doble del alcance eficaz); cada impacto suma 0,04 y cada baja 0,12. La presión se disipa con τ = 6 s. Con fuego constante el nivel tiende a S* = tasa·Δ·τ: **una escuadra a 200 m presiona (≈0,6) pero no suprime; dos escuadras concentradas suprimen (≈1,2)**. Estados con histéresis: Presionada (entra a 0,35, sale a 0,22) y **Suprimida** (entra a 0,70, sale a 0,50).
  * Efectos: velocidad de marcha ×0,6 presionada y ×0,25 suprimida; postura rodilla en tierra o cuerpo a tierra (la silueta baja de 1,7 a 1,1 o 0,35 m en el modelo de impacto); dispersión del propio fuego ×1,4 o ×2,5 y cadencia ×0,8 o ×0,4.
  * Cobertura: la presión recibida se atenúa hasta ×0,35 con cobertura total. Calibrado para que **una escuadra en la zanja aguante sin suprimirse el fuego de dos escuadras y haga falta concentrar tres** (o flanquearla), como pide el GDD.
  * `CoverSpot` / `CoverSelector`: puestos a cubierto con orientación y protección (zanja 0,8; parapeto 0,65; peñasco 0,55), que solo protegen del fuego que llega de frente. El reparto entre los hombres de la escuadra usa el método húngaro (carrera mínima y, a igualdad, el mejor puesto) sobre los puestos libres a menos de 25 m; a quien no le toca, se tiende donde está.
* **Unity:** `SquadController` integra la supresión (la alimenta el fuego enemigo, también cuando falla), la velocidad, la postura, la puntería y la cadencia. **Parada y amenazada, o suprimida, ocupa los puestos a cubierto** (`CoverPoint`, reservados por escuadra) y dispara rodilla en tierra desde el parapeto. Suprimida, detiene el asalto y lo reanuda cuando afloja el fuego. La línea de visión usa la postura del blanco, así que un enemigo tendido tras el parapeto no se ve. `SoldierUnit` pasa de pie, de rodillas o tendido en 0,3 s. `SquadAI` hace que los chilenos ataquen a la escuadra más cercana tras 20 s. HUD: estado, barra de supresión, hombres a cubierto y «¡Suprimida!» sobre las escuadras.
* **Escena `Proto_Tacna_RTS`:** puestos a cubierto cada metro al pie de los parapetos y cada 1,2 m en la zanja, y cinco peñascos en la tierra de nadie con puestos a ambos lados.
* **Verificación (criterio del roadmap, «cambio de estado a Suprimido bajo volumen de fuego»):** 16 pruebas nuevas (289 en total).
  * El fuego concentrado de dos escuadras a 200 m pasa a Suprimida en menos de 15 s, tras pasar por Presionada, y sigue suprimida mientras dura el fuego; una sola escuadra la deja presionada de forma estable.
  * **Duelo completo con impactos y bajas:** una escuadra que avanza a descubierto bajo el fuego de dos a 250 m queda suprimida, se tiende y avanza menos de la mitad que sin fuego.
  * Atrincherada aguanta dos escuadras pero no tres; recuperación por etapas en los tiempos exactos (τ·ln(S/umbral)); sin parpadeo en el umbral; las bajas pesan más que las balas; suprimida dispara el 40 %.
  * Cobertura: cada hombre al puesto que tiene delante; un parapeto no protege del fuego por detrás ni de lado; más hombres que puestos; puestos lejanos u ocupados; a igual distancia, el mejor puesto.
* **Incidencias resueltas:** la gráfica de la supresión mostró que con la primera calibración (atenuación ×0,55) una escuadra en la zanja quedaba suprimida por solo dos escuadras, contra lo que se pretendía y lo que decía el propio texto de la gráfica; se recalibró a ×0,35 y se añadió la prueba correspondiente.
* **Pendiente (tarea 0.3):** comprobar en Unity 6 el reparto de puestos en la zanja con varias escuadras y el ritmo del asalto chileno.
