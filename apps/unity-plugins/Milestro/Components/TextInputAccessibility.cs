using System;
using System.Collections.Generic;
using Milestro.Components.Internal;
using Milestro.Util;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
using UnityEngine.Accessibility;
#endif

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextInput))]
    [AddComponentMenu("Milestro/Text Input Accessibility")]
    public sealed class TextInputAccessibility : Selectable
    {
        [SerializeField] private bool m_screenReaderEnabled = true;
        [SerializeField] private string m_label = "";
        [SerializeField] private string m_editingStartedAnnouncement = "Editing";
        [SerializeField] private string m_editingEndedAnnouncement = "Editing ended";
        [SerializeField] private string m_insertedCharactersFormat = "{0} characters inserted";
        [SerializeField] private string m_deletedCharactersFormat = "{0} characters deleted";
        [SerializeField] private string m_selectedCharactersFormat = "{0} characters selected";
        [SerializeField] private string m_passwordCharacterName = "bullet";
        [SerializeField] private string m_blankAnnouncement = "blank";
        [SerializeField] private string m_readOnlyHint = "Read-only";
        [SerializeField] private string m_passwordHint = "Password field";
        [SerializeField][Min(1)] private int m_announcementTextLimit = 100;

        [NonSerialized] private TextInput? textInput;
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
        [NonSerialized] private UnityAccessibilityAdapter? accessibilityAdapter;
        [NonSerialized] private TextInputAccessibilitySnapshot previousSnapshot;
        [NonSerialized] private bool hasPreviousSnapshot;
        [NonSerialized] private readonly List<string> pendingAnnouncements = new List<string>();
        [NonSerialized] internal Action<string>? announcementSinkOverride;
#endif

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            textInput = GetComponent<TextInput>();
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
            accessibilityAdapter = new UnityAccessibilityAdapter(this);
#endif
        }

        protected override void OnEnable()
        {
            base.OnEnable();
#if !UNITY_6000_3_OR_NEWER || !MILESTRO_UNITY_ACCESSIBILITY
            if (m_screenReaderEnabled)
            {
                Debug.LogWarning("TextInputAccessibility screen-reader support requires Unity 6000.3 or newer and the built-in Accessibility module.", this);
            }
#endif
        }

        protected override void OnDisable()
        {
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
            accessibilityAdapter?.Clear();
            hasPreviousSnapshot = false;
#endif
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
            accessibilityAdapter?.Dispose();
            accessibilityAdapter = null;
#endif
            base.OnDestroy();
        }

        private TextInput? TargetTextInput()
        {
            if (textInput == null)
            {
                textInput = GetComponent<TextInput>();
            }

            return textInput;
        }

