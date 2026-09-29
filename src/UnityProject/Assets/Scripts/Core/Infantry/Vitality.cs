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

        /// <summary>Aplica daño; devuelve true si este golpe lo deja fuera de combate.</summary>
        public bool ApplyDamage(float amount)
        {
            if (IsDown || amount <= 0f) return false;
            Health = Math.Max(0f, Health - amount);
            SinceDamageSeconds = 0f;
            if (!IsDown) return false;
            Downed?.Invoke();
            return true;
        }

        public void Step(float dt)
        {
            if (IsDown || dt <= 0f) return;
            SinceDamageSeconds += dt;
            float cap = MaxHealth * RecoveryCap;
            if (RecoveryPerSecond > 0f && SinceDamageSeconds >= RecoveryDelaySeconds && Health < cap)
            {
                Health = Math.Min(cap, Health + RecoveryPerSecond * dt);
            }
        }
    }
}
