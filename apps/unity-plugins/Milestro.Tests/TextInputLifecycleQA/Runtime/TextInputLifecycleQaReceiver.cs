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
        }

        public void OnEndEdit(string value)
        {
            ++EndEditCount;
            EndEditPayload = value;
            sequence.Add("EndEdit");
        }

        public void OnFocusGained()
        {
            ++FocusGainedCount;
            sequence.Add("FocusGained");
        }

        public void OnFocusLost()
        {
            ++FocusLostCount;
            sequence.Add("FocusLost");
        }
    }
}
