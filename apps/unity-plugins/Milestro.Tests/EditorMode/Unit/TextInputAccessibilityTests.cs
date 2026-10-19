using System;
using System.Collections.Generic;
using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using NUnit.Framework;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
using UnityEngine.Accessibility;
#endif
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public class TextInputAccessibilityTests
    {
        private static TextInputAccessibilityAnnouncementStrings TestStrings(bool masked = false, int limit = 4)
        {
            return new TextInputAccessibilityAnnouncementStrings(
                "Editing",
                "Editing ended",
                "{0} characters inserted",
                "{0} characters deleted",
                "{0} characters selected",
                "bullet",
                "blank",
                limit,
                masked);
        }

        private static List<string> Diff(TextInputAccessibilitySnapshot previous,
            TextInputAccessibilitySnapshot current, bool masked = false, int limit = 4)
        {
            var output = new List<string>();
            TextInputAccessibilityAnnouncements.AppendDiff(previous, current,
                TestStrings(masked, limit), output);
            return output;
        }

        private static TextInputAccessibilitySnapshot Caret(string text, int caret,
            bool editing = true, bool composing = false)
        {
            return new TextInputAccessibilitySnapshot(text, caret, caret, caret, editing, composing);
        }

        private static TextInputAccessibilitySnapshot Selection(string text, int start, int end, int caret)
        {
            return new TextInputAccessibilitySnapshot(text, caret, start, end, true, false);
        }

        [Test]
        public void EditingStartAnnouncesAndSuppressesOtherChanges()
        {
            var previous = Caret("a", 1, editing: false);
            var current = new TextInputAccessibilitySnapshot("ab", 2, 2, 2, true, false);
            Assert.That(Diff(previous, current), Is.EqualTo(new[] { "Editing" }));
        }

        [Test]
        public void EditingEndAnnounces()
        {
            Assert.That(Diff(Caret("a", 1), Caret("a", 1, editing: false)),
                Is.EqualTo(new[] { "Editing ended" }));
        }

        [Test]
        public void SilentWhenNotEditing()
        {
            Assert.That(Diff(Caret("a", 1, editing: false), Caret("ab", 2, editing: false)),
                Is.Empty);
        }

        [Test]
        public void SilentDuringComposition()
        {
            Assert.That(Diff(Caret("a", 1), Caret("a", 0, composing: true)), Is.Empty);
        }

        [Test]
        public void InsertionEchoesInsertedText()
        {
            Assert.That(Diff(Caret("ab", 2), Caret("abc", 3)), Is.EqualTo(new[] { "c" }));
        }

        [Test]
        public void DeletionEchoesRemovedText()
        {
            Assert.That(Diff(Caret("abc", 3), Caret("ab", 2)), Is.EqualTo(new[] { "c" }));
        }

        [Test]
        public void LargeInsertionSummarized()
        {
            Assert.That(Diff(Caret("", 0), Caret("abcdef", 6)),
                Is.EqualTo(new[] { "6 characters inserted" }));
        }

        [Test]
        public void LargeDeletionSummarized()
        {
            Assert.That(Diff(Caret("abcdef", 6), Caret("a", 1)),
                Is.EqualTo(new[] { "5 characters deleted" }));
        }

        [Test]
        public void ReplacementAnnouncesInsertedText()
        {
            Assert.That(Diff(Caret("abc", 2), Caret("axc", 2)), Is.EqualTo(new[] { "x" }));
        }

        [Test]
        public void MaskedTextChangesNeverLeakPlaintext()
        {
            Assert.That(Diff(Caret("ab", 2), Caret("abc", 3), masked: true),
                Is.EqualTo(new[] { "bullet" }));
            Assert.That(Diff(Caret("abc", 3), Caret("ab", 2), masked: true),
                Is.EqualTo(new[] { "bullet" }));
        }

        [Test]
        public void MaskedSelectionAnnouncesCountOnly()
        {
            Assert.That(Diff(Caret("secret", 0), Selection("secret", 0, 6, 6), masked: true),
                Is.EqualTo(new[] { "6 characters selected" }));
        }

        [Test]
        public void SelectionEchoesSelectedText()
        {
            Assert.That(Diff(Caret("hello", 0), Selection("hello", 0, 4, 4)),
                Is.EqualTo(new[] { "hell" }));
        }

        [Test]
        public void LargeSelectionSummarized()
        {
            Assert.That(Diff(Caret("hello", 0), Selection("hello", 0, 5, 5)),
                Is.EqualTo(new[] { "5 characters selected" }));
        }

        [Test]
        public void UnchangedSelectionSilent()
        {
            Assert.That(Diff(Selection("hello", 0, 4, 4), Selection("hello", 0, 4, 4)),
                Is.Empty);
        }

        [Test]
        public void SelectionCollapseSilent()
        {
            Assert.That(Diff(Selection("hello", 0, 4, 4), Caret("hello", 0)), Is.Empty);
        }

        [Test]
        public void CaretForwardEchoesCharacter()
        {
            Assert.That(Diff(Caret("ab", 1), Caret("ab", 2)), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void CaretBackwardEchoesCharacter()
        {
            Assert.That(Diff(Caret("ab", 2), Caret("ab", 1)), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void CaretEchoKeepsSurrogatePairsIntact()
        {
            Assert.That(Diff(Caret("a😀b", 1), Caret("a😀b", 3)), Is.EqualTo(new[] { "😀" }));
            Assert.That(Diff(Caret("a😀b", 3), Caret("a😀b", 1)), Is.EqualTo(new[] { "😀" }));
        }

        [Test]
        public void WordJumpAnnouncesWord()
        {
            Assert.That(Diff(Caret("foo bar", 0), Caret("foo bar", 4)),
                Is.EqualTo(new[] { "bar" }));
        }

        [Test]
        public void LineJumpAnnouncesLine()
        {
            Assert.That(Diff(Caret("foo\nbar", 1), Caret("foo\nbar", 5)),
                Is.EqualTo(new[] { "bar" }));
        }

        [Test]
        public void BlankLineAnnounced()
        {
            Assert.That(Diff(Caret("foo\n\nbar", 1), Caret("foo\n\nbar", 4)),
                Is.EqualTo(new[] { "blank" }));
        }

        [Test]
        public void MaskedCaretEchoUsesPasswordCharacter()
        {
            Assert.That(Diff(Caret("secret", 1), Caret("secret", 2), masked: true),
                Is.EqualTo(new[] { "bullet" }));
            Assert.That(Diff(Caret("secret", 0), Caret("secret", 4), masked: true),
                Is.Empty);
        }

#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void InvokeUpdate(TextInputAccessibility accessibility)
        {
            typeof(TextInputAccessibility).GetMethod("Update", DeclaredInstance)!
                .Invoke(accessibility, null);
        }

        [Test]
        public void ScreenReaderNodePublishesValueAndClearsOnDisable()
        {
            var previousHierarchy = AssistiveSupport.activeHierarchy;
            var hierarchy = new AccessibilityHierarchy();
            var owner = new GameObject("Nickname", typeof(RectTransform));
            try
            {
                AssistiveSupport.activeHierarchy = hierarchy;
                var input = owner.AddComponent<TextInput>();
                input.Text = "hello";
                var accessibility = owner.AddComponent<TextInputAccessibility>();
                InvokeUpdate(accessibility);

                Assert.That(hierarchy.rootNodes.Count, Is.EqualTo(1));
                var node = hierarchy.rootNodes[0];
                Assert.That(node.role, Is.EqualTo(AccessibilityRole.TextField));
                Assert.That(node.label, Is.EqualTo("Nickname"));
                Assert.That(node.value, Is.EqualTo("hello"));

                input.Text = "hello world";
                InvokeUpdate(accessibility);
                Assert.That(node.value, Is.EqualTo("hello world"));

                input.maskInput = true;
                InvokeUpdate(accessibility);
                Assert.That(node.value, Is.EqualTo(new string('*', 11)));
                Assert.That(node.hint, Does.Contain("Password"));

                accessibility.enabled = false;
                Assert.That(hierarchy.rootNodes, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                AssistiveSupport.activeHierarchy = previousHierarchy;
            }
        }

        [Test]
        public void EditingTransitionsAnnounceThroughSink()
        {
            var previousHierarchy = AssistiveSupport.activeHierarchy;
            var hierarchy = new AccessibilityHierarchy();
            var owner = new GameObject("Editing input", typeof(RectTransform));
            try
            {
                AssistiveSupport.activeHierarchy = hierarchy;
                var input = owner.AddComponent<TextInput>();
                var accessibility = owner.AddComponent<TextInputAccessibility>();
                var captured = new List<string>();
                accessibility.announcementSinkOverride = captured.Add;
                InvokeUpdate(accessibility);

                var began = input.BeginEditingFromAccessibility();
                Assert.That(began, Is.EqualTo(input.IsDispatcherFocused));
                InvokeUpdate(accessibility);
                input.EndEditingFromAccessibility();
                InvokeUpdate(accessibility);

                if (began)
                {
                    Assert.That(captured, Is.EqualTo(new[] { "Editing", "Editing ended" }));
                }
                else
                {
                    Assert.That(captured, Is.Empty);
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
                AssistiveSupport.activeHierarchy = previousHierarchy;
            }
        }
#endif
    }
}
