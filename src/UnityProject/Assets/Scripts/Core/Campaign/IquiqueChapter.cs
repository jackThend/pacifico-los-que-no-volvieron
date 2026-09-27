using System;
using Pacifico.Core.Narrative;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 1, «Madera y blindaje» (21 de mayo de 1879, rada de Iquique): el guion de la misión según
    /// <c>Historia_Completa_Guion.md</c> y el GDD. Acto I a bordo de la Esmeralda (el grumete Wenceslao Vargas en
    /// la batería de estribor), Acto II en la torre Coles del Huáscar (el cabo Dámaso Antúnez), el rescate de los
    /// náufragos y la carta de Grau a Carmela Carvajal. Las frases de Prat y de Grau se leen del guion.
    /// </summary>
    public static class IquiqueChapter
    {
        public const string Id = "cap1_iquique";
        public const string ChapterHeading = "CAPÍTULO 1";

        /// <summary>Grupos de náufragos que hay que recoger. Licencia de diseño (los rescatados fueron decenas).</summary>
        public const int DefaultSurvivorGroups = 6;
        /// <summary>Si el jugador dispara tantas veces sobre los náufragos, Grau lo releva de la pieza.</summary>
        public const int ShotsAfterSinkingToFail = 3;
        /// <summary>Si el jugador no dispara, la historia sigue igualmente (s).</summary>
        public const float BroadsideStageTimeoutSeconds = 240f;
        public const float TurretStageTimeoutSeconds = 150f;

        /// <summary>Hechos que escribe la escena.</summary>
        public static class Facts
        {
            public const string Broadsides = "andanadas_esmeralda";
            public const string BroadsideHits = "impactos_esmeralda_en_huascar";
            public const string ShoreHits = "impactos_baterias_tierra";
            public const string Rams = "espolonazos";
            public const string TurretShots = "disparos_torre";
            public const string TurretHits = "impactos_torre";
            public const string WaterlineHits = "impactos_linea_de_flotacion";
            public const string ShotsAfterSinking = "disparos_tras_hundimiento";
            public const string SurvivorsRescued = "naufragos_rescatados";
        }

        /// <summary>Marcas: las primeras las pone la escena; las demás, el guion para que la escena reaccione.</summary>
        public static class Flags
        {
            public const string EsmeraldaSunk = "esmeralda_hundida";
            public const string HuascarSunk = "huascar_hundido";
            public const string DocumentClosed = "carta_cerrada";
            /// <summary>Prat ha saltado al abordaje: la escena cambia de perspectiva.</summary>
            public const string PratBoards = "prat_al_abordaje";
            /// <summary>Tercer espolonazo: la Esmeralda se va a pique (desenlace histórico).</summary>
            public const string FounderEsmeralda = "esmeralda_a_pique";
        }

        /// <summary>Etapas.</summary>
        public static class Stages
        {
            public const string WoodenDeck = "a1_cubierta_de_madera";
            public const string Ram = "a2_espolon";
            public const string Turret = "b1_torre_coles";
            public const string ThirdRam = "b2_tercer_espolonazo";
            public const string Survivors = "b3_naufragos";
            public const string Letter = "intermision_carta";
        }

        /// <summary>Qué controla el jugador en cada etapa.</summary>
        public static class Perspectives
        {
            public const string EsmeraldaBattery = "esmeralda_bateria";
            public const string HuascarTurret = "huascar_torre";
            public const string HuascarBridge = "huascar_puente";
            public const string Document = "documento";
        }

        private const string HarangueGiven = "arenga_dicha";

        public const string Prat = "Arturo Prat";
        public const string Grau = "Miguel Grau";

        public static MissionScript Build(Func<string, string> readRepositoryFile, int survivorGroups = DefaultSurvivorGroups) =>
            Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading), survivorGroups);

        public static MissionScript Build(GuionQuotes guion, int survivorGroups = DefaultSurvivorGroups)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));
            if (survivorGroups < 1) throw new ArgumentOutOfRangeException(nameof(survivorGroups));

            MissionLine Say(string who, string prefix) => new MissionLine(who, guion.Find(prefix));
            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);
            MissionCondition sunk = MissionCondition.Flag(Flags.EsmeraldaSunk);

            var script = new MissionScript
            {
                Id = Id,
                Title = "Madera y blindaje",
                Date = "21 de mayo de 1879",
                Location = "Rada de Iquique",
                RewardCollectibleId = CollectibleCatalog.GrauLetterId,
            };

            // ---- Acto I: la cubierta de madera (Esmeralda) ------------------------------------------------
            var deck = new MissionStage { Id = Stages.WoodenDeck, Title = "La cubierta de madera", Perspective = Perspectives.EsmeraldaBattery };
            deck.OnEnter.Add(Narrate("21 de mayo de 1879, rada de Iquique. Corbeta Esmeralda. Grumete Wenceslao Vargas, batería de 40 libras."));
            deck.OnEnter.Add(Narrate("El monitor Huáscar emerge de la bruma. Hacia el sur, la Independencia persigue a la Covadonga."));
            deck.OnEnter.Add(Hint("Espacio: andanada. Dispara cuando la cubierta pase por la horizontal: si escora, el tiro sale corto o largo."));
            deck.Objectives.Add(new MissionObjective
            {
                Id = "andanadas", Text = "Dispara las andanadas de estribor contra el Huáscar",
                Complete = MissionCondition.AtLeast(Facts.Broadsides, 3), ProgressFact = Facts.Broadsides, ProgressTarget = 3,
            });
            deck.Objectives.Add(new MissionObjective
            {
                Id = "blanco", Text = "Alcanza al Huáscar", Optional = true, Complete = MissionCondition.AtLeast(Facts.BroadsideHits, 1),
            });
            var harangue = new MissionTrigger { Id = "arenga_de_prat", When = MissionCondition.StageTime(6f), SetsFlag = HarangueGiven };
            harangue.Lines.Add(Say(Prat, "¡Muchachos"));
            deck.Triggers.Add(harangue);
            var shore = new MissionTrigger { Id = "baterias_de_tierra", When = MissionCondition.AtLeast(Facts.ShoreHits, 1) };
            shore.Lines.Add(Narrate("Las baterías de tierra de Iquique alcanzan a la Esmeralda: Prat ordena salir al centro de la bahía."));
            deck.Triggers.Add(shore);
            var bounce = new MissionTrigger { Id = "rebote", When = MissionCondition.AtLeast(Facts.BroadsideHits, 1) };
            bounce.Lines.Add(Narrate("Las balas de 40 libras rebotan en la coraza de hierro del Huáscar entre chispas."));
            deck.Triggers.Add(bounce);
            deck.Transitions.Add(new MissionTransition(sunk, Stages.Survivors));
            deck.Transitions.Add(new MissionTransition(
                MissionCondition.Any(
                    MissionCondition.All(MissionCondition.AtLeast(Facts.Broadsides, 3), MissionCondition.Flag(HarangueGiven), MissionCondition.DialogueIdle()),
                    MissionCondition.StageTime(BroadsideStageTimeoutSeconds),
                    MissionCondition.AtLeast(Facts.Rams, 1)),
                Stages.Ram));
            script.Stages.Add(deck);

            // ---- Acto I: el espolón ---------------------------------------------------------------------------
            var ram = new MissionStage { Id = Stages.Ram, Title = "El espolón", Perspective = Perspectives.EsmeraldaBattery };
            ram.OnEnter.Add(Narrate("El Huáscar vira y arremete contra la Esmeralda con el espolón por delante."));
            ram.OnEnter.Add(Hint("Sigue disparando mientras se acerca."));
            ram.Objectives.Add(new MissionObjective
            {
                Id = "resistir", Text = "Resiste la embestida del Huáscar", Complete = MissionCondition.AtLeast(Facts.Rams, 1),
            });
            var boarding = new MissionTrigger
            {
                Id = "al_abordaje", When = MissionCondition.AtLeast(Facts.Rams, 1), Interrupts = true, SetsFlag = Flags.PratBoards,
            };
            boarding.Lines.Add(Narrate("El espolón parte las cuadernas de babor. El agua entra a raudales."));
            boarding.Lines.Add(Say(Prat, "¡Al abordaje"));
            boarding.Lines.Add(Narrate("Prat salta a la cubierta del Huáscar, seguido solo por el sargento Juan de Dios Aldea y un marinero."));
            boarding.Lines.Add(Narrate("El comandante cae sobre la plancha de hierro del monitor."));
            ram.Triggers.Add(boarding);
            ram.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.PratBoards), MissionCondition.DialogueIdle()), Stages.Turret));
            ram.Transitions.Add(new MissionTransition(MissionCondition.All(sunk, MissionCondition.DialogueIdle()), Stages.Survivors));
            script.Stages.Add(ram);

            // ---- Acto II: la torre giratoria (Huáscar) -------------------------------------------------------
            var turret = new MissionStage { Id = Stages.Turret, Title = "La torre giratoria", Perspective = Perspectives.HuascarTurret };
            turret.OnEnter.Add(Narrate("Torre Coles del Huáscar. Cabo Dámaso Antúnez: dieciséis artilleros cargan el cañón Armstrong de 300 libras."));
            turret.OnEnter.Add(Say(Grau, "¡Fuego a la línea"));
            turret.OnEnter.Add(Hint("Ratón: girar la torre. Clic: fuego. R/F: convergencia. Mayús: telémetro."));
            turret.Objectives.Add(new MissionObjective
            {
                Id = "flotacion", Text = "Alcanza la línea de flotación de la Esmeralda", Complete = MissionCondition.AtLeast(Facts.WaterlineHits, 1),
            });
            var shell = new MissionTrigger { Id = "impacto", When = MissionCondition.AtLeast(Facts.TurretHits, 1) };
            shell.Lines.Add(Narrate("La granada de 300 libras atraviesa el costado de madera de la Esmeralda."));
            turret.Triggers.Add(shell);
            turret.Transitions.Add(new MissionTransition(sunk, Stages.Survivors));
            turret.Transitions.Add(new MissionTransition(
                MissionCondition.Any(MissionCondition.AtLeast(Facts.WaterlineHits, 1), MissionCondition.StageTime(TurretStageTimeoutSeconds)),
                Stages.ThirdRam));
            script.Stages.Add(turret);

            var third = new MissionStage { Id = Stages.ThirdRam, Title = "El tercer espolonazo", Perspective = Perspectives.HuascarTurret };
            third.OnEnter.Add(Narrate("Grau ordena embestir de nuevo. La Esmeralda sigue disparando con sus últimos cañones a flor de agua."));
            third.Objectives.Add(new MissionObjective
            {
                Id = "espolonazos", Text = "Mantén el fuego mientras el Huáscar embiste",
                Complete = MissionCondition.AtLeast(Facts.Rams, 3), ProgressFact = Facts.Rams, ProgressTarget = 3,
            });
            var second = new MissionTrigger { Id = "segundo_espolonazo", When = MissionCondition.AtLeast(Facts.Rams, 2) };
            second.Lines.Add(Narrate("Segundo espolonazo. La corbeta escora, pero su pabellón sigue arriba."));
            third.Triggers.Add(second);
            third.Triggers.Add(new MissionTrigger { Id = "a_pique", When = MissionCondition.AtLeast(Facts.Rams, 3), SetsFlag = Flags.FounderEsmeralda });
            third.Transitions.Add(new MissionTransition(sunk, Stages.Survivors));
            script.Stages.Add(third);

            // ---- El cierre humano ----------------------------------------------------------------------------
            var rescue = new MissionStage { Id = Stages.Survivors, Title = "Salvad a esos náufragos", Perspective = Perspectives.HuascarBridge };
            rescue.OnEnter.Add(Narrate("El guardiamarina Riquelme dispara el último cañonazo. La Esmeralda se hunde con el pabellón al tope."));
            rescue.OnEnter.Add(Narrate("Silencio en la bahía. Entre los restos de madera, decenas de marineros chilenos luchan por no ahogarse."));
            rescue.OnEnter.Add(Narrate("Algunos artilleros alzan sus fusiles hacia los náufragos. Grau sale de la torre de mando."));
            rescue.OnEnter.Add(Say(Grau, "¡Fuego no!"));
            rescue.OnEnter.Add(Hint("W/S: máquina. A/D: timón. Acércate despacio a cada grupo de náufragos para arriar los botes."));
            rescue.Objectives.Add(new MissionObjective
            {
                Id = "rescate", Text = "Recoge a los náufragos de la Esmeralda",
                Complete = MissionCondition.AtLeast(Facts.SurvivorsRescued, survivorGroups), ProgressFact = Facts.SurvivorsRescued, ProgressTarget = survivorGroups,
            });
            var rebuke = new MissionTrigger { Id = "reprimenda", When = MissionCondition.AtLeast(Facts.ShotsAfterSinking, 1), Interrupts = true };
            rebuke.Lines.Add(new MissionLine(Grau, guion.FirstSentence("¡Fuego no!")));
            rebuke.Lines.Add(Hint("La orden es rescatar. Si vuelves a disparar, Grau te relevará de la pieza."));
            rescue.Triggers.Add(rebuke);
            var shiver = new MissionTrigger { Id = "a_bordo", When = MissionCondition.AtLeast(Facts.SurvivorsRescued, 1) };
            shiver.Lines.Add(Narrate("Suben por la borda muchachos chilenos, tiritando y empapados de salmuera. No hay odio en sus ojos."));
            rescue.Triggers.Add(shiver);
            rescue.Transitions.Add(new MissionTransition(
                MissionCondition.All(MissionCondition.AtLeast(Facts.SurvivorsRescued, survivorGroups), MissionCondition.DialogueIdle()),
                Stages.Letter));
            script.Stages.Add(rescue);

            // ---- Intermisión: la carta de la piedad ---------------------------------------------------------
            var letter = new MissionStage { Id = Stages.Letter, Title = "La carta de la piedad", Perspective = Perspectives.Document };
            letter.OnEnter.Add(Narrate("Camarote de Grau. Días después, el comandante escribe a la viuda de Prat."));
            letter.Transitions.Add(new MissionTransition(MissionCondition.Flag(Flags.DocumentClosed), MissionScript.CompleteStage));
            script.Stages.Add(letter);

            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.HuascarSunk), "El Huáscar se ha hundido."));
            script.Failures.Add(new MissionFailure(MissionCondition.AtLeast(Facts.ShotsAfterSinking, ShotsAfterSinkingToFail),
                "Grau te releva de la pieza: la orden era salvar a los náufragos."));
            return script;
        }
    }
}
