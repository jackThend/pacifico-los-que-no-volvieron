using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Campaign
{
    /// <summary>
    /// Relieve de la bahía de Pisagua para la escena del capítulo 3 (coordenadas del mundo en metros; el mar al sur,
    /// z negativo, a cota 0). Greybox: la playa, una primera ladera de arena hasta el terraplén del ferrocarril
    /// salitrero, una segunda ladera hasta la pampa donde están la estación, los estanques y las locomotoras, y dos puntas
    /// rocosas que entran en el mar (Pichalo al oeste, Guacas al este) con sus fuertes.
    /// <para>
    /// Escala comprimida: los «acantilados de 300 metros» del guion son aquí 44 m en 110 m de ladera, para que las
    /// escuadras puedan subir por el NavMesh (pendiente máxima de 40°) y el combate quepa en el mapa táctico.
    /// </para>
    /// </summary>
    public static class PisaguaTerrain
    {
        public const float SeaLevel = 0f;
        public const float BeachZ = 0f;
        public const float EmbankmentZ = 56f;
        public const float EmbankmentHeight = 21f;
        public const float PlateauZ = 115f;
        public const float PlateauHeight = 44f;
        public const float SeabedHeight = -6f;
        /// <summary>Centro de las puntas en x (oeste negativo).</summary>
        public const float HeadlandX = 200f;
        public const float HeadlandHeight = 28f;

        /// <summary>Altura del suelo (m) en el punto (x, z).</summary>
        public static float HeightAt(float x, float z)
        {
            float profile;
            if (z < -25f) profile = MathUtil.Lerp(SeabedHeight, -1f, MathUtil.SmoothStep(-70f, -25f, z));
            else if (z < BeachZ) profile = MathUtil.Lerp(-1f, 1.5f, MathUtil.SmoothStep(-25f, BeachZ, z));
            else if (z < EmbankmentZ - 6f) profile = MathUtil.Lerp(1.5f, EmbankmentHeight, MathUtil.SmoothStep(BeachZ, EmbankmentZ - 6f, z));
            else if (z < EmbankmentZ + 6f) profile = EmbankmentHeight;
            else if (z < PlateauZ) profile = MathUtil.Lerp(EmbankmentHeight, PlateauHeight, MathUtil.SmoothStep(EmbankmentZ + 6f, PlateauZ, z));
            else profile = PlateauHeight;

            // Ondulación de dunas en las laderas y la pampa (nunca en la playa ni en el terraplén).
            float slopes = MathUtil.SmoothStep(2f, 12f, z) * MathUtil.SmoothStep(6f, 12f, Math.Abs(z - EmbankmentZ));
            float dunes = slopes * (0.6f * (float)Math.Sin(x * 0.07f + z * 0.05f) + 0.3f * (float)Math.Sin(x * 0.19f - z * 0.11f));

            float headland = Math.Max(Headland(x + HeadlandX, z), Headland(x - HeadlandX, z));
            return Math.Max(profile + dunes, headland);
        }

        /// <summary>Punta rocosa: una loma alargada de norte a sur que entra en el mar hasta z ≈ −130.</summary>
        private static float Headland(float dx, float z)
        {
            float across = (float)Math.Exp(-dx * dx / (2f * 45f * 45f));
            float along = MathUtil.SmoothStep(-150f, -90f, z);
            return HeadlandHeight * across * along + SeabedHeight * (1f - along * across);
        }

        /// <summary>Pendiente (°) en (x, z) por diferencias finitas.</summary>
        public static float SlopeDeg(float x, float z, float step = 1f)
        {
            float dx = (HeightAt(x + step, z) - HeightAt(x - step, z)) / (2f * step);
            float dz = (HeightAt(x, z + step) - HeightAt(x, z - step)) / (2f * step);
            return (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
        }
    }
}
