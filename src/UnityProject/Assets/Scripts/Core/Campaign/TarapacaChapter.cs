using System;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Capítulo 4, «Sed en la quebrada» (27 de noviembre de 1879, quebrada de Tarapacá), según
    /// <c>Historia_Completa_Guion.md</c> y el GDD: el soldado Mariano Santos, del batallón Zepita de Cáceres, sorprendido
    /// al amanecer; combate de casa en casa con los cartuchos contados (cuando se acaban los del Chassepot, un Comblain
    /// de un caído); la carga a la bayoneta contra los Krupp de la pampa; y, en la victoria sin agua, el tambor
    /// chileno herido que pide de beber. La frase de Cáceres se lee del guion.
    /// </summary>
    public static class TarapacaChapter
    {
        public const string Id = "cap4_tarapaca";
        public const string ChapterHeading = "CAPÍTULO 4";

        /// <summary>Chilenos de la vanguardia que hay que rechazar en el pueblo.</summary>
        public const int DefaultVillageEnemies = 12;
        public const int KruppGuns = 2;
        /// <summary>Con tan pocos cartuchos del arma empuñada se avisa de que hay que buscar otro fusil.</summary>
        public const int LowAmmoThreshold = 5;

        public static class Facts
        {
            public const string EnemiesDown = "chilenos_fuera_de_combate";
            public const string GunsTaken = "krupp_tomados";
            public const string Rounds = "cartuchos_del_arma";
        }

        public static class Flags
        {
            public const string AtRally = "en_la_plaza";
            public const string SwappedRifle = "fusil_cambiado";
            public const string ChargeOrdered = "carga_a_la_bayoneta";
            public const string DrummerFound = "tambor_encontrado";
            public const string WaterGiven = "agua_al_tambor";
            public const string ReachedColumn = "en_la_columna";
            public const string PlayerDown = "mariano_santos_cae";
        }

        public static class Stages
        {
            public const string Surprise = "t1_la_sorpresa";
            public const string Village = "t2_callejuelas";
            public const string Krupp = "t3_los_krupp";
            public const string Aftermath = "t4_victoria_sin_agua";
        }

        public const string Perspective = "mariano_santos";
        public const string Caceres = "Andrés Avelino Cáceres";

        public static MissionScript Build(Func<string, string> readRepositoryFile, int villageEnemies = DefaultVillageEnemies) =>
            Build(GuionQuotes.Load(readRepositoryFile, ChapterHeading), villageEnemies);

        public static MissionScript Build(GuionQuotes guion, int villageEnemies = DefaultVillageEnemies)
        {
            if (guion == null) throw new ArgumentNullException(nameof(guion));
            if (villageEnemies < 1) throw new ArgumentOutOfRangeException(nameof(villageEnemies));
            MissionLine Narrate(string text) => new MissionLine(string.Empty, text, LineKind.Narration);
            MissionLine Hint(string text) => new MissionLine(string.Empty, text, LineKind.Hint);

            var script = new MissionScript
            {
                Id = Id, Title = "Sed en la quebrada", Date = "27 de noviembre de 1879", Location = "Quebrada de Tarapacá",
                // El GDD pide la carta de un oficial chileno a su prometida, pero el Archivo no la tiene: no se inventa.
                RewardCollectibleId = null,
            };

            var surprise = new MissionStage { Id = Stages.Surprise, Title = "La sorpresa", Perspective = Perspective };
            surprise.OnEnter.Add(Narrate("27 de noviembre de 1879. Fondo de la quebrada de Tarapacá. Soldado Mariano Santos, batallón Zepita."));
            surprise.OnEnter.Add(Narrate("Amanece. Hambrientos y exhaustos tras la marcha desde Iquique, los aliados descansan entre sauces secos y casas de barro."));
            surprise.OnEnter.Add(Narrate("Las campanas de la iglesia tañen desordenadas: la vanguardia chilena baja por las laderas."));
            surprise.OnEnter.Add(new MissionLine(Caceres, guion.Find("¡Hijos del Zepita!")));
            surprise.OnEnter.Add(Hint("WASD y ratón: moverse y mirar. Clic derecho: encarar. Clic: fuego. Ve a la plaza de la iglesia con el Zepita."));
            surprise.Objectives.Add(new MissionObjective { Id = "plaza", Text = "Forma con el Zepita en la plaza de la iglesia", Complete = MissionCondition.Flag(Flags.AtRally) });
            surprise.Transitions.Add(new MissionTransition(MissionCondition.AtLeast(Facts.EnemiesDown, 1), Stages.Village));
            surprise.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.AtRally), MissionCondition.DialogueIdle()), Stages.Village));
            script.Stages.Add(surprise);

            var village = new MissionStage { Id = Stages.Village, Title = "Callejuelas de adobe", Perspective = Perspective };
            village.OnEnter.Add(Narrate("Los chilenos entran en el pueblo. Se combate de tapia en tapia, entre corrales y pircas de piedra."));
            village.OnEnter.Add(Hint("Cada cartucho cuenta: el Chassepot no espera municiones."));
            village.Objectives.Add(new MissionObjective
            {
                Id = "rechazar", Text = "Rechaza a la vanguardia chilena en el pueblo",
                Complete = MissionCondition.AtLeast(Facts.EnemiesDown, villageEnemies), ProgressFact = Facts.EnemiesDown, ProgressTarget = villageEnemies,
            });
            var lowAmmo = new MissionTrigger
            {
                Id = "pocos_cartuchos", Interrupts = true,
                When = MissionCondition.All(MissionCondition.Not(MissionCondition.AtLeast(Facts.Rounds, LowAmmoThreshold + 1)), MissionCondition.NotFlag(Flags.SwappedRifle)),
            };
            lowAmmo.Lines.Add(Hint("Quedan pocos cartuchos de Chassepot. Recoge el Comblain de un chileno caído (E) y sus cartuchos."));
            village.Triggers.Add(lowAmmo);
            var swap = new MissionTrigger { Id = "comblain", When = MissionCondition.Flag(Flags.SwappedRifle) };
            swap.Lines.Add(Narrate("Un Comblain chileno: otro cerrojo, otro cartucho. Las cartucheras de los caídos vuelven a servir."));
            village.Triggers.Add(swap);
            village.Transitions.Add(new MissionTransition(MissionCondition.AtLeast(Facts.EnemiesDown, villageEnemies), Stages.Krupp));
            script.Stages.Add(village);

            var krupp = new MissionStage { Id = Stages.Krupp, Title = "Los Krupp de la pampa", Perspective = Perspective };
            krupp.OnEnter.Add(Narrate("Desde la pampa superior, los cañones Krupp chilenos barren la quebrada."));
            krupp.OnEnter.Add(Narrate("Cáceres ordena calar bayonetas y subir la ladera."));
            krupp.OnEnter.Add(Hint("F: estocada con la bayoneta. Quédate junto a cada pieza, sin chilenos en pie, para tomarla."));
            krupp.Objectives.Add(new MissionObjective
            {
                Id = "krupp", Text = "Toma los cañones Krupp de la pampa",
                Complete = MissionCondition.AtLeast(Facts.GunsTaken, KruppGuns), ProgressFact = Facts.GunsTaken, ProgressTarget = KruppGuns,
            });
            krupp.Triggers.Add(new MissionTrigger { Id = "a_la_bayoneta", When = MissionCondition.StageTime(0f), SetsFlag = Flags.ChargeOrdered });
            var firstGun = new MissionTrigger { Id = "primera_pieza", When = MissionCondition.AtLeast(Facts.GunsTaken, 1) };
            firstGun.Lines.Add(Narrate("La primera pieza es vuestra. Sus sirvientes huyen hacia la pampa."));
            krupp.Triggers.Add(firstGun);
            krupp.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.AtLeast(Facts.GunsTaken, KruppGuns), MissionCondition.DialogueIdle()), Stages.Aftermath));
            script.Stages.Add(krupp);

            var after = new MissionStage { Id = Stages.Aftermath, Title = "Victoria sin agua", Perspective = Perspective };
            after.OnEnter.Add(Narrate("Victoria rotunda en medio de la desolación. Sin caballería ni agua, no hay persecución posible."));
            after.OnEnter.Add(Narrate("La columna se prepara para seguir a pie hacia Arica."));
            after.Objectives.Add(new MissionObjective { Id = "tambor", Text = "Acércale tu caramayola al tambor herido", Optional = true, Complete = MissionCondition.Flag(Flags.WaterGiven) });
            after.Objectives.Add(new MissionObjective { Id = "columna", Text = "Únete a la columna que marcha hacia Arica", Complete = MissionCondition.Flag(Flags.ReachedColumn) });
            var drummer = new MissionTrigger { Id = "tambor_herido", When = MissionCondition.Flag(Flags.DrummerFound) };
            drummer.Lines.Add(Narrate("Un joven tambor chileno, herido de muerte, recostado contra una pared de barro. Pide agua con un hilo de voz."));
            drummer.Lines.Add(Hint("E: darle tu caramayola."));
            after.Triggers.Add(drummer);
            var water = new MissionTrigger { Id = "agua", When = MissionCondition.Flag(Flags.WaterGiven), Interrupts = true };
            water.Lines.Add(Narrate("Bebe despacio. Expira en silencio, mirándote con gratitud."));
            after.Triggers.Add(water);
            after.Transitions.Add(new MissionTransition(MissionCondition.All(MissionCondition.Flag(Flags.ReachedColumn), MissionCondition.DialogueIdle()), MissionScript.CompleteStage));
            script.Stages.Add(after);

            script.Failures.Add(new MissionFailure(MissionCondition.Flag(Flags.PlayerDown), "Mariano Santos cae en la quebrada."));
            return script;
        }
    }
}
