# PACÍFICO: LOS QUE NO VOLVIERON
## Documento de Diseño Narrativo y de Misiones (GDD)
**Versión:** 1.0  
**Motor Técnico:** Unity (Universal Render Pipeline - URP / Unity 6)  
**Género:** Híbrido Bélico-Narrativo (FPS Ágil + Combate Naval 3D Dinámico + RTS Táctico sin Bases)  
**Tono:** Realismo histórico, crudo, humanista y antibelicista. *"El enemigo es humano"*.

---

## 1. Visión y Pilares del Proyecto

### 1.1 Premisa
*Pacífico: Los que no volvieron* relata los episodios capitales de la Guerra del Pacífico (1879–1884) a través de los ojos, cartas y vivencias de los soldados rasos, marineros, conscriptos indígenas y trabajadores olvidados de ambos bandos (Chile y la Alianza Perú-Bolivia). 

No es una obra de propaganda nacionalista de ningún bando, sino una crónica interactiva sobre el costo humano de la guerra en el desierto y el mar, donde el honor de figuras históricas como Grau, Prat y Bolognesi convive con la tragedia cotidiana de los jóvenes que no regresaron a sus hogares.

### 1.2 Los Cuatro Pilares Fundamentales

1. **La Verdad en los Bolsillos (Narrativa de Trinchera):**
   * El jugador no encuentra botines genéricos. Al revisar campamentos o caídos de cualquier bando, descubre **cartas manuscritas, daguerrotipos y recuerdos familiares reales** extraídos de los archivos históricos nacionales.
   * La guerra se desmitifica: el enemigo no es un blanco poligonil, sino un muchacho con madre, oficio y temores.
2. **El Testigo Neutral (Crónicas de Ultramar):**
   * Cinemáticas intermedias narradas por corresponsales y agregados militares europeos y norteamericanos (inspirados en George F. Morice de *The Times* de Londres y Spencer St. John).
   * La narrativa internacional evoluciona: de la soberbia colonialista ("esperábamos hordas salvajes") al asombro absoluto ante la disciplina prusiana, el estoicismo y el valor de ambos ejércitos.
3. **Jugabilidad Moderna y Enganchante (Licencias Artísticas Funcionales):**
   * El armamento y los buques son 100% fieles a 1879 en apariencia, balística y sonido, pero la jugabilidad es ágil: recargas de fusil reducidas a tiempos dinámicos (~2 segundos), humo de pólvora negra optimizado para mantener visibilidad de combate y combate naval dinámico al estilo de los mejores arcades de simulación.
4. **La Voz de los Marginados:**
   * Inclusión y protagonismo de sectores históricamente silenciados: los culíes chinos esclavizados liberados que lucharon como zapadores y combatientes, los conscriptos quechuas y aimaras de los *Colorados de Bolivia*, y los mineros y artesanos de los batallones cívicos.

---

## 2. Perfiles de Protagonistas (Basados en Registros Históricos Reales)

### 2.1 Bando Chileno: Abraham Quiroz (El Roto Culto)
* **Inspiración Histórica:** Abraham Quiroz, soldado raso real de Quillota, enrolado en el Regimiento Cazadores del Desierto y posteriormente en el 3° de Línea. Dejó un célebre epistolario dirigido a su padre, Don Luciano Quiroz.
* **Perfil Psicológico:** Un joven de origen humilde pero letrado, observador, reflexivo y profundamente consciente de la brutalidad que lo rodea. No encaja en el cliché folclórico; piensa, cuestiona la desolación del desierto, siente miedo y añora la huerta familiar.
* **Rol en el Juego:** Protagonista de la fase terrestre final (Capítulo 8).
* **Destino:** Cae en combate en las líneas de Miraflores. Su última carta a su padre se despliega al morir.

### 2.2 Bando Chileno / Naval: Wenceslao Vargas (El Último Grumete)
* **Inspiración Histórica:** Wenceslao Vargas Rojas, grumete de 17 años a bordo de la corbeta *Esmeralda* en Iquique.
* **Perfil Psicológico:** Joven aprendiz marinero en su primera batalla real. Vive el terror del casco de madera astillándose bajo los proyectiles de 300 libras del *Huáscar*, pero mantiene su puesto en cubierta junto a sus oficiales.
* **Rol en el Juego:** Protagonista del Acto 1 en el Capítulo 1 (Iquique).

