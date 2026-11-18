using System;
using Pacifico.Core.Common;
using Pacifico.Core.Weapons;

namespace Pacifico.Core.Infantry
{
    /// <summary>
    /// Salud de un combatiente (puntos sobre <see cref="WeaponSpec.MaxHealth"/>, la misma escala que el daño de las
    /// fichas). Un balazo de 11 mm a corta distancia deja fuera de combate casi siempre; la cabeza cuenta doble. El
    /// jugador puede recobrar una parte si pasa unos segundos sin recibir fuego (licencia de jugabilidad: un
    /// vendaje apretado, no una curación): nunca por encima de <see cref="RecoveryCap"/>.
    /// </summary>
    public sealed class Vitality
    {
        public const float HeadMultiplier = 2f;
        public const float LimbMultiplier = 0.6f;

        public Vitality(float maxHealth = WeaponSpec.MaxHealth)
        {
            if (maxHealth <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            MaxHealth = maxHealth;
            Health = maxHealth;
        }

        public float MaxHealth { get; }
        public float Health { get; private set; }
        public bool IsDown => Health <= 0f;
        public float Fraction => MathUtil.Clamp01(Health / MaxHealth);

        /// <summary>Recuperación tras <see cref="RecoveryDelaySeconds"/> sin daño (puntos/s; 0 = ninguna).</summary>
        public float RecoveryPerSecond { get; set; }
        public float RecoveryDelaySeconds { get; set; } = 6f;
        /// <summary>Fracción máxima de la salud que se recobra.</summary>
        public float RecoveryCap { get; set; } = 0.6f;

        public float SinceDamageSeconds { get; private set; } = float.PositiveInfinity;

        public event Action Downed;

        /// <summary>
        /// Daño a partir del cual un golpe es una muerte preparada por la historia (minas, voladuras, el final de un
        /// capítulo): no se reduce ni se amortigua. Los balazos y bayonetazos nunca llegan (la ficha más dura hace 100).
        /// </summary>
        public const float ScriptedLethalDamage = 500f;

        /// <summary>
        /// Fracción del daño que llega de verdad (1 = todo). El jugador usa <see cref="PlayerIncomingScale"/>.
        /// </summary>
        public float IncomingScale { get; set; } = 1f;

        /// <summary>
        /// Si la salud está por encima de esta fracción, un golpe normal no puede tumbar: deja <see cref="ShieldLeftHealth"/>.
        /// 0 = sin protección. Licencia de jugabilidad para el jugador: siempre hay un aviso antes de caer.
        /// </summary>
        public float OneShotShieldFraction { get; set; }

        /// <summary>Salud que queda cuando la protección de un solo golpe actúa.</summary>
        public float ShieldLeftHealth { get; set; } = 10f;

        /// <summary>
        /// Segundos sin recibir daño normal después de que actúe la protección: tiempo para ponerse a cubierto (en
        /// Miraflores, quieto, se pasaba de la salud entera a caer en 8 s). Valor de balance.
        /// </summary>
        public float ShieldGraceSeconds { get; set; } = 2f;

        /// <summary>Lo que queda del respiro tras la protección.</summary>
        public float GraceLeftSeconds { get; private set; }

        /// <summary>Valores de balance para el jugador (revisión del 2026-11-17: en Miraflores moría a los 15 s).</summary>
        public const float PlayerIncomingScale = 0.5f;
        public const float PlayerOneShotShieldFraction = 0.5f;

        /// <summary>Salud del jugador: recupera, recibe la mitad del daño y no cae de un solo golpe con más de media salud.</summary>
        public static Vitality ForPlayer(float recoveryPerSecond) => new Vitality
        {
            RecoveryPerSecond = recoveryPerSecond,
            IncomingScale = PlayerIncomingScale,
            OneShotShieldFraction = PlayerOneShotShieldFraction,
        };

        /// <summary>Aplica daño; devuelve true si este golpe lo deja fuera de combate.</summary>
        public bool ApplyDamage(float amount)
        {
            if (IsDown || amount <= 0f) return false;
            if (amount < ScriptedLethalDamage)
            {
                if (GraceLeftSeconds > 0f) return false;
                amount *= IncomingScale;
                if (OneShotShieldFraction > 0f && Health > MaxHealth * OneShotShieldFraction && amount >= Health)
                {
                    amount = Health - Math.Min(ShieldLeftHealth, Health);
                    GraceLeftSeconds = ShieldGraceSeconds;
                }
            }
            Health = Math.Max(0f, Health - amount);
            SinceDamageSeconds = 0f;
            if (!IsDown) return false;
            Downed?.Invoke();
            return true;
        }

        public void Step(float dt)
        {
            if (IsDown || dt <= 0f) return;
            GraceLeftSeconds = Math.Max(0f, GraceLeftSeconds - dt);
            SinceDamageSeconds += dt;
            float cap = MaxHealth * RecoveryCap;
            if (RecoveryPerSecond > 0f && SinceDamageSeconds >= RecoveryDelaySeconds && Health < cap)
            {
                Health = Math.Min(cap, Health + RecoveryPerSecond * dt);
            }
        }
    }
}
