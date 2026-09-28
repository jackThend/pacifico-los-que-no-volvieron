# 🗺️ MAPA DE RUTA DE DESARROLLO (ROADMAP)
## «Pacífico: Los que no volvieron»
> **Guía maestra de ejecución para Agentes Autónomos. Mantener este archivo actualizado en cada ciclo de trabajo.**
> Estados: `[ ]` = Pendiente | `[EN PROCESO]` = En ejecución | `[X]` = Completado y verificado

---

## 📌 FASE 0: CONFIGURACIÓN INICIAL Y ARQUITECTURA
- [X] **0.1 Estructura de Proyecto Unity 6 (URP)**
  * *Criterio:* Crear estructura de carpetas estándar en `src/UnityProject` (`Scripts/`, `Prefabs/`, `Scenes/`, `ScriptableObjects/`, `Audio/`, `Materials/`, `UI/`).
  * *Verificación:* Compilación sin errores y verificación de `.gitignore`.
- [X] **0.2 Pipeline de Manifiesto de Assets y Google Drive**
  * *Criterio:* Crear script `tools/assets_manager.py` y archivo `assets_manifest.json` para gestionar la descarga y sincronización de assets pesados desde Google Drive o fuentes CC0.
  * *Verificación:* Ejecución del script en modo test reportando hashes y estado de sincronización.

---

## 📌 FASE 1: ARQUITECTURA DE DATOS (SCRIPTABLE OBJECTS)
- [X] **1.1 Definición de Armamento Histórico (WeaponDataSO)**
  * *Criterio:* ScriptableObjects con datos balísticos, tiempo de recarga (Comblain: 2.0s, Chassepot: 2.2s, Remington: 2.1s), daño, dispersión y alcance.
  * *Verificación:* Test unitario validando los valores contra las especificaciones del GDD.