### 2.3 Bando Peruano / Naval: Cabo Dámaso Antúnez (Torre Coles)
* **Inspiración Histórica:** Artillero de la dotación de la torre giratoria blindada del monitor *Huáscar*.
* **Perfil Psicológico:** Marino experimentado del Callao devoto de su comandante Miguel Grau. Vive la claustrofobia sofocante de la torre de hierro cerrada, el calor de los cañones Armstrong y el sobrecogimiento al ver a Prat saltar al abordaje.
* **Rol en el Juego:** Protagonista del Acto 2 en el Capítulo 1 (Iquique).

### 2.4 Bando Peruano: Augusto Bedoya / Soldado de la Reserva
* **Inspiración Histórica:** Abogados, tipógrafos, comerciantes y estudiantes que conformaron la Reserva de Lima para cavar trincheras y defender los reductos de Miraflores.
* **Perfil Psicológico:** Un ciudadano letrado que jamás empuñó un fusil militar hasta que la guerra llegó a las puertas de su ciudad. Su motivación no es la gloria militar, sino la protección de su familia y sus hogares.
* **Rol en el Juego:** Protagonista de la defensa defensiva en Miraflores / Arica.

### 2.5 Bando Boliviano: Subteniente Daniel Ballivián (Batallón Colorados de Bolivia)
* **Inspiración Histórica:** Daniel Ballivián, oficial combatiente en el Cerro Intiorko (Batalla del Alto de la Alianza / Tacna, 26 de mayo de 1880), autor de memorias de campaña.
* **Perfil Psicológico:** Joven paceño orgulloso de su uniforme rojo y de sus camaradas aimaras y mestizos. Experimenta el viaje desde las cumbres andinas hasta el calor despiadado del desierto salitrero, culminando en la carga desesperada de bayonetas cantando huaynos de guerra.
* **Rol en el Juego:** Protagonista táctico de la Batalla de Tacna.

### 2.6 Los Olvidados: Quintín Quintana / Tan Bi (Compañía Vulcano)
* **Inspiración Histórica:** Quintín Quintana, líder de los culíes chinos contratados en condiciones de semiesclavitud en las haciendas de Ica y Cañete. Al llegar las tropas chilenas, liberaron a cientos de sus compatriotas, quienes se integraron como zapadores y combatientes contra sus antiguos amos.
* **Perfil Psicológico:** Hombre curtido por el látigo y el trabajo forzado. Inicialmente desconfía de los soldados chilenos, viéndolos solo como un instrumento para quebrar sus cadenas. A través de la supervivencia extrema y tras salvar la vida de un soldado chileno herido, forja un vínculo inquebrantable de camaradería de armas.
* **Habilidades Únicas:** Gran agilidad, desactivación de trampas y dinamitas, combate silencioso y uso magistral de carabinas ligeras.

---

## 3. Especificaciones Técnicas y Jugabilidad por Fases

### 3.1 Fase FPS: Combate de Infantería
* **Filosofía de Control:** Respuesta moderna e inmediata (cámara en 1ra persona fluida, apuntado suave con miras de época, deslizamiento corto táctico, recarga reactiva).
* **Armamento:**
  * *Fusil Comblain (Belga/Chileno):* Monotiro de palanca, daño contundente a media distancia. Tiempo de recarga acelerado: **2.0 segundos**.
  * *Fusil Chassepot / Gras (Francés/Peruano):* Cerrojo manual de 11mm, gran precisión. Tiempo de recarga: **2.2 segundos**.
  * *Fusil Remington Rolling Block (Boliviano/Aliado):* Potencia de parada brutal, sonido cavernoso. Tiempo de recarga: **2.1 segundos**.
  * *Armas Blancas:* El emblemático **Corvo chileno** y la **Bayoneta triangular** aliada para remates cuerpo a cuerpo letales con animaciones viscerales en primera persona.
