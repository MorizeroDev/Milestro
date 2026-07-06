using System;
using UnityEngine;

namespace Milestro.Util
{
    internal readonly struct CubicBezierCurve
    {
        private const double Epsilon = 0.000001;
        private readonly float ax;
        private readonly float bx;
        private readonly float cx;
        private readonly float ay;
        private readonly float by;
        private readonly float cy;

        public CubicBezierCurve(float x1, float y1, float x2, float y2)
        {
            cx = 3f * x1;
            bx = 3f * (x2 - x1) - cx;
            ax = 1f - cx - bx;
            cy = 3f * y1;
            by = 3f * (y2 - y1) - cy;
            ay = 1f - cy - by;
        }

        public float Evaluate(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f || progress >= 1f)
            {
                return progress;
            }

            return SampleY(SolveTAnalytically(progress));
        }

        private float SolveTAnalytically(float x)
        {
            // Invert x(t) with Cardano, then sample y(t).
            return Mathf.Clamp01((float)SolveCubicInUnitInterval(ax, bx, cx, -x));
        }

        private static double SolveCubicInUnitInterval(double a, double b, double c, double d)
        {
            if (Math.Abs(a) <= Epsilon)
            {
                return SolveQuadraticInUnitInterval(b, c, d);
            }

            var invA = 1.0 / a;
            var normalizedB = b * invA;
            var normalizedC = c * invA;
            var normalizedD = d * invA;
            var p = normalizedC - normalizedB * normalizedB / 3.0;
            var q = 2.0 * normalizedB * normalizedB * normalizedB / 27.0 -
                    normalizedB * normalizedC / 3.0 +
                    normalizedD;
            var discriminant = q * q / 4.0 + p * p * p / 27.0;

            if (discriminant >= 0.0)
            {
                var sqrtDiscriminant = Math.Sqrt(discriminant);
                var root = CubeRoot(-q / 2.0 + sqrtDiscriminant) +
                           CubeRoot(-q / 2.0 - sqrtDiscriminant) -
                           normalizedB / 3.0;
                return ClampUnit(root);
            }

            var radius = 2.0 * Math.Sqrt(-p / 3.0);
            var acosInput = ClampSigned((3.0 * q / (2.0 * p)) * Math.Sqrt(-3.0 / p));
            var angle = Math.Acos(acosInput) / 3.0;
            var offset = normalizedB / 3.0;
            var bestRoot = 0.0;
            var bestDistance = double.PositiveInfinity;
            for (var i = 0; i < 3; ++i)
            {
                var root = radius * Math.Cos(angle - 2.0 * Math.PI * i / 3.0) - offset;
                var clamped = ClampUnit(root);
                var distance = Math.Abs(root - clamped);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRoot = clamped;
                }
            }

            return bestRoot;
        }

        private static double SolveQuadraticInUnitInterval(double a, double b, double c)
        {
            if (Math.Abs(a) <= Epsilon)
            {
                return Math.Abs(b) <= Epsilon ? 0.0 : ClampUnit(-c / b);
            }

            var discriminant = b * b - 4.0 * a * c;
            if (discriminant <= 0.0)
            {
                return ClampUnit(-b / (2.0 * a));
            }

            var sqrtDiscriminant = Math.Sqrt(discriminant);
            var firstRoot = (-b + sqrtDiscriminant) / (2.0 * a);
            if (firstRoot >= -Epsilon && firstRoot <= 1.0 + Epsilon)
            {
                return ClampUnit(firstRoot);
            }

            return ClampUnit((-b - sqrtDiscriminant) / (2.0 * a));
        }

        private static double CubeRoot(double value)
        {
            return value < 0.0
                ? -Math.Pow(-value, 1.0 / 3.0)
                : Math.Pow(value, 1.0 / 3.0);
        }

        private static double ClampUnit(double value)
        {
            if (value <= 0.0)
            {
                return 0.0;
            }

            return value >= 1.0 ? 1.0 : value;
        }

        private static double ClampSigned(double value)
        {
            if (value <= -1.0)
            {
                return -1.0;
            }

            return value >= 1.0 ? 1.0 : value;
        }

        private float SampleY(float t)
        {
            return ((ay * t + by) * t + cy) * t;
        }
    }
}