- [X] **1.2 Definición de Buques y Blindajes (ShipDataSO)**
  * *Criterio:* Configuración del monitor *Huáscar* (torreta giratoria Coles, blindaje de 4.5"), corbeta *Esmeralda*, fragatas *Cochrane* e *Independencia*.
  * *Verificación:* ScriptableObjects instanciables y serializables sin errores.
- [X] **1.3 Definición de Coleccionables "La Memoria Rota" (CollectibleDataSO)**
  * *Criterio:* Estructura de datos para cartas históricas, remitente, facsímil 3D, texto traducido/transcrito y audio asociado.
  * *Verificación:* Carga de las cartas de Abraham Quiroz y Miguel Grau desde los archivos Markdown existentes.

---

## 📌 FASE 2: MÓDULO NAVAL 3D (PROTOTIPO IQUIQUE)
- [X] **2.1 Controlador de Navegación e Inercia Hidrodinámica**
  * *Criterio:* Simulación de aceleración por telégrafo de calderas (Detener, 1/4, Media, Toda fuerza) y respuesta de timón.
  * *Verificación:* Buque responde a controles de teclado (W/S/A/D) manteniendo inercia al cortar propulsión.
- [ ] **2.2 Sistema de Torreta Giratoria Coles (*Huáscar*)**
  * *Criterio:* Rotación horizontal independiente del casco con retícula de convergencia para los cañones Armstrong de 300 libras.
  * *Verificación:* Apuntado suave con limitadores angulares históricos.
- [ ] **2.3 Sistema de Blindaje Angular y Balística**
  * *Criterio:* Disparos navales calculan ángulo de incidencia. Impactos oblicuos en hierro rebotan; impactos directos o en madera producen daño crítico.
  * *Verificación:* Test de impacto con proyectiles de 40 lbs vs 300 lbs comprobando rebote en el *Huáscar* y perforación en la *Esmeralda*.
- [ ] **2.4 Mecánica de Espolonazo y Control de Averías**
  * *Criterio:* Colisión frontal a velocidad crítica aplica daño masivo de embestida; interfaz de control de averías (fuego, inundación, calderas).
  * *Verificación:* El *Huáscar* puede embestir y partir cuadernas simuladas.

---

## 📌 FASE 3: MÓDULO FPS DE INFANTERÍA (PROTOTIPO PISAGUA)
- [ ] **3.1 Controlador de Primera Persona Inmersivo**
  * *Criterio:* Movimiento ágil, deslizamiento corto táctico, cabeceo de cámara realista y sistema de apuntado con miras de época.
  * *Verificación:* Movimiento fluido a 60 FPS sin jitter ni errores de física.
- [ ] **3.2 Sistema de Fusiles de Época y Recarga Dinámica**
  * *Criterio:* Fusiles monotiro con ciclo de disparo: percutir -> abrir cerrojo -> expulsar vaina -> insertar cartucho -> cerrar -> apuntar (2.0 segundos).
  * *Verificación:* Temporizador y animaciones de recarga sincronizados.
- [ ] **3.3 Efectos Volumétricos de Pólvora Negra**
  * *Criterio:* Humo volumétrico generado en la boca del cañón que se disipa con rapidez cinematográfica para no bloquear la visión del jugador.
  * *Verificación:* Test de estrés con 20 disparos continuos sin saturación visual.
- [ ] **3.4 Combate Cuerpo a Cuerpo (Corvo y Bayoneta)**
  * *Criterio:* Animación de estocada y tajo visceral en primera persona al aproximarse al rango cuerpo a cuerpo.
  * *Verificación:* Detección de colisión cuerpo a cuerpo precisa contra muñecos de prueba (*dummies*).

---

## 📌 FASE 4: MÓDULO RTS TÁCTICO SIN BASES (PROTOTIPO TACNA)
- [ ] **4.1 Selección y Mando de Escuadras**
  * *Criterio:* Selección múltiple por caja de arrastre y órdenes de movimiento/ataque sobre terreno irregular (NavMesh).
  * *Verificación:* Mando coordinado de escuadras de 8 a 12 soldados en formación de línea y guerrilla.
- [ ] **4.2 Sistema de Supresión y Cobertura**
  * *Criterio:* El fuego concentrado disminuye la velocidad de la escuadra enemiga y la obliga a tenderse en zanjas o parapetos.
  * *Verificación:* Comprobación de cambio de estado a *Suprimido* bajo volumen de fuego.
- [ ] **4.3 Gestión de Cantimploras (Agua) y Munición**
  * *Criterio:* Desgaste gradual de reservas de agua y cartuchos en el desierto; reposición mediante carros de vituallas.
  * *Verificación:* Las tropas pierden efectividad si se agota el agua bajo el sol salitrero.

---

## 📌 FASE 5: SISTEMA NARRATIVO Y COLECCIONABLES
- [ ] **5.1 Visor 3D de Documentos Históricos**
  * *Criterio:* Modal interactivo para rotar cartas manuscritas, daguerrotipos y planos con zoom y transcripción textual conmutable.
  * *Verificación:* Inspección funcional del facsímil de la carta de Grau a Carmela Carvajal.
- [ ] **5.2 Gestor de Cinemáticas y Voces de Corresponsales**
  * *Criterio:* Sistema para reproducir las crónicas de Sir George F. Morice y Sir Spenser St. John con subtítulos en español sincronizados.
  * *Verificación:* Reproducción fluida del prólogo *El Ojo de Europa*.

---

## 📌 FASE 6: INTEGRACIÓN DE CAMPAÑA Y PULIDO
- [ ] **6.1 Escenario Capítulo 1: Rada de Iquique (Naval 3D)**
- [ ] **6.2 Escenario Capítulo 4: Desembarco de Pisagua (FPS)**
- [ ] **6.3 Escenario Capítulo 6: Alto de la Alianza / Tacna (RTS)**
- [ ] **6.4 Escenario Capítulo 7: Morro de Arica (RTS + Asalto)**
- [ ] **6.5 Escenario Capítulo 8: Reductos de Miraflores (FPS / Clímax)**
- [ ] **6.6 Pruebas Integrales de Rendimiento y Empaquetado Final**
