using System;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 5, «El trueno de Intiorko» (26 de mayo de 1880, Alto de la Alianza, Tacna), según
    /// <c>Historia_Completa_Guion.md</c> y el GDD: el subteniente Daniel Ballivián al mando del sector central y la
    /// reserva boliviana. Despliegue en la trinchera bajo la camanchaca, el avance chileno con la artillería Krupp, la
    /// carga de los Colorados de Bolivia (el grito se lee del guion) para recuperar los cañones y la retirada
    /// escalonada para salvar a los heridos cuando la caballería y las reservas chilenas cierran la tenaza.
    /// </summary>
    public static class AltoDeLaAlianzaChapter
    {
        public const string Id = "cap5_alto_de_la_alianza";
        public const string ChapterHeading = "CAPÍTULO 5";

        /// <summary>Segundos de camanchaca antes de que se levante (compresión del alba).</summary>
        public const float FogSeconds = 90f;
        /// <summary>Tiempo máximo que se aguanta en la trinchera antes de que ceda la izquierda.</summary>
        public const float HoldSeconds = 150f;
        /// <summary>Bajas chilenas con las que el avance se detiene y el mando ordena el contraataque.</summary>
        public const int EnemyCasualtiesToStall = 15;
        public const int GunsToRetake = 2;
        public const float ChargeStageTimeoutSeconds = 240f;
        /// <summary>
        /// Tiempo que tarda en cerrarse la tenaza (s): lo que se tarda al paso desde los cañones de la izquierda hasta la
        /// retaguardia (≈ 240 m a 1,4 m/s), con poco margen.
        /// </summary>
        public const float RetreatSeconds = 240f;
        /// <summary>Hombres que hay que llevar a la retaguardia para salvar a los heridos.</summary>
        public const int DefaultMenToEvacuate = 12;

        public static class Facts
        {
            public const string SquadsEntrenched = "escuadras_en_la_trinchera";
            public const string EnemyDown = "bajas_chilenas";
            public const string GunsRetaken = "canones_recuperados";
            public const string MenEvacuated = "hombres_en_retaguardia";
            public const string MenInField = "hombres_en_el_campo";
        }

        public static class Flags
        {
            public const string FogLifted = "niebla_levantada";
            public const string LeftFlankBroken = "izquierda_cede";
            public const string ChargeOrdered = "carga_de_los_colorados";
            public const string Retreating = "retirada";
        }

        public static class Stages
        {
            public const string Fog = "c1_camanchaca";
            public const string Advance = "c2_avance_chileno";
            public const string Charge = "c3_carga_de_los_colorados";
            public const string Retreat = "c4_la_tenaza";
        }

        public const string Perspective = "daniel_ballivian";
        public const string Colorados = "Colorados de Bolivia";

        public static MissionScript Build(Func<string, string> readRepositoryFile, int playerSquads = 4, int menToEvacuate = DefaultMenToEvacuate) =>
            Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading), playerSquads, menToEvacuate);

        public static MissionScript Build(GuionQuotes guion, int playerSquads = 4, int menToEvacuate = DefaultMenToEvacuate)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));
            if (playerSquads < 1 || menToEvacuate < 1) throw new ArgumentOutOfRangeException();
            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);

            var script = new MissionScript
            {
                Id = Id, Title = "El trueno de Intiorko", Date = "26 de mayo de 1880", Location = "Alto de la Alianza (Tacna)",
                // El GDD pide el diario de un soldado paceño, que el Archivo no tiene; el catálogo asigna a este capítulo
                // la carta de Abraham Quiroz desde las dunas de Tacna (el otro lado de la misma batalla).
                RewardCollectibleId = "carta_quiroz_2_desierto",
            };

            var fog = new MissionStage { Id = Stages.Fog, Title = "La camanchaca", Perspective = Perspective };
            fog.OnEnter.Add(Narrate("26 de mayo de 1880. Meseta del Alto de la Alianza, Tacna. La batalla campal más grande de la guerra."));
            fog.OnEnter.Add(Narrate("20.000 soldados chilenos contra 12.000 aliados atrincherados en las dunas."));
            fog.OnEnter.Add(Narrate("Subteniente Daniel Ballivián, Colorados de Bolivia: el sector central y la reserva boliviana en las alturas del Intiorko."));
            fog.OnEnter.Add(Hint("Clic o recuadro: seleccionar. Clic derecho: mover (arrastrar: frente). Lleva las escuadras a la zanja antes de que se levante la niebla."));
            fog.Objectives.Add(new MissionObjective
            {
                Id = "trinchera", Text = "Despliega las escuadras en la trinchera",
                Complete = MissionCondition.AtLeast(Facts.SquadsEntrenched, playerSquads), ProgressFact = Facts.SquadsEntrenched, ProgressTarget = playerSquads,
            });
            fog.Transitions.Add(new MissionTransition(MissionCondition.StageTime(FogSeconds), Stages.Advance));
            script.Stages.Add(fog);

            var advance = new MissionStage { Id = Stages.Advance, Title = "El avance chileno", Perspective = Perspective };
            advance.Triggers.Add(new MissionTrigger { Id = "niebla", When = MissionCondition.StageTime(0f), SetsFlag = Flags.FogLifted });
            advance.OnEnter.Add(Narrate("La niebla se disipa. Las líneas chilenas avanzan en masa bajo el fuego implacable de la artillería Krupp de montaña."));
            advance.OnEnter.Add(Hint("Concentra el fuego: dos escuadras suprimen a una; en la zanja, aguantáis a dos."));
            advance.Objectives.Add(new MissionObjective
            {
                Id = "contener", Text = "Contén el avance desde la trinchera",
                Complete = MissionCondition.AtLeast(Facts.EnemyDown, EnemyCasualtiesToStall), ProgressFact = Facts.EnemyDown, ProgressTarget = EnemyCasualtiesToStall,
            });
            var flank = new MissionTrigger
            {
                Id = "la_izquierda_cede", SetsFlag = Flags.LeftFlankBroken,
                When = MissionCondition.Any(MissionCondition.AtLeast(Facts.EnemyDown, EnemyCasualtiesToStall), MissionCondition.StageTime(HoldSeconds)),
            };
            flank.Lines.Add(Narrate("La izquierda aliada cede ante la embestida chilena. Los chilenos toman sus cañones."));
            flank.Lines.Add(Narrate("Se da la orden histórica al batallón de élite boliviano."));
            advance.Triggers.Add(flank);
            advance.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.LeftFlankBroken), MissionCondition.DialogueIdle()), Stages.Charge));
            script.Stages.Add(advance);

            var charge = new MissionStage { Id = Stages.Charge, Title = "La carga de los Colorados", Perspective = Perspective };
            charge.OnEnter.Add(Hint("C (o el botón «¡A LA CARGA!»): los Colorados cargan a la bayoneta contra el enemigo más cercano."));
            charge.Objectives.Add(new MissionObjective { Id = "carga", Text = "Ordena la carga de los Colorados", Complete = MissionCondition.Flag(Flags.ChargeOrdered) });
            charge.Objectives.Add(new MissionObjective
            {
                Id = "canones", Text = "Recupera los cañones de la izquierda",
                Complete = MissionCondition.AtLeast(Facts.GunsRetaken, GunsToRetake), ProgressFact = Facts.GunsRetaken, ProgressTarget = GunsToRetake,
            });
            var cry = new MissionTrigger { Id = "temblad", When = MissionCondition.Flag(Flags.ChargeOrdered), Interrupts = true };
            cry.Lines.Add(Narrate("Estalla un huayno guerrero de tambores y quenas. Las casacas rojas avanzan al trote, con la bayoneta calada."));
            cry.Lines.Add(new MissionLine(Colorados, guion.Find("¡Temblad")));
            charge.Triggers.Add(cry);
            var firstGun = new MissionTrigger { Id = "primer_canon", When = MissionCondition.AtLeast(Facts.GunsRetaken, 1) };
            firstGun.Lines.Add(Narrate("Las primeras líneas chilenas, arrolladas. Un cañón vuelve a manos aliadas."));
            charge.Triggers.Add(firstGun);
            charge.Transitions.Add(new MissionTransition(
                MissionCondition.Any(
                    MissionCondition.All(MissionCondition.AtLeast(Facts.GunsRetaken, GunsToRetake), MissionCondition.DialogueIdle()),
                    MissionCondition.StageTime(ChargeStageTimeoutSeconds)),
                Stages.Retreat));
            script.Stages.Add(charge);

            var retreat = new MissionStage { Id = Stages.Retreat, Title = "La tenaza", Perspective = Perspective };
            retreat.Triggers.Add(new MissionTrigger { Id = "retirada", When = MissionCondition.StageTime(0f), SetsFlag = Flags.Retreating });
            retreat.OnEnter.Add(Narrate("La caballería y las reservas chilenas cierran la tenaza. La posición está rebasada."));
            retreat.OnEnter.Add(Hint("Retirada escalonada: unas escuadras cubren mientras las otras llevan a los heridos a la retaguardia, junto al depósito."));
            retreat.Objectives.Add(new MissionObjective
            {
                Id = "heridos", Text = "Lleva a los heridos a la retaguardia",
                Complete = MissionCondition.AtLeast(Facts.MenEvacuated, menToEvacuate), ProgressFact = Facts.MenEvacuated, ProgressTarget = menToEvacuate,
            });
            var epilogue = new MissionTrigger
            {
                Id = "ultima_batalla",
                When = MissionCondition.All(MissionCondition.AtLeast(Facts.MenEvacuated, menToEvacuate),
                                            MissionCondition.Any(MissionCondition.Not(MissionCondition.AtLeast(Facts.MenInField, 1)), MissionCondition.StageTime(RetreatSeconds))),
            };
            epilogue.Lines.Add(Narrate("Es la última batalla de Bolivia en la guerra. Su ejército se retira a las cumbres andinas."));
            epilogue.Lines.Add(Narrate("La alianza militar llega a su fin en el campo de batalla."));
            retreat.Triggers.Add(epilogue);
            retreat.Transitions.Add(new MissionTransition(
                MissionCondition.All(MissionCondition.AtLeast(Facts.MenEvacuated, menToEvacuate),
                                     MissionCondition.Any(MissionCondition.Not(MissionCondition.AtLeast(Facts.MenInField, 1)), MissionCondition.StageTime(RetreatSeconds)),
                                     MissionCondition.DialogueIdle()),
                MissionScript.CompleteStage));
            script.Stages.Add(retreat);

            // Fracasos: la tenaza se cierra sin haber salvado a los heridos, o no queda nadie.
            script.Failures.Add(new MissionFailure(
                MissionCondition.All(MissionCondition.Flag(Flags.Retreating), MissionCondition.StageTime(RetreatSeconds),
                                     MissionCondition.Not(MissionCondition.AtLeast(Facts.MenEvacuated, menToEvacuate))),
                "La tenaza se cierra: los heridos quedan en el campo."));
            script.Failures.Add(new MissionFailure(
                MissionCondition.All(MissionCondition.Not(MissionCondition.AtLeast(Facts.MenInField, 1)),
                                     MissionCondition.Not(MissionCondition.AtLeast(Facts.MenEvacuated, menToEvacuate))),
                "Los Colorados han caído hasta el último hombre."));
            return script;
        }
    }
}
