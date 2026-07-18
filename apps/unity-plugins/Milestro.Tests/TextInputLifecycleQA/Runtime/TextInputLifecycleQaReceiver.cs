using System;
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

        public TextInputLifecycleQaRecordsSnapshot CaptureRecords()
        {
            return new TextInputLifecycleQaRecordsSnapshot(ValueChangedCount,
                EndEditCount,
                FocusGainedCount,
                FocusLostCount,
                ValueChangedPayload,
                EndEditPayload,
                sequence.ToArray());
        }

        public void RestoreRecords(TextInputLifecycleQaRecordsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            ValueChangedCount = snapshot.ValueChangedCount;
            EndEditCount = snapshot.EndEditCount;
            FocusGainedCount = snapshot.FocusGainedCount;
            FocusLostCount = snapshot.FocusLostCount;
            ValueChangedPayload = snapshot.ValueChangedPayload;
            EndEditPayload = snapshot.EndEditPayload;
            sequence.Clear();
            sequence.AddRange(snapshot.Sequence);
        }

        public bool RecordsMatch(TextInputLifecycleQaRecordsSnapshot snapshot)
        {
            if (snapshot == null || ValueChangedCount != snapshot.ValueChangedCount ||
                EndEditCount != snapshot.EndEditCount ||
                FocusGainedCount != snapshot.FocusGainedCount ||
                FocusLostCount != snapshot.FocusLostCount ||
                ValueChangedPayload != snapshot.ValueChangedPayload ||
                EndEditPayload != snapshot.EndEditPayload ||
                sequence.Count != snapshot.Sequence.Length)
            {
                return false;
            }
            for (var index = 0; index < sequence.Count; ++index)
            {
                if (sequence[index] != snapshot.Sequence[index])
                {
                    return false;
                }
            }
            return true;
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

    public sealed class TextInputLifecycleQaRecordsSnapshot
    {
        public TextInputLifecycleQaRecordsSnapshot(int valueChangedCount,
            int endEditCount,
            int focusGainedCount,
            int focusLostCount,
            string valueChangedPayload,
            string endEditPayload,
            string[] sequence)
        {
            ValueChangedCount = valueChangedCount;
            EndEditCount = endEditCount;
            FocusGainedCount = focusGainedCount;
            FocusLostCount = focusLostCount;
            ValueChangedPayload = valueChangedPayload;
            EndEditPayload = endEditPayload;
            Sequence = sequence;
        }

        public int ValueChangedCount { get; }
        public int EndEditCount { get; }
        public int FocusGainedCount { get; }
        public int FocusLostCount { get; }
        public string ValueChangedPayload { get; }
        public string EndEditPayload { get; }
        public string[] Sequence { get; }
    }

    public static class TextInputLifecycleQaStableRecorder
    {
        private static bool armed;

        public static bool IsArmed => armed;
        public static string ArmedSourceHead { get; private set; } = string.Empty;
        public static string ArmedSourceTree { get; private set; } = string.Empty;
        public static int ValueChangedCount { get; private set; }
        public static int EndEditCount { get; private set; }
        public static int FocusGainedCount { get; private set; }
        public static int FocusLostCount { get; private set; }

        public static void Arm()
        {
            Reset();
            ArmedSourceHead = string.Empty;
            ArmedSourceTree = string.Empty;
            armed = true;
        }

        public static void ArmForTargetScene(string sourceHead, string sourceTree)
        {
            Reset();
            ArmedSourceHead = sourceHead;
            ArmedSourceTree = sourceTree;
            armed = true;
        }

        public static void Disarm()
        {
            armed = false;
            ArmedSourceHead = string.Empty;
            ArmedSourceTree = string.Empty;
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
