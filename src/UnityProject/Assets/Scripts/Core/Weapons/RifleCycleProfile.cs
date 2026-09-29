using System;
using System.Collections.Generic;

namespace Pacifico.Core.Weapons
{
    /// <summary>Etapas del ciclo de un arma de retrocarga (ROADMAP 3.2).</summary>
    public enum RifleStage
    {
        Ready = 0,
        /// <summary>Percutir: disparo, retroceso y recuperación.</summary>
        Firing = 1,
        /// <summary>Amartillar a mano (Remington: martillo; Chassepot: pieza de disparo).</summary>
        Cocking = 2,
        /// <summary>Abrir el cierre: bajar la palanca, girar y retirar el cerrojo o echar atrás el bloque.</summary>
        OpeningAction = 3,
        /// <summary>Expulsar la vaina vacía (no existe con cartucho combustible).</summary>
        Extracting = 4,
        /// <summary>Sacar un cartucho de la cartuchera e introducirlo en la recámara.</summary>
        InsertingCartridge = 5,
        ClosingAction = 6,
        /// <summary>Volver a encarar y apuntar.</summary>
        Shouldering = 7,
        /// <summary>Introducir un cartucho en el depósito tubular (Winchester).</summary>
        LoadingMagazine = 8,
    }

    /// <summary>Una etapa con su duración.</summary>
    public struct StageSpec
    {
        public RifleStage Stage;
        public float Seconds;

        public StageSpec(RifleStage stage, float seconds)
        {
            Stage = stage;
            Seconds = seconds;
        }
    }

    /// <summary>
    /// Secuencias y duraciones del ciclo según el mecanismo real de cada arma. Para las armas monotiro, la
    /// secuencia completa (percutir → … → encarar) dura exactamente <see cref="WeaponSpec.ReloadSeconds"/>
    /// (2,0 s el Comblain, según el GDD); los pesos reparten ese tiempo entre las etapas presentes.
    /// </summary>
    public sealed class RifleCycleProfile
    {
        // Pesos relativos de cada etapa: insertar el cartucho (sacarlo de la cartuchera) y volver a encarar son lo
        // más lento; accionar el cierre es rápido en los cuatro sistemas.
        private static readonly Dictionary<RifleStage, float> Weights = new Dictionary<RifleStage, float>
        {
            { RifleStage.Firing, 0.10f },
            { RifleStage.Cocking, 0.07f },
            { RifleStage.OpeningAction, 0.12f },
            { RifleStage.Extracting, 0.09f },
            { RifleStage.InsertingCartridge, 0.30f },
            { RifleStage.ClosingAction, 0.10f },
            { RifleStage.Shouldering, 0.22f },
        };

        /// <summary>Duraciones fijas del ciclo de palanca de la Winchester (bajar: extrae y expulsa; subir: alimenta y cierra).</summary>
        public const float LeverFiringSeconds = 0.12f;
        public const float LeverOpenSeconds = 0.2f;
        public const float LeverCloseSeconds = 0.2f;
        public const float LeverShoulderSeconds = 0.14f;

        private readonly StageSpec[] _fireCycle;
        private readonly StageSpec[] _reloadWithCase;
        private readonly StageSpec[] _reloadEmpty;

        private RifleCycleProfile(WeaponSpec weapon)
        {
            Weapon = weapon;
            UsesMagazine = weapon.Feed == AmmoFeed.TubeMagazine;
            NeedsManualCocking = weapon.Action == FiringAction.RollingBlock ||
                                 (weapon.Action == FiringAction.BoltAction && weapon.Case == CartridgeCase.Combustible);
            EjectsCase = weapon.Case == CartridgeCase.Metallic;

            if (UsesMagazine)
            {
                _fireCycle = new[]
                {
                    new StageSpec(RifleStage.Firing, LeverFiringSeconds),
                    new StageSpec(RifleStage.OpeningAction, LeverOpenSeconds),
                    new StageSpec(RifleStage.ClosingAction, LeverCloseSeconds),
                    new StageSpec(RifleStage.Shouldering, LeverShoulderSeconds),
                };
                _reloadWithCase = _reloadEmpty = new StageSpec[0];
                return;
            }

            var stages = new List<RifleStage> { RifleStage.Firing };
            if (NeedsManualCocking) stages.Add(RifleStage.Cocking);
            stages.Add(RifleStage.OpeningAction);
            if (EjectsCase) stages.Add(RifleStage.Extracting);
            stages.Add(RifleStage.InsertingCartridge);
            stages.Add(RifleStage.ClosingAction);
            stages.Add(RifleStage.Shouldering);

            float total = 0f;
            foreach (RifleStage s in stages) total += Weights[s];
            _fireCycle = stages.ConvertAll(s => new StageSpec(s, weapon.ReloadSeconds * Weights[s] / total)).ToArray();

            // Recarga manual (desde el arma lista pero vacía): el mismo ciclo sin percutir; con o sin vaina que extraer.
            var withCase = new List<StageSpec>();
            var empty = new List<StageSpec>();
            foreach (StageSpec spec in _fireCycle)
            {
                if (spec.Stage == RifleStage.Firing) continue;
                withCase.Add(spec);
                if (spec.Stage != RifleStage.Extracting) empty.Add(spec);
            }
            _reloadWithCase = withCase.ToArray();
            _reloadEmpty = empty.ToArray();
        }

        public static RifleCycleProfile For(WeaponSpec weapon)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            if (!weapon.IsFirearm) throw new ArgumentException(weapon.Id + " no es un arma de fuego", nameof(weapon));
            return new RifleCycleProfile(weapon);
        }

        public WeaponSpec Weapon { get; }
        public bool UsesMagazine { get; }
        public bool NeedsManualCocking { get; }
        public bool EjectsCase { get; }

        /// <summary>Disparo seguido de la recarga completa (monotiro) o del ciclo de palanca (depósito).</summary>
        public IReadOnlyList<StageSpec> FireCycle => _fireCycle;

        /// <summary>Recarga manual de un arma monotiro vacía, con o sin vaina en la recámara.</summary>
        public IReadOnlyList<StageSpec> ReloadCycle(bool spentCaseInChamber) => spentCaseInChamber ? _reloadWithCase : _reloadEmpty;

        public float FireCycleSeconds
        {
            get
            {
                float t = 0f;
                foreach (StageSpec s in _fireCycle) t += s.Seconds;
                return t;
            }
        }
    }
}
