using System;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 7, «Cadenas rotas» (diciembre de 1880, cañaverales de Cañete), según <c>Historia_Completa_Guion.md</c> y
    /// el GDD: Quintín Quintana, líder de los culíes chinos liberados. Acto I, el silencio en la noche (cortar los
    /// alambres de los «torpedos de tierra» para abrir una senda); acto II, el vínculo de sangre (abatir con el machete a
    /// los que vienen a rematar a un soldado chileno herido y llevarlo a cubierto bajo el fuego); acto III, el asalto con
    /// fuego (la Winchester y la dinamita contra el portón de la hacienda fortificada). El prólogo del corresponsal se lee
    /// del guion.
    /// <para>
    /// El guion no pone frases entre comillas en boca de los personajes: todo lo demás es narración. No hay coleccionable:
    /// el contrato de servidumbre de Quintín que pide el guion no está en el Archivo Histórico (ver DEV_LOG).
    /// </para>
    /// </summary>
    public static class CaneteChapter
    {
        public const string Id = "cap7_canete";
        public const string ChapterHeading = "CAPÍTULO 7";

        /// <summary>Torpedos de tierra que cortar para abrir la senda. Diseño.</summary>
        public const int Mines = 5;
        /// <summary>Tiradores que salen de la hacienda a rematar al herido.</summary>
        public const int Finishers = 2;
        /// <summary>Defensores de la hacienda fortificada que hay que abatir tras volar el portón.</summary>
        public const int HaciendaDefenders = 8;
        /// <summary>Mecha de la dinamita del portón (s): tiempo para ponerse a cubierto.</summary>
        public const float GateFuseSeconds = 6f;

        /// <summary>
        /// De noche, un fusilero solo ve para apuntar a quien tiene a esta distancia (m). Diseño: con la vista de día (70 m),
        /// seis Remington desde la tapia acertarían a un hombre al descubierto a 60 m el 40 % de los tiros y dos impactos
        /// lo matan; cruzar el campo de tiro sería imposible (DEV_LOG 6.9).
        /// </summary>
        public const float NightSightM = 35f;
        /// <summary>Los dos tiradores van a por el herido y no ven a quien se les acerca agachado hasta tenerlo encima (m).</summary>
        public const float FinisherSightM = 8f;

        public static class Facts
        {
            public const string MinesDisarmed = "torpedos_desactivados";
            public const string FinishersDown = "tiradores_abatidos";
            public const string DefendersDown = "defensores_abatidos";
        }

        public static class Flags
        {
            /// <summary>Escena: Quintín ha cruzado un alambre armado.</summary>
            public const string MineTriggered = "torpedo_pisado";
            /// <summary>Guion → escena: la patrulla chilena impaciente pisa una mina fuera de la senda.</summary>
            public const string PatrolBlast = "estalla_la_patrulla";
            public const string WoundedCarried = "herido_a_cuestas";
            public const string WoundedSafe = "herido_a_cubierto";
            public const string WoundedKilled = "herido_rematado";
            public const string WoundedBledOut = "herido_desangrado";
            /// <summary>Guion → escena: los culíes y los soldados chilenos se suman al asalto.</summary>
            public const string AlliesJoin = "se_suman_al_asalto";
            public const string WinchesterTaken = "winchester_recogida";
            public const string DynamiteTaken = "dinamita_recogida";
            public const string GateBlown = "porton_volado";
            public const string PlayerDown = "quintin_cae";
        }

        public static class Stages
        {
            public const string Prologue = "c0_prologo";
            public const string Silence = "c1_el_silencio_en_la_noche";
            public const string Blood = "c2_el_vinculo_de_sangre";
            public const string Assault = "c3_el_asalto_con_fuego";
            public const string Dawn = "c4_el_amanecer";
        }

        public static class Perspectives
        {
            public const string Plate = "lamina";
            public const string Quintin = "quintin_quintana";
        }

        public const string Correspondent = "El corresponsal";

        public static MissionScript Build(Func<string, string> readRepositoryFile) => Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading));

        public static MissionScript Build(GuionQuotes guion)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));
            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);
            MissionCondition idle = MissionCondition.DialogueIdle();

            var script = new MissionScript { Id = Id, Title = "Cadenas rotas", Date = "Diciembre de 1880", Location = "Cañaverales de Cañete" };

            // ---- Prólogo ------------------------------------------------------------------------------------
            var prologue = new MissionStage { Id = Stages.Prologue, Title = "Los olvidados", Perspective = Perspectives.Plate };
            prologue.OnEnter.Add(Narrate("Diciembre de 1880. Valle de Cañete, al sur de Lima. La expedición chilena avanza hacia la capital."));
            prologue.OnEnter.Add(new MissionLine(Correspondent, guion.QuotedField("Prólogo del Corresponsal")));
            prologue.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.StageTime(1f), idle), Stages.Silence));
            script.Stages.Add(prologue);

            // ---- Acto I: el silencio en la noche -------------------------------------------------------------
            var silence = new MissionStage { Id = Stages.Silence, Title = "El silencio en la noche", Perspective = Perspectives.Quintin };
            silence.OnEnter.Add(Narrate("Quintín Quintana avanza entre los cañaverales. La luna alumbra apenas las acequias."));
            silence.OnEnter.Add(Narrate("Los defensores han sembrado la senda de torpedos de tierra: cajas de dinamita con un alambre oculto en la arena."));
            silence.OnEnter.Add(Hint("C: agacharse. Agachado, el gancho de caña descubre los alambres a 4 m. Junto a la carga, quieto y agachado, mantén E para cortarlo."));
            silence.Objectives.Add(new MissionObjective
            {
                Id = "senda", Text = "Abre una senda segura para la columna",
                Complete = MissionCondition.AtLeast(Facts.MinesDisarmed, Mines), ProgressFact = Facts.MinesDisarmed, ProgressTarget = Mines,
            });
            var first = new MissionTrigger { Id = "primer_alambre", When = MissionCondition.AtLeast(Facts.MinesDisarmed, 1) };
            first.Lines.Add(Narrate("El alambre cede sin un ruido. Quintín deja una caña clavada junto a la carga: aquí ya se puede pasar."));
            silence.Triggers.Add(first);
            silence.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.AtLeast(Facts.MinesDisarmed, Mines), idle), Stages.Blood));
            script.Stages.Add(silence);

            // ---- Acto II: el vínculo de sangre --------------------------------------------------------------
            var blood = new MissionStage { Id = Stages.Blood, Title = "El vínculo de sangre", Perspective = Perspectives.Quintin };
            blood.Triggers.Add(new MissionTrigger { Id = "la_patrulla", When = MissionCondition.StageTime(2f), SetsFlag = Flags.PatrolBlast });
            var alarm = new MissionTrigger { Id = "fuego_cruzado", When = MissionCondition.Flag(Flags.PatrolBlast), Interrupts = true };
            alarm.Lines.Add(Narrate("Una patrulla chilena impaciente se sale de la senda. Una mina estalla en la oscuridad."));
            alarm.Lines.Add(Narrate("Desde los muros de la hacienda, los fusileros peruanos abren fuego cruzado."));
            alarm.Lines.Add(Narrate("Un joven soldado chileno queda malherido en medio del campo de tiro, gritando de dolor. Dos tiradores salen a rematarlo."));
            alarm.Lines.Add(Hint("G: tajo de machete. Llega hasta él, mantén E a su lado para cargarlo y llévalo a cubierto, a la acequia."));
            blood.Triggers.Add(alarm);
            blood.Objectives.Add(new MissionObjective
            {
                Id = "tiradores", Text = "Abate a los que vienen a rematar al herido",
                Complete = MissionCondition.AtLeast(Facts.FinishersDown, Finishers), ProgressFact = Facts.FinishersDown, ProgressTarget = Finishers,
            });
            blood.Objectives.Add(new MissionObjective { Id = "herido", Text = "Lleva al herido a cubierto", Complete = MissionCondition.Flag(Flags.WoundedSafe) });
            var carried = new MissionTrigger { Id = "a_cuestas", When = MissionCondition.Flag(Flags.WoundedCarried) };
            carried.Lines.Add(Narrate("Quintín se carga al soldado a la espalda y corre agachado entre la caña bajo una granizada de balas."));
            blood.Triggers.Add(carried);
            var bond = new MissionTrigger { Id = "el_vinculo", When = MissionCondition.Flag(Flags.WoundedSafe), Interrupts = true };
            bond.Lines.Add(Narrate("En la acequia, el soldado chileno, con los ojos desorbitados, le sostiene la mano con lágrimas de agradecimiento."));
            bond.Lines.Add(Narrate("El muro del prejuicio y de la desconfianza se derrumba en un segundo."));
            blood.Triggers.Add(bond);
            blood.Transitions.Add(new MissionTransition(
                MissionCondition.All(MissionCondition.Flag(Flags.WoundedSafe), MissionCondition.AtLeast(Facts.FinishersDown, Finishers), idle), Stages.Assault));
            script.Stages.Add(blood);

            // ---- Acto III: el asalto con fuego -----------------------------------------------------------------
            var assault = new MissionStage { Id = Stages.Assault, Title = "El asalto con fuego", Perspective = Perspectives.Quintin };
            assault.Triggers.Add(new MissionTrigger { Id = "se_suman", When = MissionCondition.StageTime(0f), SetsFlag = Flags.AlliesJoin });
            assault.OnEnter.Add(Narrate("Los culíes liberados y los compañeros del herido se agrupan detrás de Quintín."));
            assault.OnEnter.Add(Hint("E: recoge la carabina Winchester y los cartuchos de dinamita. Pon la carga en el portón y apártate."));
            assault.Objectives.Add(new MissionObjective { Id = "winchester", Text = "Recoge la Winchester 1873", Complete = MissionCondition.Flag(Flags.WinchesterTaken) });
            assault.Objectives.Add(new MissionObjective { Id = "dinamita", Text = "Recoge la dinamita", Complete = MissionCondition.Flag(Flags.DynamiteTaken) });
            assault.Objectives.Add(new MissionObjective { Id = "porton", Text = "Vuela el portón de la hacienda", Complete = MissionCondition.Flag(Flags.GateBlown) });
            assault.Objectives.Add(new MissionObjective
            {
                Id = "nido", Text = "Neutraliza el nido de resistencia",
                Complete = MissionCondition.AtLeast(Facts.DefendersDown, HaciendaDefenders), ProgressFact = Facts.DefendersDown, ProgressTarget = HaciendaDefenders,
            });
            var gate = new MissionTrigger { Id = "porton", When = MissionCondition.Flag(Flags.GateBlown) };
            gate.Lines.Add(Narrate("La dinamita revienta los portones de madera. Culíes y soldados chilenos entran juntos por la brecha."));
            assault.Triggers.Add(gate);
            assault.Transitions.Add(new MissionTransition(
                MissionCondition.All(MissionCondition.Flag(Flags.GateBlown), MissionCondition.AtLeast(Facts.DefendersDown, HaciendaDefenders), idle), Stages.Dawn));
            script.Stages.Add(assault);

            // ---- El amanecer -----------------------------------------------------------------------------------
            var dawn = new MissionStage { Id = Stages.Dawn, Title = "Cadenas rotas", Perspective = Perspectives.Quintin };
            dawn.OnEnter.Add(Narrate("Amanece sobre la hacienda. En los galpones, los candados de las cadenas cuelgan rotos."));
            dawn.OnEnter.Add(Narrate("Cientos de culíes liberados se suman a la columna como zapadores y vanguardia, camino de Lima."));
            dawn.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.StageTime(1f), idle), MissionScript.CompleteStage));
            script.Stages.Add(dawn);

            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.MineTriggered), "Quintín ha cruzado un alambre: la mina estalla en el cañaveral."));
            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.WoundedKilled), "Han rematado al soldado herido."));
            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.WoundedBledOut), "El soldado se ha desangrado en el campo de tiro."));
            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.PlayerDown), "Quintín Quintana ha caído."));
            return script;
        }
    }
}