* **Gestión de Humo:** En lugar de cegar al jugador, los disparos generan bocanadas volumétricas cinematográficas que se disipan rápidamente, permitiendo lectura clara de objetivos en pantalla.

### 3.2 Fase Naval 3D: Duelo de Blindados y Cañones
* **Inspiración Mecánica:** Dinámico, inspirado en *World of Warships*, con interfaz limpia y controles accesibles.
* **Cámara y Control:**
  * Vista en tercera persona sobre el buque con zoom de artillería en el telémetro.
  * Control del timón (A/D) y telégrafo de calderas (W/S: Detener, 1/4, Media, Toda fuerza).
* **Sistemas de Combate:**
  * *Ángulo de Blindaje:* La inclinación del casco permite que proyectiles reboten o hagan impacto pleno.
  * *Torreta Giratoria (Torre Coles del Huáscar):* Rotación independiente del casco con retícula de convergencia.
  * *Espolonazos:* Maniobra de embestida a máxima velocidad para partir buques de madera o dañar cascos de hierro.
  * *Control de Daños:* Tecla rápida para extinguir fuegos, achicar inundaciones de casco o reparar vapor en calderas.

### 3.3 Fase RTS: Estrategia Táctica sin Construcción de Bases
* **Inspiración Mecánica:** Táctico en tiempo real al estilo *Company of Heroes* / *Total War*.
* **Control de Unidades:**
  * Se comandan escuadras de 8 a 12 hombres (Infantería de Línea, Cazadores tiradores, dotaciones de Cañones Krupp/Armstrong y Caballería de exploración).
* **Mecánicas Clave:**
  * *Supresión y Cobertura:* El fuego continuo inmoviliza a las escuadras enemigas en zanjas o dunas; maniobras de flanqueo son indispensables para romper defensas.
  * *Moral y Carga a la Bayoneta:* Al alcanzar distancia crítica con el enemigo suprimido, se activa la orden de asalto cuerpo a cuerpo.
  * *Suministro Crítico (Agua y Munición):* En el desierto, las tropas agotan cantimploras y cartuchos. Mantener carros de vituallas conectados a la retaguardia es vital para sostener el avance.

---

## 4. Sistema de Coleccionables: "La Memoria Rota"

### 4.1 Inspección Física en 3D
* Cuando el jugador se aproxima a escritorios de oficiales, bolsillos de soldados caídos o mochilas abandonadas, aparece la opción **[Inspeccionar]**.
* La cámara enfoca el objeto en 3D: el jugador puede rotarlo libremente con el ratón.
  * Al rotar una fotografía, puede ver la dedicatoria escrita con pluma en el reverso: *"A mi adorado hijo José, que Dios te guarde de los cañones. Tu madre, Cochabamba, 1879"*.
  * Al rotar una carta, el texto manuscrito se resalta con traducción clara si el calígrafo es complejo.

### 4.2 Arquitectura para Doblaje de Audio
* Cada documento cuenta con un botón en pantalla: **[Escuchar Carta]**.
* El sistema reproducirá una pista de audio (diseñado para albergar las grabaciones de voz personalizadas del desarrollador), con efectos de eco sutil y música de guitarra o piano acústico de época de fondo.

---

## 5. Cronología y Diseño Detallado de Capítulos

```
[Prólogo] Despacho de Valparaíso (Cinemática del Corresponsal de "The Times")
   │
   ├─► Cap. 1: "Madera y Blindaje" [NAVAL 3D] (Iquique y Punta Gruesa - 1879)
   │
   ├─► Cap. 2: "Caza en Alta Mar" [NAVAL 3D] (Combate de Angamos - 1879)
   │
   ├─► Cap. 3: "Sangre en el Salitre" [RTS] (Desembarco de Pisagua - 1879)
   │
   ├─► Cap. 4: "Sed en la Quebrada" [FPS] (Batalla de Tarapacá - 1879)
   │
   ├─► Cap. 5: "El Trueno de Intiorko" [RTS] (Batalla del Alto de la Alianza / Tacna - 1880)
   │
   ├─► Cap. 6: "Hasta el Último Cartucho" [FPS] (Asalto al Morro de Arica - 1880)
   │
   ├─► Cap. 7: "Cadenas Rotas" [FPS / Infiltración y Asalto] (Los Culíes y Cañete - 1880)
   │
   └─► Cap. 8: "Los que no volvieron" [FPS / Clímax Trágico] (Reductos de Miraflores - 1881)
```

