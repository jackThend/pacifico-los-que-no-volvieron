using System;
using Pacifico.Core.Naval;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 2, «Caza en alta mar» (8 de octubre de 1879, frente a Punta Angamos): el guion de la misión según
    /// <c>Historia_Completa_Guion.md</c> y el GDD. El jugador manda la fragata blindada Almirante Cochrane: corta la
    /// retirada del Huáscar, aguanta el fuego hasta poder perforar su coraza, presenta la suya en ángulo, esquiva el
    /// espolón y lo bate. Intercalado, el Huáscar: el proyectil en la torre de mando, la muerte de Grau y el relevo de
    /// sus oficiales. Termina con el abordaje que cierra las válvulas de fondo y las láminas de época del Archivo.
    /// <para>
    /// El guion no pone ninguna frase entre comillas en este capítulo: el prólogo se lee de su viñeta y el resto es
    /// narración. No hay coleccionable: el «libro de guardia del Huáscar» que pide el guion no está en el Archivo
    /// Histórico, y no se inventa un documento de época (ver DEV_LOG).
    /// </para>
    /// </summary>
    public static class AngamosChapter
    {
        public const string Id = "cap2_angamos";
        public const string ChapterHeading = "CAPÍTULO 2";

        /// <summary>El Huáscar abre fuego y se considera alcanzado el cerco (m). Diseño, del orden de las crónicas.</summary>
        public const float ChaseRangeM = 3000f;
        /// <summary>Si el Huáscar se aleja tanto del Cochrane durante la caza, ha escapado (m). Diseño.</summary>
        public const float EscapeRangeM = 8000f;
        /// <summary>Perforaciones del Cochrane antes del proyectil en la torre de mando. Diseño.</summary>
        public const int PenetrationsBeforeConningTower = 3;
        public const float DuelTimeoutSeconds = 300f;
        /// <summary>
        /// El Huáscar (11 nudos) no alcanza al Cochrane carenado (12) si este se aparta: solo se lanza al espolón cuando lo
        /// tiene cerca, y el jugador lo esquiva maniobrando (como en 1879: los intentos de embestir fracasaron).
        /// </summary>
        public const float RamStartRangeM = 1800f;
        public const float RamAfterSeconds = 60f;
        /// <summary>Duración máxima de la carrera de embestida (s): si no hay espolonazo, se ha esquivado.</summary>
        public const float RamRunSeconds = 40f;
        /// <summary>Perforaciones (de cualquier buque chileno) tras la muerte de Grau hasta que el Huáscar deja de responder.</summary>
        public const int HitsToSilence = 4;
        public const float ReliefTimeoutSeconds = 240f;
        /// <summary>
        /// Tiempo de juego entre la apertura de las válvulas y el hundimiento si nadie las cierra (s). Diseño: la simulación
        /// de maniobra del Cochrane (3.500 t, frenada lenta) da 4–5 minutos para llegar desde 1.500 m y quedarse al costado.
        /// </summary>
        public const float ScuttleSeconds = 300f;

        /// <summary>Abordaje: distancia entre centros, arrancada máxima y tiempo para arriar los botes y llegar al costado.</summary>
        public const float BoardingRadiusM = 200f;
        public const float BoardingMaxSpeedMps = 3f;
        public const float BoardingHoldSeconds = 8f;

        /// <summary>
        /// Balanceo de la cubierta del Cochrane: 3.500 t de reducto central, más estable que la Esmeralda aun con mar
        /// picado. Licencia de diseño: con 1° y 11 s la ventana de tiro a 1.000 m es parecida a la de Iquique a 600 m.
        /// </summary>
        public const float RollAmplitudeDeg = 1f;
        public const float RollPeriodSeconds = 11f;

        /// <summary>Hechos que escribe la escena.</summary>
        public static class Facts
        {
            public const string Broadsides = "andanadas_cochrane";
            /// <summary>Andanadas disparadas antes de tener al Huáscar a distancia de perforar su coraza.</summary>
            public const string BroadsidesBeyondRange = "andanadas_sin_alcance_de_perforacion";
            public const string PenetratingHits = "perforaciones_en_huascar";
            /// <summary>Impactos del Cochrane que la coraza del Huáscar detiene.</summary>
            public const string ArmorStops = "impactos_detenidos_por_coraza";
            public const string ConningTowerHits = "impactos_torre_de_mando";
            /// <summary>Perforaciones en el Huáscar (de cualquier buque chileno) tras la muerte de Grau.</summary>
            public const string HitsSinceGrau = "perforaciones_tras_grau";
            public const string HitsOnCochrane = "impactos_en_cochrane";
            /// <summary>Granadas del Huáscar que rebotan o no atraviesan la coraza del Cochrane.</summary>
            public const string Deflected = "granadas_desviadas";
            public const string RamsReceived = "espolonazos_recibidos";
            /// <summary>Progreso del abordaje (0 a 1).</summary>
            public const string Boarding = "abordaje";
        }

        /// <summary>Marcas: unas las pone la escena; otras, el guion para que la escena reaccione.</summary>
        public static class Flags
        {
            /// <summary>El Cochrane tiene al Huáscar a distancia de tiro (<see cref="ChaseRangeM"/>).</summary>
            public const string WithinRange = "huascar_a_tiro";
            public const string HuascarEscaped = "huascar_escapa";
            /// <summary>Guion → escena: la Unión se separa a toda máquina.</summary>
            public const string UnionBreaksAway = "union_escapa";
            /// <summary>Guion → escena: el Huáscar vira para enfrentar al Cochrane.</summary>
            public const string HuascarTurns = "huascar_vira";
            /// <summary>
            /// Escena: el Huáscar se lanza al espolón (tras la primera perforación o <see cref="RamAfterSeconds"/> de
            /// duelo, cuando lo tiene a menos de <see cref="RamStartRangeM"/>).
            /// </summary>
            public const string RamAttempt = "huascar_al_espolon";
            /// <summary>Escena: pasó la carrera de embestida sin espolonazo.</summary>
            public const string RamAvoided = "espolon_esquivado";
            public const string HuascarSteeringJammed = "huascar_sin_gobierno";
            /// <summary>Guion → escena: el Huáscar deja de responder y abre las válvulas.</summary>
            public const string ValvesOpen = "valvulas_abiertas";
            /// <summary>Escena: los botes del Cochrane han llegado al costado del Huáscar.</summary>
            public const string Boarded = "huascar_abordado";
            public const string ValvesClosed = "valvulas_cerradas";
            /// <summary>Guion → escena: nadie cerró las válvulas a tiempo; el Huáscar se va a pique.</summary>
            public const string ScuttleComplete = "huascar_a_pique";
            public const string HuascarSunk = "huascar_hundido";
            public const string CochraneSunk = "cochrane_hundido";
            public const string PlatesClosed = "laminas_cerradas";
        }

        public static class Stages
        {
            public const string Trap = "a1_la_trampa";
            public const string Duel = "a2_duelo_de_blindados";
            public const string ConningTower = "b1_torre_de_mando";
            public const string Relief = "a3_el_relevo";
            public const string Valves = "a4_las_valvulas";
            public const string Plates = "cierre_laminas";
        }

        /// <summary>Qué ve y controla el jugador en cada etapa.</summary>
        public static class Perspectives
        {
            /// <summary>El puente del Cochrane: máquina, timón, andanadas y brigada de averías.</summary>
            public const string CochraneBridge = "cochrane_puente";
            /// <summary>Intercalado a bordo del Huáscar: sin mandos, la cámara sigue al monitor.</summary>
            public const string HuascarDeck = "huascar_cubierta";
            /// <summary>Láminas de época del Archivo Histórico.</summary>
            public const string Plates = "laminas";
        }

        public const string Latorre = "Juan José Latorre";

        /// <summary>
        /// Distancia (m, redondeada a la centena inferior) a la que las piezas de 250 lb del Cochrane empiezan a
        /// perforar el cinturón central del Huáscar en un impacto perpendicular: la distancia de apertura de fuego.
        /// Sale del modelo de blindaje (≈2.200 m), no de una constante de diseño.
        /// </summary>
        public static float OpeningFireRangeM()
        {
            ShipSpec cochrane = ShipCatalog.Cochrane();
            GunMount battery = BroadsideBatteryModel.FromShip(cochrane).Gun;
            float range = ArmorPenetrationModel.PenetrationRangeM(battery, ShipCatalog.Huascar(), ArmorZone.BeltMidships);
            return (float)Math.Floor(range / 100f) * 100f;
        }

        /// <summary>«2.200»: miles con punto, como en el resto de los textos (sin depender de la cultura del sistema).</summary>
        public static string Meters(float meters) =>
            ((int)Math.Round(meters)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

        public static MissionScript Build(Func<string, string> readRepositoryFile) =>
            Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading));

        public static MissionScript Build(GuionQuotes guion)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));

            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);
            MissionCondition idle = MissionCondition.DialogueIdle();
            string openFire = Meters(OpeningFireRangeM());

            var script = new MissionScript
            {
                Id = Id,
                Title = "Caza en alta mar",
                Date = "8 de octubre de 1879",
                Location = "Frente a Punta Angamos",
            };

            // ---- La trampa: cortar la retirada ------------------------------------------------------------
            var trap = new MissionStage { Id = Stages.Trap, Title = "La trampa de Angamos", Perspective = Perspectives.CochraneBridge };
            trap.OnEnter.Add(Narrate("8 de octubre de 1879, frente a Punta Angamos. Fragata blindada Almirante Cochrane, comandante Juan José Latorre."));
            trap.OnEnter.Add(Narrate(guion.Field("Prólogo")));
            trap.OnEnter.Add(Narrate("Amanece con mar picado y cielo plomizo. El Huáscar y la corbeta Unión navegan a toda máquina hacia el norte."));
            trap.OnEnter.Add(Hint("W/S: máquina. A/D: timón. Córtale el paso al Huáscar antes de que escape."));
            trap.Objectives.Add(new MissionObjective
            {
                Id = "cerco", Text = "Corta la retirada del Huáscar (" + Meters(ChaseRangeM) + " m)",
                Complete = MissionCondition.Flag(Flags.WithinRange),
            });
            var union = new MissionTrigger { Id = "la_union", When = MissionCondition.All(MissionCondition.StageTime(20f), idle), SetsFlag = Flags.UnionBreaksAway };
            union.Lines.Add(Narrate("La Unión, más veloz, se separa a toda máquina: Grau le ha ordenado salvar un buque para la república."));
            trap.Triggers.Add(union);
            var turns = new MissionTrigger
            {
                Id = "el_huascar_vira",
                When = MissionCondition.All(MissionCondition.Flag(Flags.UnionBreaksAway), idle,
                    MissionCondition.Any(MissionCondition.Flag(Flags.WithinRange), MissionCondition.StageTime(90f))),
                SetsFlag = Flags.HuascarTurns,
            };
            turns.Lines.Add(Narrate("El Huáscar vira y se vuelve para enfrentar solo al Cochrane."));
            trap.Triggers.Add(turns);
            trap.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.HuascarTurns), idle), Stages.Duel));
            script.Stages.Add(trap);

            // ---- El duelo de blindados ------------------------------------------------------------------------
            var duel = new MissionStage { Id = Stages.Duel, Title = "Duelo de blindados", Perspective = Perspectives.CochraneBridge };
            duel.OnEnter.Add(Narrate("El Huáscar abre fuego. Sus granadas de 300 libras levantan columnas de agua junto al Cochrane."));
            duel.OnEnter.Add(Narrate("Latorre ordena no responder hasta tenerlo a " + openFire + " m: más lejos, las granadas de 250 libras no atraviesan su coraza."));
            duel.OnEnter.Add(Hint("Espacio: andanada cuando la cubierta pase por la horizontal (la aguja en la zona verde)."));
            duel.OnEnter.Add(Hint("Mientras recargas, presenta la coraza en ángulo (unos 45°): sus granadas no atravesarán los extremos. Para la andanada, vuelve a ponerlo por el través. 1/2/3: averías."));
            duel.Objectives.Add(new MissionObjective
            {
                Id = "perforar", Text = "Perfora la coraza del Huáscar",
                Complete = MissionCondition.AtLeast(Facts.PenetratingHits, PenetrationsBeforeConningTower),
                ProgressFact = Facts.PenetratingHits, ProgressTarget = PenetrationsBeforeConningTower,
            });
            duel.Objectives.Add(new MissionObjective
            {
                Id = "retener_fuego", Text = "Retén el fuego hasta los " + openFire + " m", Optional = true,
                Complete = MissionCondition.All(MissionCondition.AtLeast(Facts.PenetratingHits, 1),
                    MissionCondition.Not(MissionCondition.AtLeast(Facts.BroadsidesBeyondRange, 1))),
            });
            duel.Objectives.Add(new MissionObjective
            {
                Id = "coraza_en_angulo", Text = "Desvía sus granadas con la coraza", Optional = true,
                Complete = MissionCondition.AtLeast(Facts.Deflected, 2), ProgressFact = Facts.Deflected, ProgressTarget = 2,
            });
            duel.Objectives.Add(new MissionObjective
            {
                Id = "esquivar_espolon", Text = "Esquiva el espolón del Huáscar", Optional = true, Complete = MissionCondition.Flag(Flags.RamAvoided),
            });
            duel.Objectives.Add(new MissionObjective
            {
                Id = "gobierno", Text = "Alcanza la popa: el gobierno del Huáscar", Optional = true, Complete = MissionCondition.Flag(Flags.HuascarSteeringJammed),
            });
            var stopped = new MissionTrigger { Id = "coraza_detiene", When = MissionCondition.AtLeast(Facts.ArmorStops, 1) };
            stopped.Lines.Add(Narrate("La granada de 250 libras estalla contra el cinturón del Huáscar sin atravesarlo. Hay que acortar distancia."));
            duel.Triggers.Add(stopped);
            var pierced = new MissionTrigger { Id = "primera_perforacion", When = MissionCondition.AtLeast(Facts.PenetratingHits, 1) };
            pierced.Lines.Add(Narrate("¡Perforado! La granada atraviesa el blindaje del Huáscar y estalla dentro."));
            duel.Triggers.Add(pierced);
            var deflected = new MissionTrigger { Id = "rebote", When = MissionCondition.AtLeast(Facts.Deflected, 1) };
            deflected.Lines.Add(Narrate("Una granada de 300 libras se estrella contra la coraza del Cochrane sin atravesarla."));
            duel.Triggers.Add(deflected);
            var steering = new MissionTrigger { Id = "sin_gobierno", When = MissionCondition.Flag(Flags.HuascarSteeringJammed) };
            steering.Lines.Add(Narrate("Un impacto en la popa corta los guardines del timón: el Huáscar gira sin gobierno."));
            duel.Triggers.Add(steering);
            var ramRun = new MissionTrigger { Id = "al_espolon", When = MissionCondition.Flag(Flags.RamAttempt), Interrupts = true };
            ramRun.Lines.Add(Narrate("Grau lanza el Huáscar al espolón contra el Cochrane. ¡Todo el timón a la banda!"));
            duel.Triggers.Add(ramRun);
            var avoided = new MissionTrigger { Id = "espolon_esquivado", When = MissionCondition.Flag(Flags.RamAvoided) };
            avoided.Lines.Add(Narrate("El espolón pasa de largo. El Huáscar vuelve a abrir distancia."));
            duel.Triggers.Add(avoided);
            duel.Transitions.Add(new MissionTransition(
                MissionCondition.All(idle, MissionCondition.Any(
                    MissionCondition.AtLeast(Facts.PenetratingHits, PenetrationsBeforeConningTower),
                    MissionCondition.AtLeast(Facts.ConningTowerHits, 1),
                    MissionCondition.StageTime(DuelTimeoutSeconds))),
                Stages.ConningTower));
            script.Stages.Add(duel);

            // ---- Intercalado: la torre de mando del Huáscar --------------------------------------------------
            var tower = new MissionStage { Id = Stages.ConningTower, Title = "La torre de mando", Perspective = Perspectives.HuascarDeck };
            tower.OnEnter.Add(Narrate("A bordo del Huáscar."));
            tower.OnEnter.Add(Narrate("Un proyectil perforante del Cochrane estalla de lleno en la torre de mando."));
            tower.OnEnter.Add(Narrate("Mueren en el acto Miguel Grau y su ayudante, el teniente Diego Ferré."));
            tower.OnEnter.Add(Narrate("El capitán de corbeta Elías Aguirre asume el mando. El Huáscar no arría su pabellón."));
            tower.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.StageTime(1f), idle), Stages.Relief));
            script.Stages.Add(tower);

            // ---- El relevo de los comandantes ----------------------------------------------------------------
            var relief = new MissionStage { Id = Stages.Relief, Title = "El relevo", Perspective = Perspectives.CochraneBridge };
            relief.OnEnter.Add(Narrate("El Blanco Encalada, con el comodoro Galvarino Riveros a bordo, se suma al combate."));
            relief.OnEnter.Add(Hint("Sigue batiendo al Huáscar hasta que deje de responder."));
            relief.Objectives.Add(new MissionObjective
            {
                Id = "silenciar", Text = "Silencia al Huáscar",
                Complete = MissionCondition.AtLeast(Facts.HitsSinceGrau, HitsToSilence), ProgressFact = Facts.HitsSinceGrau, ProgressTarget = HitsToSilence,
            });
            var aguirre = new MissionTrigger { Id = "cae_aguirre", When = MissionCondition.AtLeast(Facts.HitsSinceGrau, 1) };
            aguirre.Lines.Add(Narrate("Cae Elías Aguirre. Toma el mando el teniente primero José Melitón Rodríguez."));
            relief.Triggers.Add(aguirre);
            var flag = new MissionTrigger { Id = "pabellon", When = MissionCondition.AtLeast(Facts.HitsSinceGrau, 2) };
            flag.Lines.Add(Narrate("Un proyectil abate el pabellón del Huáscar. Un marinero vuelve a izarlo bajo el fuego."));
            relief.Triggers.Add(flag);
            var rodriguez = new MissionTrigger { Id = "cae_rodriguez", When = MissionCondition.AtLeast(Facts.HitsSinceGrau, 3) };
            rodriguez.Lines.Add(Narrate("Muere Rodríguez. Lo releva el teniente Enrique Palacios, que cae herido; el mando pasa al teniente primero Pedro Gárezon."));
            relief.Triggers.Add(rodriguez);
            relief.Transitions.Add(new MissionTransition(
                MissionCondition.All(idle, MissionCondition.Any(
                    MissionCondition.AtLeast(Facts.HitsSinceGrau, HitsToSilence), MissionCondition.StageTime(ReliefTimeoutSeconds))),
                Stages.Valves));
            script.Stages.Add(relief);

            // ---- Las válvulas abiertas -------------------------------------------------------------------------
            var valves = new MissionStage { Id = Stages.Valves, Title = "Las válvulas abiertas", Perspective = Perspectives.CochraneBridge };
            var silent = new MissionTrigger { Id = "valvulas", When = MissionCondition.StageTime(0f), SetsFlag = Flags.ValvesOpen };
            silent.Lines.Add(Narrate("El Huáscar, destrozado y con la máquina parada, deja de disparar."));
            silent.Lines.Add(Narrate("Gárezon ordena abrir las válvulas de fondo: hundirlo antes que entregarlo."));
            silent.Lines.Add(Hint("Ponte a su costado despacio (a menos de " + Meters(BoardingRadiusM) + " m y " +
                Common.Units.MetersPerSecondToKnots(BoardingMaxSpeedMps).ToString("0") + " nudos) para que los botes lleven a los marineros de abordaje. S: atrás."));
            valves.Triggers.Add(silent);
            valves.Objectives.Add(new MissionObjective
            {
                Id = "abordar", Text = "Aborda el Huáscar antes de que se hunda", Complete = MissionCondition.Flag(Flags.ValvesClosed),
                ProgressFact = Facts.Boarding, ProgressTarget = 1f,
            });
            var boarded = new MissionTrigger { Id = "abordaje", When = MissionCondition.Flag(Flags.Boarded), Interrupts = true, SetsFlag = Flags.ValvesClosed };
            boarded.Lines.Add(Narrate("Los botes del Cochrane llegan al costado. Marineros chilenos saltan a bordo con la pistola en la mano."));
            boarded.Lines.Add(Narrate("Bajan a la sala de máquinas y cierran las válvulas. El agua ya cubre los fondos."));
            valves.Triggers.Add(boarded);
            var founders = new MissionTrigger
            {
                Id = "a_pique", When = MissionCondition.All(MissionCondition.StageTime(ScuttleSeconds), MissionCondition.NotFlag(Flags.Boarded)),
                Interrupts = true, SetsFlag = Flags.ScuttleComplete,
            };
            founders.Lines.Add(Narrate("El agua inunda la sala de máquinas. El Huáscar se va a pique con su pabellón."));
            valves.Triggers.Add(founders);
            valves.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.ValvesClosed), idle), Stages.Plates));
            script.Stages.Add(valves);

            // ---- Cierre: las láminas del Archivo ---------------------------------------------------------------
            var plates = new MissionStage { Id = Stages.Plates, Title = "La captura del Huáscar", Perspective = Perspectives.Plates };
            plates.OnEnter.Add(Narrate("El Huáscar es llevado a Mejillones con la bandera de Chile. Con él, el Perú pierde el dominio del mar."));
            plates.OnEnter.Add(Hint("Espacio: siguiente lámina."));
            plates.Transitions.Add(new MissionTransition(MissionCondition.Flag(Flags.PlatesClosed), MissionScript.CompleteStage));
            script.Stages.Add(plates);

            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.HuascarEscaped), "El Huáscar ha escapado hacia el norte."));
            script.Failures.Add(new MissionFailure(MissionCondition.All(MissionCondition.Flag(Flags.ScuttleComplete), idle),
                "Los botes no llegaron a tiempo: el Huáscar se ha ido a pique con las válvulas abiertas."));
            script.Failures.Add(new MissionFailure(MissionCondition.All(MissionCondition.Flag(Flags.HuascarSunk), MissionCondition.NotFlag(Flags.ScuttleComplete)),
                "El Huáscar se ha hundido."));
            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.CochraneSunk), "El Cochrane se ha hundido."));
            return script;
        }
    }
}
