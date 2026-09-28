using System;
using Pacifico.Core.Ships;

namespace Pacifico.Core.Naval
{
    /// <summary>
    /// Cuadernas del casco distribuidas a lo largo de la eslora (índice 0 = popa).
    /// Un espolonazo las parte alrededor del punto de impacto; cada cuaderna
    /// rota es una vía de agua.
    /// </summary>
    public sealed class HullFrames
    {
        public const float FrameSpacingM = 1.5f;
        /// <summary>Energía (MJ) que absorbe cada cuaderna antes de partirse.</summary>
        public const float WoodFrameStrengthMJ = 0.8f;
        public const float IronFrameStrengthMJ = 2f;
        /// <summary>Vía de agua por cuaderna rota (toneladas por minuto).</summary>
        public const float WoodInflowPerFrameTonsPerMinute = 15f;
        public const float IronInflowPerFrameTonsPerMinute = 10f;

        private readonly bool[] broken;

        public HullMaterial Material { get; }
        public float LengthM { get; }
        public int Count => broken.Length;
        public float FrameStrengthMJ => Material == HullMaterial.Wood ? WoodFrameStrengthMJ : IronFrameStrengthMJ;
        public float InflowPerFrameTonsPerMinute =>
            Material == HullMaterial.Wood ? WoodInflowPerFrameTonsPerMinute : IronInflowPerFrameTonsPerMinute;

        public HullFrames(HullMaterial material, float lengthM)
        {
            if (lengthM <= 0f) throw new ArgumentOutOfRangeException(nameof(lengthM));
            Material = material;
            LengthM = lengthM;
            broken = new bool[Math.Max(1, (int)Math.Round(lengthM / FrameSpacingM))];
        }

        public static HullFrames ForShip(ShipSpec spec) => new HullFrames(spec.Hull, spec.LengthM);

        public bool IsBroken(int index) => broken[index];

        public int BrokenCount
        {
            get
            {
                var count = 0;
                foreach (var b in broken) if (b) count++;
                return count;
            }
        }

        /// <summary>Índice de la cuaderna situada a <paramref name="forwardM"/> m del centro hacia proa.</summary>
        public int IndexAt(float forwardM)
        {
            var fromStern = forwardM + LengthM * 0.5f;
            var index = (int)Math.Floor(fromStern / LengthM * Count);
            return index < 0 ? 0 : index >= Count ? Count - 1 : index;
        }

        /// <summary>
        /// Parte hasta <paramref name="count"/> cuadernas intactas alrededor de
        /// <paramref name="centerIndex"/>, alternando hacia proa y popa.
        /// Devuelve cuántas se partieron realmente.
        /// </summary>
        public int BreakAround(int centerIndex, int count)
        {
            var newlyBroken = 0;
            for (var offset = 0; newlyBroken < count && offset < Count; offset++)
            {
                foreach (var index in offset == 0 ? new[] { centerIndex } : new[] { centerIndex + offset, centerIndex - offset })
                {
                    if (newlyBroken >= count || index < 0 || index >= Count || broken[index]) continue;
                    broken[index] = true;
                    newlyBroken++;
                }
            }
            return newlyBroken;
        }
    }
}