---

### CAPÍTULO 1: "Madera y Blindaje"
* **Escenario:** Rada de Iquique y costas de Punta Gruesa (21 de mayo de 1879).
* **Modalidad:** Naval 3D.
* **Prólogo del Corresponsal:** Grabados del puerto de Iquique. Despacho británico: *"El viejo casco de madera de la corbeta chilena parecía un juguete ante la coraza de acero y los cañones de torre del monitor peruano..."*.
* **Jugabilidad (Perspectiva Dual):**
  * **Parte A (Chilena):** Al mando de la *Esmeralda* como Wenceslao Vargas en el puente. Maniobras desesperadas para esquivar las baterías costeras de tierra y contener al *Huáscar*. El jugador debe operar la artillería de costado contra el blindaje y resistir las embestidas de espolón. Termina con el abordaje de Arturo Prat sobre la cubierta enemiga.
  * **Parte B (Peruana):** El jugador asume el control del *Huáscar* en la torre giratoria Coles. Miguel Grau da instrucciones de apuntar a la línea de flotación. Al consumarse el hundimiento de la *Esmeralda*, la cámara se sitúa en la baranda del *Huáscar*: Grau ordena detener los cañones y desplegar botes salvavidas: *"¡Salvad a esos náufragos valientes!"*.
* **Documento Histórico de Cierre:** Facsímil interactivo en 3D de la **Carta de Don Miguel Grau a Doña Carmela Carvajal**, viuda de Prat, devolviendo sus prendas personales con las palabras de homenaje más célebres de la historia naval.

---

### CAPÍTULO 2: "Caza en Alta Mar"
* **Escenario:** Punta Angamos (8 de octubre de 1879).
* **Modalidad:** Naval 3D.
* **Prólogo del Corresponsal:** Mapas navales y crónicas sobre la solitaria y audaz campaña del *Huáscar* durante seis meses asediando puertos chilenos.
* **Jugabilidad:**
  * Control del combate entre la división blindada chilena (*Cochrane*, *Blanco Encalada*) y el *Huáscar*.
  * Combate de maniobras a alta velocidad para cortar la retirada del monitor, uso de artillería de torreta pesada y fuego concentrado.
  * Cinemática in-game de la explosión en la torre de mando donde perece Miguel Grau y el relevo sucesivo de comandantes peruanos que se niegan a arriar el pabellón.
* **Documento Histórico de Cierre:** El inventario de pertenencias y diario de navegación del *Huáscar*, con dedicatorias de los oficiales peruanos caídos.

---

### CAPÍTULO 3: "Sangre en el Salitre"
* **Escenario:** Bahía y acantilados de Pisagua (2 de noviembre de 1879).
* **Modalidad:** RTS Táctico.
* **Prólogo del Corresponsal:** *"Por primera vez en la historia moderna, una flota combinada descargó tropas y artillería sobre una costa defendida por trincheras excavadas en acantilados verticales..."*.
* **Jugabilidad:**
  * El jugador comanda la fuerza anfibia chilena: desembarco escalonado de botes planos bajo intenso fuego de fusilería boliviano y peruano apostado en el ferrocarril salitrero.
  * Despliegue de cabezas de playa, uso de fuego naval de cobertura para suprimir nidos de fusileros y asalto coordinado por las laderas empinadas del morro para tomar la estación de tren y asegurar los pozos de agua dulce.
* **Coleccionables en el Mapa:** Telegramas de los telegrafistas del ferrocarril salitrero pidiendo refuerzos con urgencia.

---

