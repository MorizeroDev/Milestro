using System;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public struct TextGradient : IEquatable<TextGradient>
    {
        public const int DefaultMeshResolution = 16;
        public const int MinMeshResolution = 2;
        public const int MaxMeshResolution = 64;

        [SerializeField]
        private TextGradientMode m_mode;

        [SerializeField]
        private TextColorGradient m_fourCorners;

        [SerializeField]
        private Gradient? m_stops;

        [SerializeField]
        private float m_angleDegrees;

        [SerializeField]
        private Vector2 m_radialCenter;

        [SerializeField]
        private int m_meshResolution;

        private TextGradient(TextGradientMode mode,
            TextColorGradient fourCorners,
            Gradient? stops,
            float angleDegrees,
            Vector2 radialCenter,
            int meshResolution)
        {
            m_mode = mode;
            m_fourCorners = fourCorners;
            m_stops = stops;
            m_angleDegrees = angleDegrees;
            m_radialCenter = radialCenter;
            m_meshResolution = meshResolution;
        }

        public TextGradientMode Mode
        {
            get => m_mode;
            set => m_mode = value;
        }

        public TextColorGradient FourCorners
        {
            get => m_fourCorners;
            set => m_fourCorners = value;
        }

        public Gradient? Stops
        {
            get => m_stops;
            set => m_stops = value;
        }

        /// <summary>
        /// Office-style linear gradient angle: 0 fills left to right, 90 fills
        /// top to bottom, and values increase clockwise.
        /// </summary>
        public float AngleDegrees
        {
            get => m_angleDegrees;
            set => m_angleDegrees = value;
        }

        /// <summary>
        /// Normalized radial center inside the display quad, (0.5, 0.5) is the middle.
        /// </summary>
        public Vector2 RadialCenter
        {
            get => m_radialCenter;
            set => m_radialCenter = value;
        }

        /// <summary>
        /// Tessellation density used to bake Linear and Radial gradients into the
        /// display mesh. FourCorners always uses the exact four-vertex quad.
        /// </summary>
        public int MeshResolution
        {
            get => m_meshResolution;
            set => m_meshResolution = Mathf.Clamp(value, MinMeshResolution, MaxMeshResolution);
        }

        public static TextGradient ForCorners(TextColorGradient corners)
        {
            return new TextGradient(TextGradientMode.FourCorners,
                corners,
                null,
                0f,
                new Vector2(0.5f, 0.5f),
                DefaultMeshResolution);
        }

        public static TextGradient Linear(Gradient stops, float angleDegrees)
        {
            return new TextGradient(TextGradientMode.Linear,
                TextColorGradient.White,
                stops,
                angleDegrees,
                new Vector2(0.5f, 0.5f),
                DefaultMeshResolution);
        }

        public static TextGradient Radial(Gradient stops)
        {
            return Radial(stops, new Vector2(0.5f, 0.5f));
        }

        public static TextGradient Radial(Gradient stops, Vector2 center)
        {
            return new TextGradient(TextGradientMode.Radial,
                TextColorGradient.White,
                stops,
                0f,
                center,
                DefaultMeshResolution);
        }

        public static TextGradient White => ForCorners(TextColorGradient.White);

        public bool Equals(TextGradient other)
        {
            // Stops compares by reference on purpose: mutating a shared Gradient in
            // place is not observable here, so animated content should reassign the
            // struct (or call SetVerticesDirty on the component).
            return m_mode == other.m_mode
                   && m_fourCorners == other.m_fourCorners
                   && ReferenceEquals(m_stops, other.m_stops)
                   && m_angleDegrees.Equals(other.m_angleDegrees)
                   && m_radialCenter.Equals(other.m_radialCenter)
                   && m_meshResolution == other.m_meshResolution;
        }

        public override bool Equals(object? obj)
        {
            return obj is TextGradient other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m_mode,
                m_fourCorners,
                m_stops,
                m_angleDegrees,
                m_radialCenter,
                m_meshResolution);
        }

        public static bool operator ==(TextGradient left, TextGradient right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextGradient left, TextGradient right)
        {
            return !left.Equals(right);
        }
    }
}
