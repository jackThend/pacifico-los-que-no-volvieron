using System;

namespace Pacifico.Core.Naval
{
    /// <summary>Integridad del casco. Recibe impactos resueltos por <see cref="ArmorImpact"/>.</summary>
    public sealed class HullIntegrity
    {
        public float Max { get; }
        public float Current { get; private set; }
        public bool IsSunk => Current <= 0f;
        public float Fraction => Max > 0f ? Current / Max : 0f;

        public event Action<ImpactReport> Hit;
        public event Action Sunk;

        public HullIntegrity(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = max;
        }

        public void ApplyImpact(ImpactReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (IsSunk) return;
            Current = Math.Max(0f, Current - report.Damage);
            Hit?.Invoke(report);
            if (IsSunk) Sunk?.Invoke();
        }

        /// <summary>Daño estructural genérico (espolonazo, fuego).</summary>
        public void ApplyDamage(float amount)
        {
            if (IsSunk || amount <= 0f) return;
            Current = Math.Max(0f, Current - amount);
            if (IsSunk) Sunk?.Invoke();
        }

        /// <summary>Hundimiento por causa ajena a la estructura (inundación).</summary>
        public void Founder()
        {
            if (IsSunk) return;
            Current = 0f;
            Sunk?.Invoke();
        }
    }
}
