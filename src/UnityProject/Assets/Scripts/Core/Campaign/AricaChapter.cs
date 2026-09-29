using System;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 6, «Hasta el último cartucho» (7 de junio de 1880, Morro de Arica), según
    /// <c>Historia_Completa_Guion.md</c> y el GDD: la junta de oficiales y la respuesta de Bolognesi (leída del guion);
    /// el asalto sin preparación artillera a las 5:30; la defensa de los parapetos de caliza con la Gatling; los
    /// cables de las minas cortados; la retirada disparando hasta la explanada; y, en la cima del abismo, la caída de
    /// Bolognesi y el salto de Ugarte con la bandera. En la cima, caer no es un fracaso: es el destino de la guarnición.
    /// </summary>
    public static class AricaChapter
    {
        public const string Id = "cap6_morro_de_arica";
        public const string ChapterHeading = "CAPÍTULO 6";

        /// <summary>Asaltantes que hay que rechazar en el parapeto antes de que la línea se rompa.</summary>
        public const int ParapetEnemies = 12;
        /// <summary>Si no se rechazan antes, el parapeto cae igualmente (el asalto real duró 55 minutos).</summary>
        public const float ParapetHoldSeconds = 150f;
        /// <summary>Segundos en la cima hasta el salto de Ugarte y el final.</summary>
        public const float BolognesiFallsAt = 20f;
        public const float UgarteLeapsAt = 45f;
        public const float SummitSeconds = 75f;

        public static class Facts
        {
            public const string EnemiesDown = "asaltantes_fuera_de_combate";
        }

        public static class Flags
        {
            public const string DetonatorTried = "detonador_probado";
            public const string AtSummit = "en_la_explanada";
            public const string PlayerDown = "manuel_salazar_cae";
            public const string BolognesiFalls = "cae_bolognesi";
            public const string UgarteLeaps = "salto_de_ugarte";
        }

        public static class Stages
        {
            public const string Council = "m0_la_junta";
            public const string Parapet = "m1_parapetos_de_caliza";
            public const string Withdraw = "m2_hacia_la_explanada";
            public const string Summit = "m3_la_cima_del_abismo";
        }

        public static class Perspectives
        {
            /// <summary>La junta: una lámina, sin mandos.</summary>
            public const string Council = "junta";
            public const string Soldier = "manuel_salazar";
        }

        public const string Bolognesi = "Francisco Bolognesi";

        public static MissionScript Build(Func<string, string> readRepositoryFile) => Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading));

        public static MissionScript Build(GuionQuotes guion)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));
            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);
            MissionCondition down = MissionCondition.Flag(Flags.PlayerDown);

            var script = new MissionScript
            {
                Id = Id, Title = "Hasta el último cartucho", Date = "7 de junio de 1880", Location = "Morro de Arica",
                // El reloj de Bolognesi del guion no está en el Archivo; el catálogo asigna a este capítulo el despacho de
                // Spenser St. John sobre Arica.
                RewardCollectibleId = "despacho_3_arica",
            };

            var council = new MissionStage { Id = Stages.Council, Title = "La junta de oficiales", Perspective = Perspectives.Council };
            council.OnEnter.Add(Narrate("Arica. La junta de oficiales peruanos. El mayor chileno Salvo pide la rendición incondicional."));
            council.OnEnter.Add(Narrate("6.000 chilenos contra menos de 1.900 peruanos, cercados por tierra y por mar."));
            council.OnEnter.Add(Narrate("El anciano coronel Francisco Bolognesi, de 63 años, responde con calma pétrea:"));
            council.OnEnter.Add(new MissionLine(Bolognesi, guion.Find("Tengo deberes sagrados")));
            council.Transitions.Add(new MissionTransition(MissionCondition.DialogueIdle(), Stages.Parapet));
            script.Stages.Add(council);

            var parapet = new MissionStage { Id = Stages.Parapet, Title = "Los parapetos de caliza", Perspective = Perspectives.Soldier };
            parapet.OnEnter.Add(Narrate("7 de junio de 1880, 5:30 de la madrugada. Soldado Manuel Salazar, batallón Artesanos de Tacna."));
            parapet.OnEnter.Add(Narrate("Sin preparación de artillería, el 3.º y el 4.º de Línea asaltan los fuertes del bajo y trepan por las escarpas."));
            parapet.OnEnter.Add(Hint("Defiende los sacos y el parapeto. Clic derecho: encarar. Clic: fuego. R: cargar. F: bayoneta."));
            parapet.Objectives.Add(new MissionObjective
            {
                Id = "parapeto", Text = "Rechaza el asalto al parapeto",
                Complete = MissionCondition.AtLeast(Facts.EnemiesDown, ParapetEnemies), ProgressFact = Facts.EnemiesDown, ProgressTarget = ParapetEnemies,
            });
            parapet.Objectives.Add(new MissionObjective
            {
                Id = "minas", Text = "Acciona el detonador de las minas (E)", Optional = true, Complete = MissionCondition.Flag(Flags.DetonatorTried),
            });
            var engineers = new MissionTrigger { Id = "ingenieros", When = MissionCondition.StageTime(35f) };
            engineers.Lines.Add(Narrate("Los ingenieros intentan volar los polvorines y las minas eléctricas desde el detonador del parapeto."));
            parapet.Triggers.Add(engineers);
            var cut = new MissionTrigger { Id = "cables_cortados", When = MissionCondition.Flag(Flags.DetonatorTried), Interrupts = true };
            cut.Lines.Add(Narrate("Nada. Los asaltantes han cortado los cables en el asalto."));
            parapet.Triggers.Add(cut);
            parapet.Transitions.Add(new MissionTransition(
                MissionCondition.Any(MissionCondition.AtLeast(Facts.EnemiesDown, ParapetEnemies), MissionCondition.StageTime(ParapetHoldSeconds)),
                Stages.Withdraw));
            script.Stages.Add(parapet);

            var withdraw = new MissionStage { Id = Stages.Withdraw, Title = "Hacia la explanada", Perspective = Perspectives.Soldier };
            withdraw.OnEnter.Add(Narrate("El parapeto está rebasado. Los asaltantes coronan las escarpas a una velocidad increíble."));
            withdraw.OnEnter.Add(Hint("Retrocede disparando hasta la explanada superior del Morro."));
            withdraw.Objectives.Add(new MissionObjective { Id = "explanada", Text = "Retrocede disparando hasta la explanada", Complete = MissionCondition.Flag(Flags.AtSummit) });
            withdraw.Transitions.Add(new MissionTransition(MissionCondition.Flag(Flags.AtSummit), Stages.Summit));
            script.Stages.Add(withdraw);

            var summit = new MissionStage { Id = Stages.Summit, Title = "La cima del abismo", Perspective = Perspectives.Soldier };
            summit.OnEnter.Add(Narrate("La explanada superior, a metros del acantilado que cae a pico sobre el mar embravecido."));
            summit.Objectives.Add(new MissionObjective { Id = "bandera", Text = "Resiste junto a la bandera", Complete = MissionCondition.StageTime(SummitSeconds) });
            var bolognesi = new MissionTrigger { Id = "bolognesi", When = MissionCondition.StageTime(BolognesiFallsAt), SetsFlag = Flags.BolognesiFalls };
            bolognesi.Lines.Add(Narrate("Bolognesi cae abatido de un disparo en el corazón, junto a sus oficiales."));
            summit.Triggers.Add(bolognesi);
            var ugarte = new MissionTrigger { Id = "ugarte", When = MissionCondition.StageTime(UgarteLeapsAt), SetsFlag = Flags.UgarteLeaps };
            ugarte.Lines.Add(Narrate("El coronel Alfonso Ugarte, herido y acorralado, toma la bandera, espolea a su caballo y se lanza al vacío."));
            ugarte.Lines.Add(Narrate("El pabellón no cae en manos del enemigo."));
            summit.Triggers.Add(ugarte);
            // En la cima, caer no es fracasar: es el final de la guarnición, y el capítulo termina igual.
            summit.Transitions.Add(new MissionTransition(
                MissionCondition.Any(down, MissionCondition.All(MissionCondition.StageTime(SummitSeconds), MissionCondition.DialogueIdle())),
                MissionScript.CompleteStage));
            script.Stages.Add(summit);

            script.Failures.Add(new MissionFailure(MissionCondition.All(down, MissionCondition.NotFlag(Flags.AtSummit)),
                "Manuel Salazar cae en las escarpas antes de llegar a la explanada."));
            return script;
        }
    }
}
