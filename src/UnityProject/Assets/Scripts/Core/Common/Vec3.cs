using System;

namespace Pacifico.Core.Common
{
    /// <summary>
    /// Vector 3D mínimo para la simulación sin motor (mismas convenciones que Unity: Y arriba, Z adelante).
    /// </summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);

        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Módulo en el plano horizontal (XZ).</summary>
        public float HorizontalMagnitude => (float)Math.Sqrt(X * X + Z * Z);

        public Vec3 Horizontal => new Vec3(X, 0f, Z);

        public Vec3 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? this / m : Zero;
            }
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => a * s;
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;

        /// <summary>Desplaza <paramref name="current"/> hacia <paramref name="target"/> sin recorrer más de <paramref name="maxDelta"/>.</summary>
        public static Vec3 MoveTowards(Vec3 current, Vec3 target, float maxDelta)
        {
            Vec3 delta = target - current;
            float distance = delta.Magnitude;
            if (distance <= maxDelta || distance < 1e-6f) return target;
            return current + delta / distance * maxDelta;
        }

        /// <summary>Gira un vector del plano horizontal <paramref name="yawDeg"/> grados en sentido horario (como la Y de Unity).</summary>
        public static Vec3 RotateYaw(float localX, float localZ, float yawDeg)
        {
            double r = yawDeg * MathUtil.Deg2Rad;
            float sin = (float)Math.Sin(r);
            float cos = (float)Math.Cos(r);
            return new Vec3(localX * cos + localZ * sin, 0f, -localX * sin + localZ * cos);
        }

        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);

        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();

        public override string ToString() => "(" + X.ToString("0.###") + ", " + Y.ToString("0.###") + ", " + Z.ToString("0.###") + ")";
    }
}