#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
        private void Update()
        {
            var target = TargetTextInput();
            if (!m_screenReaderEnabled || target == null || !target.isActiveAndEnabled)
            {
                accessibilityAdapter?.Clear();
                hasPreviousSnapshot = false;
                return;
            }

            var snapshot = CaptureSnapshot(target);
            accessibilityAdapter?.Sync(ResolveLabel(target), ResolveHint(target),
                ResolveValue(target), snapshot.Editing);

            if (hasPreviousSnapshot)
            {
                pendingAnnouncements.Clear();
                TextInputAccessibilityAnnouncements.AppendDiff(previousSnapshot, snapshot,
                    BuildAnnouncementStrings(target), pendingAnnouncements);
                foreach (var announcement in pendingAnnouncements)
                {
                    SendAnnouncement(announcement);
                }
            }

            previousSnapshot = snapshot;
            hasPreviousSnapshot = true;
        }

        private static TextInputAccessibilitySnapshot CaptureSnapshot(TextInput target)
        {
            var text = target.Text ?? string.Empty;
            var caret = 0;
            var selectionStart = 0;
            var selectionEnd = 0;
            var box = target.AccessibilityInputBox;
            if (box != null)
            {
                caret = ClampOffset(box.Cursor.Utf16Offset, text.Length);
                var selection = box.Selection;
                if (selection.HasSelection)
                {
                    selectionStart = ClampOffset(box.Utf8ToUtf16(selection.StartUtf8), text.Length);
                    selectionEnd = ClampOffset(box.Utf8ToUtf16(selection.EndUtf8), text.Length);
                    if (selectionEnd < selectionStart)
                    {
                        (selectionStart, selectionEnd) = (selectionEnd, selectionStart);
                    }
                }
                else
                {
                    selectionStart = caret;
                    selectionEnd = caret;
                }
            }

            return new TextInputAccessibilitySnapshot(text, caret, selectionStart, selectionEnd,
                target.IsDispatcherFocused, target.CompositionIsActive);
        }

        private static int ClampOffset(ulong utf16Offset, int textLength)
        {
            return utf16Offset >= (ulong)textLength ? textLength : (int)utf16Offset;
        }

        private string ResolveLabel(TextInput target)
        {
            if (!string.IsNullOrEmpty(m_label))
            {
                return m_label;
            }

            if (target.placeholder is Text placeholderText &&
                !string.IsNullOrEmpty(placeholderText.text))
            {
                return placeholderText.text;
            }

            return target.gameObject.name;
        }

        private string ResolveHint(TextInput target)
        {
            string? hint = null;
            if (target.readOnly)
            {
                hint = m_readOnlyHint;
            }

            if (target.maskInput)
            {
                hint = hint == null ? m_passwordHint : hint + ", " + m_passwordHint;
            }

            return hint ?? "";
        }

        private static string ResolveValue(TextInput target)
        {
            var text = target.Text ?? string.Empty;
            if (!target.maskInput)
            {
                return text;
            }

            var maskChar = target.maskChar == '\0' ? '*' : target.maskChar;
            return new string(maskChar, Utf16Util.CountGraphemeClusters(text));
        }

        private TextInputAccessibilityAnnouncementStrings BuildAnnouncementStrings(TextInput target)
        {
            return new TextInputAccessibilityAnnouncementStrings(
                m_editingStartedAnnouncement,
                m_editingEndedAnnouncement,
                m_insertedCharactersFormat,
                m_deletedCharactersFormat,
                m_selectedCharactersFormat,
                m_passwordCharacterName,
                m_blankAnnouncement,
                m_announcementTextLimit,
                target.maskInput);
        }

        private Rect GetInputScreenRect()
        {
            var target = TargetTextInput();
            return target == null ? default : LocalRectToScreen(target, target.GetPixelAdjustedRect());
        }

        private static Rect LocalRectToScreen(Graphic target, Rect localRect)
        {
            var canvas = target.canvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var worldMin = target.rectTransform.TransformPoint(new Vector3(localRect.xMin, localRect.yMin));
            var worldMax = target.rectTransform.TransformPoint(new Vector3(localRect.xMax, localRect.yMax));
            var screenMin = RectTransformUtility.WorldToScreenPoint(camera, worldMin);
            var screenMax = RectTransformUtility.WorldToScreenPoint(camera, worldMax);
            return Rect.MinMaxRect(Mathf.Min(screenMin.x, screenMax.x),
                Mathf.Min(screenMin.y, screenMax.y),
                Mathf.Max(screenMin.x, screenMax.x),
                Mathf.Max(screenMin.y, screenMax.y));
        }

        private bool OnInvoked()
        {
            var target = TargetTextInput();
            return target != null &&
                   target.isActiveAndEnabled &&
                   target.BeginEditingFromAccessibility();
        }

        private bool OnDismissed()
        {
            TargetTextInput()?.EndEditingFromAccessibility();
            return true;
        }

        private void SendAnnouncement(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (announcementSinkOverride != null)
            {
                announcementSinkOverride(message);
                return;
            }

            AssistiveSupport.notificationDispatcher?.SendAnnouncement(message);
        }

        private sealed class UnityAccessibilityAdapter : IDisposable
        {
            private readonly TextInputAccessibility owner;
            private AccessibilityHierarchy? hierarchy;
            private AccessibilityNode? node;

            internal UnityAccessibilityAdapter(TextInputAccessibility owner)
            {
                this.owner = owner;
            }

            internal void Sync(string label, string hint, string value, bool editing)
            {
                EnsureHierarchy();
                if (hierarchy == null)
                {
                    return;
                }

                if (node == null)
                {
                    node = hierarchy.AddNode(label, null);
                    node.role = AccessibilityRole.TextField;
                    node.frameGetter = owner.GetInputScreenRect;
                    node.invoked += owner.OnInvoked;
                    node.dismissed += owner.OnDismissed;
                }

                node.label = label;
                node.hint = hint;
                node.value = value;
                node.state = editing ? AccessibilityState.Selected : AccessibilityState.None;
                node.allowsDirectInteraction = editing;
            }

            internal void Clear()
            {
                if (node != null && hierarchy != null)
                {
                    hierarchy.RemoveNode(node, true);
                }

                node = null;
            }

            public void Dispose()
            {
                Clear();
                hierarchy = null;
            }

            private void EnsureHierarchy()
            {
                if (hierarchy != null && AssistiveSupport.activeHierarchy == hierarchy)
                {
                    return;
                }

                Clear();
                hierarchy = AssistiveSupport.activeHierarchy;
                if (hierarchy == null)
                {
                    hierarchy = new AccessibilityHierarchy();
                    AssistiveSupport.activeHierarchy = hierarchy;
                }
            }
        }
#endif
    }
}
