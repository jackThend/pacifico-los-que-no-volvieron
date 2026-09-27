using System;
using Pacifico.Core.Common;

namespace Pacifico.Core.Melee
{
    /// <summary>Cápsula (segmento con radio): el cuerpo, la cabeza o un poste de un muñeco de práctica.</summary>
    public struct Capsule
    {
        public Vec3 A;
        public Vec3 B;
        public float Radius;

        public Capsule(Vec3 a, Vec3 b, float radius)
        {
            A = a;
            B = b;
            Radius = radius;
        }

        /// <summary>Esfera: cápsula de longitud nula.</summary>
        public static Capsule Sphere(Vec3 center, float radius) => new Capsule(center, center, radius);
    }

    /// <summary>
    /// Posición y orientación del ojo (la cámara): la hoja se describe en coordenadas de vista (X derecha, Y arriba,
    /// Z adelante, como la cámara de Unity) y se lleva al mundo con este marco.
    /// </summary>
    public struct ViewFrame
    {
        public Vec3 Origin;
        public Vec3 Right;
        public Vec3 Up;
        public Vec3 Forward;

        public ViewFrame(Vec3 origin, Vec3 right, Vec3 up, Vec3 forward)
        {
            Origin = origin;
            Right = right;
            Up = up;
            Forward = forward;
        }

        /// <summary>Marco a partir de la dirección de la mirada (sin alabeo).</summary>
        public static ViewFrame LookingAlong(Vec3 origin, Vec3 forward)
        {
            Vec3 f = forward.Normalized;
            Vec3 right = Vec3.Cross(new Vec3(0f, 1f, 0f), f);
            right = right.SqrMagnitude < 1e-8f ? new Vec3(1f, 0f, 0f) : right.Normalized;
            return new ViewFrame(origin, right, Vec3.Cross(f, right), f);
        }

        public Vec3 ToWorld(Vec3 local) => Origin + Right * local.X + Up * local.Y + Forward * local.Z;

        /// <summary>Interpolación entre dos marcos (posición lineal, ejes re-ortonormalizados).</summary>
        public static ViewFrame Lerp(ViewFrame a, ViewFrame b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            Vec3 forward = (a.Forward + (b.Forward - a.Forward) * t).Normalized;
            Vec3 upHint = a.Up + (b.Up - a.Up) * t;
            Vec3 right = Vec3.Cross(upHint, forward);
            right = right.SqrMagnitude < 1e-8f ? a.Right : right.Normalized;
            return new ViewFrame(a.Origin + (b.Origin - a.Origin) * t, right, Vec3.Cross(forward, right), forward);
        }
    }

    public static class MeleeGeometry
    {
        /// <summary>Punto de un segmento más cercano a <paramref name="p"/>.</summary>
        public static Vec3 ClosestPointOnSegment(Vec3 a, Vec3 b, Vec3 p)
        {
            Vec3 ab = b - a;
            float len2 = ab.SqrMagnitude;
            if (len2 < 1e-12f) return a;
            float t = MathUtil.Clamp01(Vec3.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        /// <summary>
        /// Puntos más cercanos entre los segmentos p1–q1 y p2–q2 (Ericson, «Real-Time Collision Detection», §5.1.9).
        /// Devuelve la distancia al cuadrado.
        /// </summary>
        public static float ClosestPointsSegmentSegment(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2, out Vec3 c1, out Vec3 c2)
        {
            const float epsilon = 1e-10f;
            Vec3 d1 = q1 - p1;
            Vec3 d2 = q2 - p2;
            Vec3 r = p1 - p2;
            float a = Vec3.Dot(d1, d1);
            float e = Vec3.Dot(d2, d2);
            float f = Vec3.Dot(d2, r);
            float s, t;

            if (a <= epsilon && e <= epsilon)
            {
                c1 = p1;
                c2 = p2;
                return (c1 - c2).SqrMagnitude;
            }
            if (a <= epsilon)
            {
                s = 0f;
                t = MathUtil.Clamp01(f / e);
            }
            else
            {
                float c = Vec3.Dot(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = MathUtil.Clamp01(-c / a);
                }
                else
                {
                    float b = Vec3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > epsilon ? MathUtil.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = MathUtil.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = MathUtil.Clamp01((b - c) / a);
                    }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).SqrMagnitude;
        }

        /// <summary>
        /// ¿Toca el segmento a–b la cápsula? Si la toca, <paramref name="point"/> es el punto de contacto sobre la
        /// superficie y <paramref name="depth"/> cuánto se ha hundido la hoja (0 si solo roza).
        /// </summary>
        public static bool SegmentHitsCapsule(Vec3 a, Vec3 b, Capsule capsule, out Vec3 point, out float depth)
        {
            float d2 = ClosestPointsSegmentSegment(a, b, capsule.A, capsule.B, out Vec3 onBlade, out Vec3 onAxis);
            float r = capsule.Radius;
            if (d2 > r * r)
            {
                point = onBlade;
                depth = 0f;
                return false;
            }
            float d = (float)Math.Sqrt(d2);
            depth = r - d;
            // Punto de entrada en la superficie, en la dirección de la hoja respecto al eje.
            point = d > 1e-6f ? onAxis + (onBlade - onAxis) * (r / d) : onBlade;
            return true;
        }

        /// <summary>Distancia de un punto a la superficie de la cápsula (negativa si está dentro).</summary>
        public static float SurfaceDistance(Vec3 p, Capsule capsule)
        {
            return (p - ClosestPointOnSegment(capsule.A, capsule.B, p)).Magnitude - capsule.Radius;
        }
    }
}
