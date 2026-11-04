using UnityEngine;

namespace Craquez.Sim
{
    /// <summary>Utilidades de vetor 2D usadas pela simulação.</summary>
    public static class V2
    {
        public static Vector2 Rotated(this Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Ângulo com sinal (radianos) de a para b.</summary>
        public static float AngleTo(this Vector2 a, Vector2 b)
            => Mathf.Atan2(a.x * b.y - a.y * b.x, a.x * b.x + a.y * b.y);

        public static Vector2 Limit(this Vector2 v, float max)
        {
            float l = v.magnitude;
            return l > max && l > 0f ? v * (max / l) : v;
        }

        public static Vector2 MoveToward(this Vector2 v, Vector2 to, float delta)
        {
            var d = to - v;
            float l = d.magnitude;
            return l <= delta || l < 1e-6f ? to : v + d / l * delta;
        }

        public static Vector2 Norm(this Vector2 v)
        {
            float l = v.magnitude;
            return l > 1e-6f ? v / l : Vector2.zero;
        }

        public static float Dist(this Vector2 a, Vector2 b) => (a - b).magnitude;

        public static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-9f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return a + ab * t;
        }

        public static Vector2 RotateToward(Vector2 a, Vector2 b, float maxAngle)
        {
            if (b.sqrMagnitude < 0.0001f) return a;
            float ang = a.AngleTo(b);
            return a.Rotated(Mathf.Clamp(ang, -maxAngle, maxAngle));
        }
    }
}
