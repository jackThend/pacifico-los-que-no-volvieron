using System;
using Pacifico.Core.Tactics;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 3, «Sangre en el salitre» (2 de noviembre de 1879, bahía de Pisagua), según
    /// <c>Historia_Completa_Guion.md</c> y el GDD: la comandancia de desembarco chilena. Tres fases del guion:
    /// la cabeza de playa (botes bajo el fuego y la flota contra los fuertes de Punta Pichalo y Punta Guacas), el asalto
    /// al risco (supresión para que los zapadores suban al terraplén del ferrocarril) y los pozos de agua (salvar los
    /// estanques y las locomotoras de la dinamita y aguantar el contraataque). El prólogo del corresponsal se lee del
    /// guion; la recompensa es el despacho del coronel Wood sobre el desembarco.
    /// </summary>
    public static class PisaguaChapter
    {
        public const string Id = "cap3_pisagua";
        public const string ChapterHeading = "CAPÍTULO 3";
        public const string RewardId = "despacho_2_pisagua";

        /// <summary>Escuadras que desembarcan: dos olas de dos botes (Atacama y Zapadores).</summary>
        public const int DefaultBoats = 4;
        public const int MenPerBoat = 12;
        /// <summary>Distancia de los transportes a la playa (m). Comprimida: dos minutos de travesía.</summary>
        public const float BoatRunM = 300f;
        public const int FortsToSilence = 2;
        /// <summary>Si la cabeza de playa se alarga tanto, la flota acaba de batir los fuertes por su cuenta (s).</summary>
        public const float BeachheadTimeoutSeconds = 420f;
        /// <summary>
        /// Mechas de los estanques y de las locomotoras desde que empieza la fase 3 (s). Diseño: del terraplén a los
        /// estanques hay unos 150 m de ladera (~2 minutos a paso de asalto), con los defensores replegados en la estación.
        /// </summary>
        public const float WaterFuseSeconds = 210f;
        public const float LocomotiveFuseSeconds = 270f;
        /// <summary>Lo que dura el contraataque de la retaguardia aliada si no se le rechaza antes (s).</summary>
        public const float DefendSeconds = 120f;

        /// <summary>
        /// Nidos de fusileros del terraplén (los que tiran a los botes y defienden la ladera): dos de 8 hombres con
        /// Remington tras parapetos de piedra. Calibrado con la simulación de fuego y supresión del juego (DEV_LOG 6.8):
        /// subiendo todas las escuadras a la vez sin tirar no se toma nunca; fijándolos con fuego mientras suben dos, en
        /// tres de cada cuatro partidas.
        /// </summary>
        public const int EmbankmentNests = 2;
        public const int MenPerNest = 8;
        public const float NestCover = 0.5f;
        /// <summary>Un nido se repliega con esta fuerza o menos…</summary>
        public const int WithdrawAtStrength = 4;
        /// <summary>…o suprimido con chilenos a esta distancia (m).</summary>
        public const float WithdrawWhenSuppressedWithinM = 50f;

        /// <summary>¿Se repliega hacia la estación este nido de fusileros?</summary>
        public static bool NestWithdraws(int strength, bool suppressed, float nearestEnemyM) =>
            strength <= WithdrawAtStrength || (suppressed && nearestEnemyM <= WithdrawWhenSuppressedWithinM);

        public static class Facts
        {
            public const string BoatsLanded = "botes_en_la_playa";
            public const string MenLanded = "hombres_desembarcados";
            public const string MenLostAtSea = "bajas_en_los_botes";
            public const string FortsSilenced = "fuertes_acallados";
            public const string MenInField = "hombres_en_tierra";
            public const string Salvos = "salvas_de_la_flota";
        }

        public static class Flags
        {
            public const string PichaloSilenced = "fuerte_pichalo_acallado";
            public const string GuacasSilenced = "fuerte_guacas_acallado";
            /// <summary>Guion → escena: zarpa la segunda ola de botes.</summary>
            public const string SecondWave = "segunda_ola";
            /// <summary>Guion → escena: la flota termina de batir los fuertes que queden.</summary>
            public const string FleetFinishesForts = "flota_bate_los_fuertes";
            public const string EmbankmentTaken = "terraplen_tomado";
            /// <summary>Guion → escena: se encienden las mechas de los estanques y las locomotoras.</summary>
            public const string FusesLit = "mechas_encendidas";
            public const string WaterSecured = "estanques_salvados";
            public const string WaterBlown = "estanques_volados";
            public const string LocomotivesSecured = "locomotoras_salvadas";
            public const string LocomotivesBlown = "locomotoras_voladas";
            /// <summary>Guion → escena: la retaguardia aliada contraataca.</summary>
            public const string CounterAttack = "contraataque";
            /// <summary>Escena: el contraataque ha sido rechazado (sus escuadras caídas o desbandadas).</summary>
            public const string CounterRepulsed = "contraataque_rechazado";
            public const string DocumentClosed = "despacho_cerrado";
        }

        public static class Stages
        {
            public const string Prologue = "p0_prologo";
            public const string Beachhead = "p1_cabeza_de_playa";
            public const string Cliff = "p2_asalto_al_risco";
            public const string Wells = "p3_los_pozos";
            public const string Defense = "p4_la_defensa";
            public const string Dispatch = "p5_despacho";
        }

        public static class Perspectives
        {
            /// <summary>Lámina del Archivo con el prólogo del corresponsal.</summary>
            public const string Plate = "lamina";
            /// <summary>Mapa táctico: la comandancia de desembarco.</summary>
            public const string Command = "comandancia";
            public const string Document = "documento";
        }

        public const string Correspondent = "El corresponsal";
        public const string Pichalo = "Punta Pichalo";
        public const string Guacas = "Punta Guacas";

        /// <summary>El prólogo del corresponsal, literal del guion y sin sus comillas.</summary>
        public static string Prologue(GuionQuotes guion) => guion.QuotedField("Prólogo del Corresponsal");

        public static MissionScript Build(Func<string, string> readRepositoryFile, int boats = DefaultBoats) =>
            Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading), boats);

        public static MissionScript Build(GuionQuotes guion, int boats = DefaultBoats)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));
            if (boats < 2) throw new ArgumentOutOfRangeException(nameof(boats));
            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);
            MissionCondition idle = MissionCondition.DialogueIdle();
            MissionCondition noneLeft = MissionCondition.Not(MissionCondition.AtLeast(Facts.MenInField, 1));
            int firstWave = boats / 2;

            var script = new MissionScript
            {
                Id = Id, Title = "Sangre en el salitre", Date = "2 de noviembre de 1879", Location = "Bahía de Pisagua",
                // El guion pide una fotografía hallada en la estación, que el Archivo no tiene; el catálogo asigna a este
                // capítulo el despacho del coronel Wood sobre el desembarco.
                RewardCollectibleId = RewardId,
            };

            // ---- Prólogo ------------------------------------------------------------------------------------
            var prologue = new MissionStage { Id = Stages.Prologue, Title = "El anfiteatro", Perspective = Perspectives.Plate };
            prologue.OnEnter.Add(Narrate("2 de noviembre de 1879. La escuadra chilena fondea frente a Pisagua con el ejército expedicionario."));
            prologue.OnEnter.Add(new MissionLine(Correspondent, Prologue(guion)));
            prologue.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.StageTime(1f), idle), Stages.Beachhead));
            script.Stages.Add(prologue);

            // ---- Fase 1: la cabeza de playa --------------------------------------------------------------------
            var beach = new MissionStage { Id = Stages.Beachhead, Title = "La cabeza de playa", Perspective = Perspectives.Command };
            beach.OnEnter.Add(Narrate("Comandancia de desembarco. Los botes planos del Atacama y de los Zapadores se separan de los transportes."));
            beach.OnEnter.Add(Hint("B y clic en el mapa (o los botones de los fuertes): marca el blanco de la artillería de la flota, el Cochrane y el O'Higgins."));
            beach.OnEnter.Add(Hint("Mientras los fuertes y los fusileros tiren, los botes sangran. Callados los fuertes, la travesía cuesta poco."));
            beach.Objectives.Add(new MissionObjective { Id = "pichalo", Text = "Silencia el fuerte de " + Pichalo, Complete = MissionCondition.Flag(Flags.PichaloSilenced) });
            beach.Objectives.Add(new MissionObjective { Id = "guacas", Text = "Silencia el fuerte de " + Guacas, Complete = MissionCondition.Flag(Flags.GuacasSilenced) });
            beach.Objectives.Add(new MissionObjective
            {
                Id = "desembarco", Text = "Desembarca las escuadras",
                Complete = MissionCondition.AtLeast(Facts.BoatsLanded, boats), ProgressFact = Facts.BoatsLanded, ProgressTarget = boats,
            });
            var landed = new MissionTrigger { Id = "en_la_playa", When = MissionCondition.AtLeast(Facts.BoatsLanded, 1) };
            landed.Lines.Add(Narrate("Las primeras compañías saltan de los botes entre las rocas. Ni un grito: avanzan en silencio."));
            landed.Lines.Add(Hint("Clic o recuadro: seleccionar. Clic derecho: mover o atacar. Ponlas a cubierto al pie del risco."));
            beach.Triggers.Add(landed);
            var second = new MissionTrigger { Id = "segunda_ola", When = MissionCondition.AtLeast(Facts.BoatsLanded, firstWave), SetsFlag = Flags.SecondWave };
            second.Lines.Add(Narrate("Los botes vacíos vuelven a los transportes. Zarpa la segunda ola."));
            beach.Triggers.Add(second);
            var fort = new MissionTrigger { Id = "primer_fuerte", When = MissionCondition.AtLeast(Facts.FortsSilenced, 1) };
            fort.Lines.Add(Narrate("Una granada de la flota revienta entre las piezas del fuerte. Sus cañones callan."));
            beach.Triggers.Add(fort);
            var fleet = new MissionTrigger
            {
                Id = "la_flota", When = MissionCondition.All(MissionCondition.StageTime(BeachheadTimeoutSeconds), MissionCondition.Not(MissionCondition.AtLeast(Facts.FortsSilenced, FortsToSilence))),
                SetsFlag = Flags.FleetFinishesForts,
            };
            fleet.Lines.Add(Narrate("Los blindados se acercan a la costa y baten los fuertes a quemarropa hasta acallarlos."));
            beach.Triggers.Add(fleet);
            beach.Transitions.Add(new MissionTransition(
                MissionCondition.All(idle, MissionCondition.AtLeast(Facts.BoatsLanded, boats), MissionCondition.AtLeast(Facts.FortsSilenced, FortsToSilence)),
                Stages.Cliff));
            script.Stages.Add(beach);

            // ---- Fase 2: el asalto al risco ---------------------------------------------------------------------
            var cliff = new MissionStage { Id = Stages.Cliff, Title = "El asalto al risco", Perspective = Perspectives.Command };
            cliff.OnEnter.Add(Narrate("No hay sitio para atrincherarse. Desde el terraplén del ferrocarril salitrero, los fusileros bolivianos y peruanos barren la ladera."));
            cliff.OnEnter.Add(Hint("Fija a los fusileros con el fuego de una escuadra (clic derecho sobre ellos) y, mientras estén suprimidos, sube a los zapadores por la duna."));
            cliff.Objectives.Add(new MissionObjective { Id = "terraplen", Text = "Toma el terraplén del ferrocarril", Complete = MissionCondition.Flag(Flags.EmbankmentTaken) });
            var crest = new MissionTrigger { Id = "en_el_terraplen", When = MissionCondition.Flag(Flags.EmbankmentTaken) };
            crest.Lines.Add(Narrate("Los zapadores coronan el terraplén con la mochila a la espalda y el fusil en alto. Los defensores se repliegan hacia la estación."));
            cliff.Triggers.Add(crest);
            cliff.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.EmbankmentTaken), idle), Stages.Wells));
            script.Stages.Add(cliff);

            // ---- Fase 3: los pozos de agua ----------------------------------------------------------------------
            var wells = new MissionStage { Id = Stages.Wells, Title = "Los pozos de agua", Perspective = Perspectives.Command };
            wells.Triggers.Add(new MissionTrigger { Id = "mechas", When = MissionCondition.StageTime(0f), SetsFlag = Flags.FusesLit });
            wells.OnEnter.Add(Narrate("En su repliegue hacia el desierto, los defensores han cebado dinamita en los estanques de agua dulce y en las locomotoras."));
            wells.OnEnter.Add(Hint("Llega a cada objetivo sin enemigos alrededor para cortar la mecha. Sin agua, el ejército no puede internarse en el desierto."));
            wells.Objectives.Add(new MissionObjective { Id = "agua", Text = "Salva los estanques de agua dulce", Complete = MissionCondition.Flag(Flags.WaterSecured) });
            wells.Objectives.Add(new MissionObjective { Id = "locomotoras", Text = "Salva las locomotoras", Optional = true, Complete = MissionCondition.Flag(Flags.LocomotivesSecured) });
            var water = new MissionTrigger { Id = "agua_salvada", When = MissionCondition.Flag(Flags.WaterSecured) };
            water.Lines.Add(Narrate("Un zapador arranca la mecha a dos palmos de la carga. El agua de Pisagua es chilena."));
            wells.Triggers.Add(water);
            var engines = new MissionTrigger { Id = "locomotoras_salvadas", When = MissionCondition.Flag(Flags.LocomotivesSecured) };
            engines.Lines.Add(Narrate("Las locomotoras quedan intactas en la estación: el ferrocarril llevará al ejército hacia la pampa."));
            wells.Triggers.Add(engines);
            var lost = new MissionTrigger { Id = "locomotoras_voladas", When = MissionCondition.Flag(Flags.LocomotivesBlown) };
            lost.Lines.Add(Narrate("La dinamita revienta la caldera de una locomotora. El ejército tendrá que marchar a pie."));
            wells.Triggers.Add(lost);
            MissionCondition enginesResolved = MissionCondition.Any(MissionCondition.Flag(Flags.LocomotivesSecured), MissionCondition.Flag(Flags.LocomotivesBlown));
            wells.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.WaterSecured), enginesResolved, idle), Stages.Defense));
            script.Stages.Add(wells);

            // ---- Fase 3: la defensa -------------------------------------------------------------------------------
            var defense = new MissionStage { Id = Stages.Defense, Title = "La defensa de los pozos", Perspective = Perspectives.Command };
            defense.Triggers.Add(new MissionTrigger { Id = "contraataque", When = MissionCondition.StageTime(0f), SetsFlag = Flags.CounterAttack });
            defense.OnEnter.Add(Narrate("La retaguardia aliada vuelve sobre la estación para intentar volar los estanques."));
            defense.OnEnter.Add(Hint("Defiende los estanques: a cubierto, el fuego concentrado los detiene."));
            defense.Objectives.Add(new MissionObjective
            {
                Id = "defender", Text = "Rechaza el contraataque",
                Complete = MissionCondition.Any(MissionCondition.Flag(Flags.CounterRepulsed), MissionCondition.StageTime(DefendSeconds)),
            });
            var held = new MissionTrigger
            {
                Id = "pisagua_tomada", When = MissionCondition.Any(MissionCondition.Flag(Flags.CounterRepulsed), MissionCondition.StageTime(DefendSeconds)),
            };
            held.Lines.Add(Narrate("Los últimos defensores se pierden hacia el interior por la línea del tren. Pisagua está tomada: la invasión de Tarapacá ha comenzado."));
            defense.Triggers.Add(held);
            defense.Transitions.Add(new MissionTransition(
                MissionCondition.All(idle, MissionCondition.Any(MissionCondition.Flag(Flags.CounterRepulsed), MissionCondition.StageTime(DefendSeconds))),
                Stages.Dispatch));
            script.Stages.Add(defense);

            // ---- El despacho ----------------------------------------------------------------------------------------
            var dispatch = new MissionStage { Id = Stages.Dispatch, Title = "El silencio chileno", Perspective = Perspectives.Document };
            dispatch.OnEnter.Add(Narrate("Esa noche, un observador militar británico escribe lo que ha visto."));
            dispatch.Transitions.Add(new MissionTransition(MissionCondition.Flag(Flags.DocumentClosed), MissionScript.CompleteStage));
            script.Stages.Add(dispatch);

            // ---- Fracasos -------------------------------------------------------------------------------------------
            MissionCondition ashore = MissionCondition.Any(
                MissionCondition.AtLeast(Facts.BoatsLanded, boats), MissionCondition.Flag(Flags.EmbankmentTaken));
            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.WaterBlown), "Los estanques han volado: sin agua, el ejército no puede internarse en el desierto."));
            script.Failures.Add(new MissionFailure(MissionCondition.All(ashore, noneLeft), "No queda nadie en tierra: el desembarco ha fracasado."));
            return script;
        }

        /// <summary>Intensidad de fuego sobre un bote: los fuertes activos a su alcance y los nidos de fusileros.</summary>
        public static float FireOnBoat(CoastalFort[] forts, float boatX, float boatZ, float nestIntensity)
        {
            float total = nestIntensity;
            if (forts == null) return total;
            foreach (CoastalFort fort in forts)
            {
                float dx = fort.X - boatX, dz = fort.Z - boatZ;
                total += fort.IntensityOn((float)Math.Sqrt(dx * dx + dz * dz));
            }
            return total;
        }
    }
}
