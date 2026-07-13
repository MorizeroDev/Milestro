using System;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public sealed class ScrollElasticSettings : ISerializationCallbackReceiver
    {
        private const int CurrentSerializedVersion = 1;

        public const float DefaultResistance = 0.5f;
        public const float DefaultMaxOverscroll = 96f;
        public const float DefaultReturnDurationSeconds = 0.24f;
        public const float DefaultReleaseDelaySeconds = 0.08f;

        [SerializeField]
        [HideInInspector]
        private int m_serializedVersion = CurrentSerializedVersion;

        [SerializeField]
        private bool m_enabled = true;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_resistance = DefaultResistance;

        [SerializeField]
        [Min(0f)]
        private float m_maxOverscroll = DefaultMaxOverscroll;

        [SerializeField]
        [Min(0f)]
        private float m_returnDurationSeconds = DefaultReturnDurationSeconds;

        [SerializeField]
        [Min(0f)]
        private float m_releaseDelaySeconds = DefaultReleaseDelaySeconds;

        public bool Enabled
        {
            get => m_enabled;
            set => m_enabled = value;
        }

        public float Resistance
        {
            get => ResolveResistance(m_resistance);
            set => m_resistance = ResolveResistance(value);
        }

        public float MaxOverscroll
        {
            get => ResolveMaxOverscroll(m_maxOverscroll);
            set => m_maxOverscroll = ResolveMaxOverscroll(value);
        }

        public float ReturnDurationSeconds
        {
            get => ResolveReturnDurationSeconds(m_returnDurationSeconds);
            set => m_returnDurationSeconds = ResolveReturnDurationSeconds(value);
        }

        public float ReleaseDelaySeconds
        {
            get => ResolveReleaseDelaySeconds(m_releaseDelaySeconds);
            set => m_releaseDelaySeconds = ResolveReleaseDelaySeconds(value);
        }

        internal void EnsureInitialized()
        {
            if (m_serializedVersion <= 0)
            {
                ResetToDefaults();
            }
        }

        internal static ScrollElasticSettings Resolve(ref ScrollElasticSettings? settings)
        {
            settings ??= new ScrollElasticSettings();
            settings.EnsureInitialized();
            return settings;
        }

        internal void Validate()
        {
            EnsureInitialized();
            m_resistance = Resistance;
            m_maxOverscroll = MaxOverscroll;
            m_returnDurationSeconds = ReturnDurationSeconds;
            m_releaseDelaySeconds = ReleaseDelaySeconds;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            EnsureInitialized();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            EnsureInitialized();
        }

        private void ResetToDefaults()
        {
            m_serializedVersion = CurrentSerializedVersion;
            m_enabled = true;
            m_resistance = DefaultResistance;
            m_maxOverscroll = DefaultMaxOverscroll;
            m_returnDurationSeconds = DefaultReturnDurationSeconds;
            m_releaseDelaySeconds = DefaultReleaseDelaySeconds;
        }

        private static float ResolveResistance(float value)
        {
            return FloatUtil.IsFinite(value) ? Mathf.Clamp01(value) : DefaultResistance;
        }

        private static float ResolveMaxOverscroll(float value)
        {
            return FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : DefaultMaxOverscroll;
        }

        private static float ResolveReturnDurationSeconds(float value)
        {
            return FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : DefaultReturnDurationSeconds;
        }

        private static float ResolveReleaseDelaySeconds(float value)
        {
            return FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : DefaultReleaseDelaySeconds;
        }
    }
}
