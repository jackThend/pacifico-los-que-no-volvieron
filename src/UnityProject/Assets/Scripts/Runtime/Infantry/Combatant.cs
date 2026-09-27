using System;
using System.Collections.Generic;
using Pacifico.Core.Common;
using Pacifico.Core.Infantry;
using Pacifico.Core.Weapons;
using UnityEngine;

namespace Pacifico.Infantry
{
    /// <summary>
    /// Un combatiente de los capítulos FPS (el jugador o un soldado de la IA): bando, salud (<see cref="Vitality"/>) y
    /// recepción de balas y de golpes de bayoneta. La cabeza cuenta doble. Al caer, un soldado de la IA se tumba,
    /// suelta su fusil con los cartuchos que le quedaban (<see cref="WeaponPickup"/>) y avisa por <see cref="Downed"/>.
    /// </summary>
    public sealed class Combatant : MonoBehaviour, IBulletTarget, IMeleeTarget
    {
        [SerializeField] private Faction faction = Faction.Chile;
        [SerializeField] private bool isPlayer;
        [SerializeField] private Collider head;
        [SerializeField] private string displayName = string.Empty;

        private static readonly List<Combatant> s_all = new List<Combatant>();

        /// <summary>Todos los combatientes vivos o caídos de la escena (para buscar blancos).</summary>
        public static IReadOnlyList<Combatant> All => s_all;

        /// <summary>Cualquier combatiente que cae (y quién lo derribó, si se sabe).</summary>
        public static event Action<Combatant, GameObject> Downed;

        public Vitality Vitality { get; private set; }
        public Faction Faction => faction;
        public bool IsPlayer => isPlayer;
        public bool Alive => Vitality != null && !Vitality.IsDown;
        public string DisplayName => displayName;
        public Color ImpactColor => new Color(0.55f, 0.08f, 0.06f);
        /// <summary>Último momento en que recibió daño (para el viñeteado rojo del jugador).</summary>
        public float LastHurtTime { get; private set; } = float.NegativeInfinity;

        /// <summary>Punto al que se apunta: el pecho.</summary>
        public Vector3 AimPoint => transform.position + Vector3.up * (isPlayer ? 1.35f : 1.3f);
        public Vector3 EyePoint => transform.position + Vector3.up * 1.55f;

        /// <summary>El fusil que suelta al caer (lo configura <see cref="RiflemanAI"/>).</summary>
        public Func<(Data.WeaponDataSO weapon, int rounds)> DropProvider { get; set; }

        public void Configure(Faction side, bool player, Collider headCollider, string name, float recoveryPerSecond = 0f)
        {
            faction = side;
            isPlayer = player;
            head = headCollider;
            displayName = name;
            Vitality = new Vitality { RecoveryPerSecond = recoveryPerSecond };
        }

        private void Awake()
        {
            if (Vitality == null) Vitality = new Vitality { RecoveryPerSecond = isPlayer ? 4f : 0f };
        }

        private void OnEnable() => s_all.Add(this);
        private void OnDisable() => s_all.Remove(this);

        private void Update() => Vitality.Step(Time.deltaTime);

        /// <summary>Chile contra la alianza peruano-boliviana.</summary>
        public static bool AreEnemies(Combatant a, Combatant b) => (a.faction == Faction.Chile) != (b.faction == Faction.Chile);

        public float DamageMultiplier(Collider part) => part != null && part == head ? Vitality.HeadMultiplier : 1f;

        public void ReceiveBullet(BulletHit hit)
        {
            // Sin fuego amigo entre soldados de la IA (el jugador sí puede herir a los suyos).
            if (hit.Shooter != null && hit.Shooter.TryGetComponent(out Combatant shooter) && !AreEnemies(shooter, this) && !shooter.isPlayer) return;
            Hurt(hit.Damage * DamageMultiplier(hit.Collider), hit.Shooter, hit.Point - hit.Velocity.normalized);
        }

        public void ReceiveMelee(MeleeStrike strike) => Hurt(strike.Damage * DamageMultiplier(strike.Part), strike.Attacker, strike.Point - strike.Direction);

        /// <summary>Daño directo (golpe de la IA, metralla).</summary>
        public void Hurt(float damage, GameObject attacker, Vector3 from)
        {
            if (!Alive || damage <= 0f) return;
            LastHurtTime = Time.time;
            if (!Vitality.ApplyDamage(damage)) return;
            if (!isPlayer) Fall(from);
            Downed?.Invoke(this, attacker);
        }

        private void Fall(Vector3 from)
        {
            if (TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - from, Vector3.up);
            if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            transform.position += Vector3.up * 0.15f;

            if (DropProvider != null)
            {
                (Data.WeaponDataSO weapon, int rounds) = DropProvider();
                if (weapon != null) WeaponPickup.Spawn(weapon, rounds, transform.position + away.normalized * 0.8f);
            }
            Destroy(gameObject, 60f);
        }
    }
}