### CAPÍTULO 4: "Sed en la Quebrada"
* **Escenario:** Fondo de la Quebrada de Tarapacá (27 de noviembre de 1879).
* **Modalidad:** FPS Ágil.
* **Prólogo del Corresponsal:** Las columnas aliadas, agotadas y sin pertrechos, sorprenden a la vanguardia chilena en el fondo de un cañón seco.
* **Jugabilidad:**
  * Perspectiva peruana: Encarnando a un infante del batallón Zepita bajo el mando de Andrés Avelino Cáceres.
  * Combate laberíntico entre casas de adobe, pircas de piedra y sauces secos. 
  * Escasez crítica de cartuchos: necesidad de recoger munición del suelo o cambiar de fusil rápidamente entre un Comblain capturado y un Gras.
  * Carga a la bayoneta para rechazar a los granaderos y artilleros chilenos que descienden de los cerros.
* **Coleccionable:** Carta encontrada en la chaqueta de un oficial chileno del Regimiento 2° de Línea dirigida a su prometida en Santiago.

---

### CAPÍTULO 5: "El Trueno de Intiorko"
* **Escenario:** Meseta del cerro Intiorko / Tacna (26 de mayo de 1880).
* **Modalidad:** RTS Táctico.
* **Prólogo del Corresponsal:** Despacho sobre la mayor concentración de tropas de toda la guerra: 20.000 soldados enfrentados en las dunas bajo una neblina densa llamada *camanchaca*.
* **Jugabilidad:**
  * Perspectiva Aliada (Bolivia-Perú): El jugador comanda la línea defensiva de trincheras en la meseta arenosa.
  * Control del legendario **Batallón Colorados de Bolivia** (Subteniente Daniel Ballivián).
  * Repeler las primeras olas de asalto chileno con fuego coordinado de fusilería y artillería Krupp.
  * Momento clímax: Contraataque de los Colorados al trote con bayoneta calada para cerrar una brecha abierta en el flanco izquierdo. 
  * El empuje final de la caballería y artillería chilena arrolla las posiciones, forzando la retirada heroica de los supervivientes.
* **Coleccionable:** Diario de campaña ensangrentado de un soldado paceño con versos en quechua.

---

### CAPÍTULO 6: "Hasta el Último Cartucho"
* **Escenario:** Baterías y cima del Morro de Arica (7 de junio de 1880).
* **Modalidad:** FPS Ágil.
* **Prólogo del Corresponsal:** La negativa del Coronel Francisco Bolognesi de rendirse: *"Tengo deberes sagrados que cumplir y los cumpliré hasta quemar el último cartucho"*.
* **Jugabilidad:**
  * Perspectiva Peruana: El jugador encarna al soldado Manuel Salazar en el fuerte del Morro.
  * Asalto vertiginoso: los regimientos chilenos asaltan la posición cuesta arriba en apenas 55 minutos.
  * Disparos a quemarropa con fusiles Chassepot, defensa de los sacos de arena y fortines de caliza.
  * Secuencia final: Los últimos defensores combatiendo alrededor de la bandera peruana en la cúspide del abismo sobre el océano Pacífico.
* **Coleccionable:** La respuesta formal escrita de la junta de oficiales de Bolognesi ante el parlamentario chileno Salvo.

---

### CAPÍTULO 7: "Cadenas Rotas"
* **Escenario:** Valles y cañaverales de Cañete / Asalto a posiciones avanzadas de Lurín (Diciembre de 1880).
* **Modalidad:** FPS Infiltración y Asalto / Rol de Quintín Quintana.
* **Prólogo del Corresponsal:** Reporte sobre la liberación de miles de culíes asiáticos en los valles costeños y su insólita incorporación voluntaria a las filas expedicionarias como zapadores y vanguardia.
* **Jugabilidad:**
  * **Fase 1 (Sigilo y Desarme):** Quintín avanza entre los cañaverales durante la noche detectando y desactivando torpedos de tierra (minas artesanales peruanas) mediante estacas y cuerdas.
  * **Fase 2 (El Quiebre del Recelo):** Una patrulla chilena cae en una emboscada con dinamita. Quintín rompe el sigilo, empuña una carabina Winchester y abate a los atacantes, cargando en hombros a un soldado chileno herido hasta zona segura.
  * **Fase 3 (Asalto Frontal):** Equipado con armas de fuego y cargas explosivas, lidera a su escuadra de trabajadores liberados en el asalto a una trinchera fortificada para abrir paso a la columna principal.
