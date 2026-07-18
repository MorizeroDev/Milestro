using System.Collections.Generic;
using UnityEngine;

namespace Milestro.TextInputLifecycleQA
{
    public sealed class TextInputLifecycleQaReceiver : MonoBehaviour
    {
        private readonly List<string> sequence = new List<string>();

        public int ValueChangedCount { get; private set; }
        public int EndEditCount { get; private set; }
        public int FocusGainedCount { get; private set; }
        public int FocusLostCount { get; private set; }
        public string ValueChangedPayload { get; private set; } = string.Empty;
        public string EndEditPayload { get; private set; } = string.Empty;
        public IReadOnlyList<string> Sequence => sequence;

        public void ResetRecords()
        {
            ValueChangedCount = 0;
            EndEditCount = 0;
            FocusGainedCount = 0;
            FocusLostCount = 0;
            ValueChangedPayload = string.Empty;
            EndEditPayload = string.Empty;
            sequence.Clear();
        }

        public void OnValueChanged(string value)
        {
            ++ValueChangedCount;
            ValueChangedPayload = value;
            sequence.Add("ValueChanged");
            TextInputLifecycleQaStableRecorder.RecordValueChanged();
        }

        public void OnEndEdit(string value)
        {
            ++EndEditCount;
            EndEditPayload = value;
            sequence.Add("EndEdit");
            TextInputLifecycleQaStableRecorder.RecordEndEdit();
        }

        public void OnFocusGained()
        {
            ++FocusGainedCount;
            sequence.Add("FocusGained");
            TextInputLifecycleQaStableRecorder.RecordFocusGained();
        }

        public void OnFocusLost()
        {
            ++FocusLostCount;
            sequence.Add("FocusLost");
            TextInputLifecycleQaStableRecorder.RecordFocusLost();
        }
    }

    public static class TextInputLifecycleQaStableRecorder
    {
        private static bool armed;

        public static bool IsArmed => armed;
        public static int ValueChangedCount { get; private set; }
        public static int EndEditCount { get; private set; }
        public static int FocusGainedCount { get; private set; }
        public static int FocusLostCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ArmForInitialSceneLoad()
        {
            Arm();
        }

        public static void Arm()
        {
            Reset();
            armed = true;
        }

        public static void Disarm()
        {
            armed = false;
        }

        public static void Reset()
        {
            ValueChangedCount = 0;
            EndEditCount = 0;
            FocusGainedCount = 0;
            FocusLostCount = 0;
        }

        internal static void RecordValueChanged()
        {
            if (armed)
            {
                ++ValueChangedCount;
            }
        }

        internal static void RecordEndEdit()
        {
            if (armed)
            {
                ++EndEditCount;
            }
        }

        internal static void RecordFocusGained()
        {
            if (armed)
            {
                ++FocusGainedCount;
            }
        }

        internal static void RecordFocusLost()
        {
            if (armed)
            {
                ++FocusLostCount;
            }
        }
    }
}
