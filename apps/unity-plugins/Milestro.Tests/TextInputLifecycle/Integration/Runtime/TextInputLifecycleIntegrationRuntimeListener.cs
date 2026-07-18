using System;
using System.Collections.Generic;
using Milestro.Components;
using UnityEngine;

namespace Milestro.Tests.TextInputLifecycle.Integration
{
    public sealed class TextInputLifecycleIntegrationRuntimeListener : MonoBehaviour
    {
        [SerializeField] private TextInput? input;

        private readonly List<string> sequence = new List<string>();
        private bool bound;

        public int ValueChangedCount { get; private set; }
        public int EndEditCount { get; private set; }
        public int FocusGainedCount { get; private set; }
        public int FocusLostCount { get; private set; }
        public string ValueChangedPayload { get; private set; } = string.Empty;
        public string EndEditPayload { get; private set; } = string.Empty;
        public IReadOnlyList<string> Sequence => sequence;

        public bool IsBoundTo(TextInput target)
        {
            return bound && input == target;
        }

        public void Configure(TextInput target)
        {
            Unbind();
            input = target;
            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

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

        public TextInputLifecycleIntegrationRecordsSnapshot CaptureRecords()
        {
            return new TextInputLifecycleIntegrationRecordsSnapshot(ValueChangedCount,
                EndEditCount,
                FocusGainedCount,
                FocusLostCount,
                ValueChangedPayload,
                EndEditPayload,
                sequence.ToArray());
        }

        public void RestoreRecords(TextInputLifecycleIntegrationRecordsSnapshot snapshot)
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

        public bool RecordsMatch(TextInputLifecycleIntegrationRecordsSnapshot snapshot)
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

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (bound || input == null)
            {
                return;
            }
            input.onValueChanged.AddListener(OnValueChanged);
            input.onEndEdit.AddListener(OnEndEdit);
            input.onFocusGained.AddListener(OnFocusGained);
            input.onFocusLost.AddListener(OnFocusLost);
            bound = true;
        }

        private void Unbind()
        {
            if (!bound || input == null)
            {
                bound = false;
                return;
            }
            input.onValueChanged.RemoveListener(OnValueChanged);
            input.onEndEdit.RemoveListener(OnEndEdit);
            input.onFocusGained.RemoveListener(OnFocusGained);
            input.onFocusLost.RemoveListener(OnFocusLost);
            bound = false;
        }

        private void OnValueChanged(string value)
        {
            ++ValueChangedCount;
            ValueChangedPayload = value;
            sequence.Add("ValueChanged");
        }

        private void OnEndEdit(string value)
        {
            ++EndEditCount;
            EndEditPayload = value;
            sequence.Add("EndEdit");
        }

        private void OnFocusGained()
        {
            ++FocusGainedCount;
            sequence.Add("FocusGained");
        }

        private void OnFocusLost()
        {
            ++FocusLostCount;
            sequence.Add("FocusLost");
        }
    }
}
