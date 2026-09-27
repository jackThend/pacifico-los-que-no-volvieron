using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Tactics
{
    public enum FormationType
    {
        /// <summary>
        /// Línea: hombro con hombro (≈1 m por hombre), en una fila o, con más de 8 hombres, en dos (la segunda
        /// detrás de la primera), como disparaba la infantería de línea.
        /// </summary>
        Line = 0,
        /// <summary>
        /// Guerrilla (orden disperso): una fila abierta con 5 m entre tiradores y los hombres alternos adelantados o
        /// retrasados, para no ofrecer un blanco compacto. Era el orden habitual de los cazadores en 1879–1880.
        /// </summary>
        Skirmish = 1,
    }

    /// <summary>
    /// Geometría de las formaciones de una escuadra. Las posiciones se dan en coordenadas locales del ancla de la
    /// escuadra: X a la derecha, Z hacia el frente; el ancla es el centro de la primera fila.
    /// </summary>
    public static class Formation
    {
        public const float LineSpacingM = 1.0f;
        public const float LineRankDepthM = 1.2f;
        /// <summary>Por encima de este número de hombres la línea forma en dos filas.</summary>
        public const int LineMaxSingleRank = 8;
        public const float SkirmishSpacingM = 5f;
        public const float SkirmishStaggerM = 1.5f;

        /// <summary>Intervalo entre escuadras vecinas cuando se despliegan varias juntas.</summary>
        public static float IntervalM(FormationType type) => type == FormationType.Line ? 2f : 5f;

        public static int Ranks(FormationType type, int count) => type == FormationType.Line && count > LineMaxSingleRank ? 2 : 1;

        /// <summary>Anchura del frente (de hombre a hombre de los extremos).</summary>
        public static float Frontage(FormationType type, int count)
        {
            if (count <= 1) return 0f;
            int perRank = (int)Math.Ceiling(count / (double)Ranks(type, count));
            return (perRank - 1) * (type == FormationType.Line ? LineSpacingM : SkirmishSpacingM);
        }

        /// <summary>Puestos de <paramref name="count"/> hombres en coordenadas locales (X derecha, Z frente).</summary>
        public static Vec3[] LocalSlots(FormationType type, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var slots = new Vec3[count];
            if (type == FormationType.Line)
            {
                int ranks = Ranks(type, count);
                int perRank = (int)Math.Ceiling(count / (double)ranks);
                int index = 0;
                for (int rank = 0; rank < ranks && index < count; rank++)
                {
                    // La segunda fila puede ir incompleta: se centra detrás de la primera.
                    int inRank = Math.Min(perRank, count - index);
                    for (int i = 0; i < inRank; i++)
                    {
                        float x = (i - (inRank - 1) * 0.5f) * LineSpacingM;
                        slots[index++] = new Vec3(x, 0f, -rank * LineRankDepthM);
                    }
                }
                return slots;
            }

            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * SkirmishSpacingM;
                float z = (i & 1) == 0 ? 0f : -SkirmishStaggerM;
                slots[i] = new Vec3(x, 0f, z);
            }
            return slots;
        }

        /// <summary>Derecha de una orientación horizontal (Y arriba, como Unity).</summary>
        public static Vec3 RightOf(Vec3 facing)
        {
            Vec3 f = Flatten(facing);
            return new Vec3(f.Z, 0f, -f.X);
        }

        /// <summary>Orientación horizontal normalizada (hacia +Z si es nula).</summary>
        public static Vec3 Flatten(Vec3 v)
        {
            var h = new Vec3(v.X, 0f, v.Z);
            float m = h.Magnitude;
            return m > 1e-5f ? h / m : new Vec3(0f, 0f, 1f);
        }

        /// <summary>Puesto en el mundo: ancla + derecha·x + frente·z (la altura la pone el terreno).</summary>
        public static Vec3 ToWorld(Vec3 anchor, Vec3 facing, Vec3 local)
        {
            Vec3 f = Flatten(facing);
            Vec3 r = RightOf(f);
            return anchor + r * local.X + f * local.Z;
        }

        /// <summary>Ángulo (grados, sentido horario visto desde arriba) de una orientación horizontal.</summary>
        public static float HeadingDeg(Vec3 facing)
        {
            Vec3 f = Flatten(facing);
            return (float)(Math.Atan2(f.X, f.Z) / MathUtil.Deg2Rad);
        }

        public static Vec3 FromHeadingDeg(float degrees)
        {
            double r = degrees * MathUtil.Deg2Rad;
            return new Vec3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }
    }
}