* **Coleccionable:** Contrato de enganche de un culí chino con sello de la hacienda azucarera, manchado de salitre y pólvora.

---

### CAPÍTULO 8: "Los que no volvieron" (Clímax Trágico)
* **Escenario:** Reductos y trincheras de Miraflores, Lima (15 de enero de 1881).
* **Modalidad:** FPS Ágil.
* **Protagonista:** **Abraham Quiroz** (Soldado raso chileno, 3° de Línea).
* **Prólogo del Corresponsal:** *"La última línea de defensa antes de la capital estaba compuesta no por soldados de carrera, sino por los habitantes enteros de una urbe: profesores, jueces, comerciantes y jóvenes muchachos atrincherados en zanjas a lo largo de los campos de Miraflores..."*.
* **Jugabilidad:**
  * Avance bajo fuego cruzado atravesando acequias, tapias de haciendas y muros de adobe destrozados por la artillería.
  * El jugador combate contra los desesperados defensores de los Reductos (el jugador puede escuchar los gritos de hombres civiles defendiendo sus casas).
  * Tras un arduo asalto, Abraham y su escuadra logran silenciar la última batería del reducto y asegurar la posición.
* **El Desenlace:**
  * Una vez cumplido el objetivo, la música de combate cesa bruscamente. Solo se escucha el viento costero y los lamentos lejanos de los heridos.
  * Abraham baja su fusil, se sienta exhausto contra el parapeto de tierra y mete la mano en su guerrera para sacar la carta que planeaba enviar a su padre Luciano.
  * Un disparo rezagado de un francotirador oculto impacta en su pecho. Abraham cae al suelo lentamente; la carta resbala de sus manos en la arena.
  * La cámara se eleva en plano cenital sobre el cuerpo inmóvil. La pantalla se desvanece a negro absoluto.
* **Epílogo Histórico (Sin Fanfarria):**
  * En silencio sepulcral, aparece en pantalla el daguerrotipo/fotografía original de época de **Abraham Quiroz**.
  * Texto en pantalla:  
    **Soldado Abraham Quiroz**  
    *Regimiento 3° de Línea / Cazadores del Desierto*  
    *Natural de Quillota, Chile. Fallecido en combate.*  
  * En la pantalla se transcribe el fragmento real de su correspondencia a su padre:  
    > *"Mi querido padre: Si esta llega a sus manos y yo ya no existo, consuélese pensando que cumplí con lo que me tocó. No crea que no tuve miedo, pero más miedo tuve de no volver a ver la huerta de Quillota ni sus ojos cansados..."*
  * Al pulsar una tecla, la pantalla despliega un mosaico con fotos reales de combatientes peruanos, bolivianos y chilenos caídos, junto con el número total de bajas humanas del conflicto.
  * Cierre con la cita del corresponsal extranjero:  
    > *"Aquí no venció el odio, venció el deber; pero el precio lo pagaron los que no volvieron."*

---

## 6. Hoja de Ruta para Desarrollo en Unity

1. **Sprint 1: Base del Proyecto e Interfaz de Documentos (UI / Lore Engine)**
   * Configuración de Unity con URP (Universal Render Pipeline).
   * Script del Visor de Documentos 3D (rotación con mouse, zoom, despliegue de transcripción y trigger de audio para voz en off).
2. **Sprint 2: Prototipo Naval 3D (Capítulo 1 - Iquique)**
   * Físicas de navegación del buque (*ShipController*), sistema de flotabilidad y timón.
   * Sistema de artillería de torreta (retícula de puntería, recarga, dispersión balística y ángulo de blindaje).
3. **Sprint 3: Controlador FPS y Armamento de Época (Capítulo 4 o 6)**
   * Movimiento en primera persona ágil (caminar, correr, apuntar, culatazo/bayoneta).
   * Armas monotiro con ciclo de disparo acelerado y efectos de humo cinemático.
4. **Sprint 4: Controlador RTS Táctico (Capítulo 3 - Pisagua o Capítulo 5 - Tacna)**
   * Selección de escuadras con recuadro (box selection), navegación NavMesh en terreno accidentado.
   * Fuego de cobertura, supresión de moral y asalto con bayoneta.
