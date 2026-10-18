using System;
using System.Collections.Generic;
using System.Reflection;
using Milestro.Components;
using Milestro.Input;
using Milestro.Model;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public class TextInputFormTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject owner = null!;
        private TextInput input = null!;
        private InputBox? editor;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Form input", typeof(RectTransform));
            owner.SetActive(false);
            input = owner.AddComponent<TextInput>();
        }

        [TearDown]
        public void TearDown()
        {
            editor?.Dispose();
            SetField("inputBox", null);
            Object.DestroyImmediate(owner);
            editor = null;
        }

        [Test]
        public void LimitCountsUnicodeScalarsAndSilentAssignmentsStaySilent()
        {
            var values = new List<string>();
            input.onValueChanged.AddListener(values.Add);
            input.characterLimit = 2;
            input.Text = "😀中a";
            input.SetTextWithoutNotify("a😀b");
            Assert.That(input.Text, Is.EqualTo("a😀"));
            Assert.That(values, Is.EqualTo(new[] { "😀中" }));
            input.characterLimit = 1;
            Assert.That(input.Text, Is.EqualTo("a"));
            input.characterLimit = -1;
            input.Text = "😀中文abc";
            Assert.That(input.characterLimit, Is.Zero);
            Assert.That(input.Text, Is.EqualTo("😀中文abc"));
        }

        [TestCase(TextInputContentType.IntegerNumber, "-1a2-3", "-123")]
        [TestCase(TextInputContentType.DecimalNumber, "-1.2.3x", "-1.23")]
        [TestCase(TextInputContentType.Alphanumeric, "A中b!2", "Ab2")]
        [TestCase(TextInputContentType.Name, " jANE  o'NEIL 3", "Jane O'Neil ")]
        [TestCase(TextInputContentType.Name, "李小龍", "李小龍")]
        [TestCase(TextInputContentType.EmailAddress, "a@@b..com 中", "a@b.com")]
        [TestCase(TextInputContentType.Pin, "-12.3x", "123")]
        public void ContentPresetsFilterWholeValues(TextInputContentType contentType, string value, string expected)
        {
            input.contentType = contentType;
            input.Text = value;
            Assert.That(input.Text, Is.EqualTo(expected));
        }

        [Test]
        public void PasswordPresetMasksAndPreventsCopyAndCustomSettingsRemainEditable()
        {
            input.contentType = TextInputContentType.Password;
            Assert.That(input.maskInput, Is.True);
            Assert.That(input.allowCopy, Is.False);
            input.contentType = TextInputContentType.EmailAddress;
            Assert.That(input.maskInput, Is.False);
            Assert.That(input.allowCopy, Is.True);
            Assert.That(input.keyboardType, Is.EqualTo(TouchScreenKeyboardType.EmailAddress));
            input.keyboardType = TouchScreenKeyboardType.URL;
            Assert.That(input.contentType, Is.EqualTo(TextInputContentType.Custom));
            Assert.That(input.characterValidation, Is.EqualTo(TextInputCharacterValidation.EmailAddress));
        }

        [Test]
        public void CustomValidationReceivesUnicodeScalarsAndUtf16InsertionPosition()
        {
            var positions = new List<int>();
            input.onValidateInput = (text, position, scalar) =>
            {
                positions.Add(position);
                return scalar == 'x' ? 0 : scalar == 'a' ? 0x1f600 : scalar;
            };
            input.Text = "abx";
            Assert.That(input.Text, Is.EqualTo("😀b"));
            Assert.That(positions, Is.EqualTo(new[] { 0, 2, 3 }));
            CreateNativeEditor();
            Assert.That(input.Text, Is.EqualTo("😀b"));
            Assert.That(positions, Has.Count.EqualTo(3));
        }

        [TestCase(-1)]
        [TestCase(0xd800)]
        [TestCase(0x110000)]
        public void InvalidCustomScalarIsRejected(int scalar)
        {
            input.onValidateInput = (_, __, ___) => scalar;
            input.Text = "abc";
            Assert.That(input.Text, Is.Empty);
        }

        [Test]
        public void PlaceholderTracksSilentTextAndCompositionWithoutBecomingTheValue()
        {
            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderObject.transform.SetParent(owner.transform, false);
            var graphic = placeholderObject.GetComponent<Text>();
            input.placeholder = graphic;
            Assert.That(graphic.enabled, Is.True);
            input.SetTextWithoutNotify("filled");
            Assert.That(graphic.enabled, Is.False);
            input.SetTextWithoutNotify("");
            Assert.That(graphic.enabled, Is.True);
            CreateNativeEditor();
            ReadFrame(HybridInputEvent.Composition("候选", 1d));
            Assert.That(input.Text, Is.Empty);
            Assert.That(graphic.enabled, Is.False);
            ReadFrame(HybridInputEvent.Composition("", 2d));
            Assert.That(graphic.enabled, Is.True);
            ReadFrame(HybridInputEvent.Composition("候选", 3d));
            input.readOnly = true;
            Assert.That(graphic.enabled, Is.True);
            Assert.That(editor!.ClearComposition(), Is.False);
            Assert.Throws<ArgumentException>(() => input.placeholder = input);
        }

        [Test]
        public void SelectionReplacementUsesUtf8OffsetsAndAvailableCharacterBudget()
        {
            input.characterLimit = 3;
            input.Text = "中😀z";
            TextInputEditContext? editContext = null;
            input.onValidateEdit = context =>
            {
                editContext = context;
                return TextInputEditResult.Accept();
            };
            CreateNativeEditor();
            editor!.MoveDocumentStart();
            editor.MoveNext();
            editor.MoveNext(true);
            ReadFrame(HybridInputEvent.CommittedText("ab", 1d));
            Assert.That(input.Text, Is.EqualTo("中az"));
            Assert.That(editContext, Is.Not.Null);
            Assert.That(editContext!.Value.CurrentText, Is.EqualTo("中😀z"));
            Assert.That(editContext.Value.ProposedText, Is.EqualTo("中az"));
            Assert.That(editContext.Value.InsertedText, Is.EqualTo("a"));
            Assert.That(editContext.Value.SelectionStart, Is.EqualTo(1));
            Assert.That(editContext.Value.SelectionEnd, Is.EqualTo(3));
            Assert.That(editContext.Value.HasSelection, Is.True);
            Assert.That(editor.Text, Is.EqualTo(input.Text));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Text, Is.EqualTo("中😀z"));
            Assert.That(editor.Redo(), Is.True);
            Assert.That(editor.Text, Is.EqualTo("中az"));
        }

        [Test]
        public void RejectedInputPreservesSelectionAndDoesNotNotify()
        {
            input.contentType = TextInputContentType.IntegerNumber;
            input.Text = "12";
            CreateNativeEditor();
            editor!.SelectAll();
            var calls = 0;
            input.onValueChanged.AddListener(_ => ++calls);
            ReadFrame(HybridInputEvent.CommittedText("abc", 1d));
            Assert.That(input.Text, Is.EqualTo("12"));
            Assert.That(editor.Selection.HasSelection, Is.True);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void ImePreeditIsUnrestrictedButCommitIsValidatedWithoutFallbackToPreedit()
        {
            input.contentType = TextInputContentType.IntegerNumber;
            input.characterLimit = 2;
            CreateNativeEditor();
            ReadFrame(HybridInputEvent.Composition("invalid", 1d));
            Assert.That(input.Text, Is.Empty);
            ReadFrame(HybridInputEvent.CommittedText("abc", 2d));
            Assert.That(input.Text, Is.Empty);
            Assert.That(editor!.ClearComposition(), Is.False);
            ReadFrame(HybridInputEvent.Composition("12345", 3d));
            ReadFrame(HybridInputEvent.CommittedText("1x23", 4d));
            Assert.That(input.Text, Is.EqualTo("12"));
        }

        [Test]
        public void EditValidatorSeesCodePointFilteredZwJSequenceAndRejectsItAtomically()
        {
            const string family = "👨‍👩‍👧‍👦";
            var codePoints = new List<int>();
            var contexts = new List<TextInputEditContext>();
            input.onValidateInput = (text, position, codePoint) =>
            {
                codePoints.Add(codePoint);
                return codePoint;
            };
            input.onValidateEdit = context =>
            {
                contexts.Add(context);
                return TextInputEditResult.Reject();
            };
            CreateNativeEditor();

            ReadFrame(HybridInputEvent.CommittedText(family, 1d));

            Assert.That(codePoints, Has.Count.EqualTo(7));
            Assert.That(codePoints.FindAll(codePoint => codePoint == 0x200d), Has.Count.EqualTo(3));
            Assert.That(contexts, Has.Count.EqualTo(1));
            Assert.That(contexts[0].InsertedText, Is.EqualTo(family));
            Assert.That(contexts[0].ProposedText, Is.EqualTo(family));
            Assert.That(contexts[0].Source, Is.EqualTo(TextInputEditSource.Typing));
            Assert.That(input.Text, Is.Empty);
            Assert.That(editor!.Text, Is.Empty);
        }

        [Test]
        public void EditValidatorReplacementAppliesExistingRulesWithoutRecursing()
        {
            input.characterLimit = 2;
            input.Text = "a";
            CreateNativeEditor();
            editor!.MoveDocumentEnd();
            var calls = 0;
            input.onValidateEdit = context =>
            {
                ++calls;
                Assert.That(context.ProposedText, Is.EqualTo("ab"));
                return TextInputEditResult.Replace("😀xyz");
            };

            ReadFrame(HybridInputEvent.CommittedText("b", 1d));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(input.Text, Is.EqualTo("😀x"));
            Assert.That(editor!.Text, Is.EqualTo("😀x"));
            Assert.That(editor.Cursor.Utf16Offset, Is.EqualTo(3));
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Text, Is.EqualTo("a"));
            Assert.That(editor.Redo(), Is.True);
            Assert.That(editor.Text, Is.EqualTo("😀x"));
        }

        [Test]
        public void EditValidatorReceivesTypingPasteImeAndProgrammaticSources()
        {
            var previousClipboard = GUIUtility.systemCopyBuffer;
            try
            {
                var sources = new List<TextInputEditSource>();
                input.onValidateEdit = context =>
                {
                    sources.Add(context.Source);
                    return TextInputEditResult.Accept();
                };
                input.Text = "P";
                CreateNativeEditor();
                editor!.MoveDocumentEnd();
                ReadFrame(HybridInputEvent.CommittedText("T", 1d));
                ReadFrame(HybridInputEvent.Composition("I", 2d));
                ReadFrame(HybridInputEvent.CommittedText("I", 3d));
                GUIUtility.systemCopyBuffer = "V";
                var paste = new HybridInputFrame(4, 4d, "test", 1, 1,
                    new[] { HybridInputEvent.KeyState(KeyCode.V, true, 4d) }, new[] { KeyCode.LeftControl });
                Invoke("ReadKeyboardInput", paste);

                Assert.That(sources, Is.EqualTo(new[]
                {
                    TextInputEditSource.Programmatic,
                    TextInputEditSource.Typing,
                    TextInputEditSource.ImeCommit,
                    TextInputEditSource.Paste
                }));
                Assert.That(input.Text, Is.EqualTo("PTIV"));
            }
            finally
            {
                GUIUtility.systemCopyBuffer = previousClipboard;
            }
        }

        [Test]
        public void EditValidatorCanReplaceWholeValueWithEmptyText()
        {
            input.Text = "abc";
            CreateNativeEditor();
            input.onValidateEdit = context => TextInputEditResult.Replace(string.Empty);

            ReadFrame(HybridInputEvent.CommittedText("x", 1d));

            Assert.That(input.Text, Is.Empty);
            Assert.That(editor!.Text, Is.Empty);
            Assert.That(editor.Undo(), Is.True);
            Assert.That(editor.Text, Is.EqualTo("abc"));
        }

        [Test]
        public void EditValidatorCanRejectProgrammaticAssignmentWithoutNotification()
        {
            input.Text = "keep";
            var notifications = 0;
            input.onValueChanged.AddListener(_ => ++notifications);
            input.onValidateEdit = context => TextInputEditResult.Reject();

            input.Text = "new";
            input.SetTextWithoutNotify("silent");

            Assert.That(input.Text, Is.EqualTo("keep"));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void ChangingRulesClearsHistoryThatCouldRestoreInvalidValues()
        {
            CreateNativeEditor();
            ReadFrame(HybridInputEvent.CommittedText("abc", 1d));
            editor!.SelectAll();
            ReadFrame(HybridInputEvent.CommittedText("123", 2d));
            input.contentType = TextInputContentType.IntegerNumber;
            Assert.That(input.Text, Is.EqualTo("123"));
            Assert.That(editor.Undo(), Is.False);
        }

        [Test]
        public void PasteUsesSameValidationAndLimitAsTyping()
        {
            var previousClipboard = GUIUtility.systemCopyBuffer;
            try
            {
                input.contentType = TextInputContentType.DecimalNumber;
                input.characterLimit = 4;
                CreateNativeEditor();
                GUIUtility.systemCopyBuffer = "-1x.234";
                var frame = new HybridInputFrame(1, 1d, "test", 1, 1,
                    new[] { HybridInputEvent.KeyState(KeyCode.V, true, 1d) },
                    new[] { KeyCode.LeftControl });
                Invoke("ReadKeyboardInput", frame);
                Assert.That(input.Text, Is.EqualTo("-1.2"));
            }
            finally
            {
                GUIUtility.systemCopyBuffer = previousClipboard;
            }
        }

        [Test]
        public void MultilineEnterInsertsOneNewlineAndSubmitModeAllowsShiftEnter()
        {
            input.lineMode = TextInputLineMode.MultiLine;
            CreateNativeEditor();
            ReadFrame(HybridInputEvent.CommittedText("\n", 1d),
                HybridInputEvent.KeyState(KeyCode.Return, true, 1d));
            Assert.That(input.Text, Is.EqualTo("\n"));
            input.submitOnEnter = true;
            ReadFrame(HybridInputEvent.CommittedText("\n", 2d),
                HybridInputEvent.KeyState(KeyCode.Return, true, 2d));
            Assert.That(input.Text, Is.EqualTo("\n"));
            var shiftEnter = new HybridInputFrame(3, 3d, "test", 1, 1,
                new[] { HybridInputEvent.KeyState(KeyCode.Return, true, 3d) }, new[] { KeyCode.LeftShift });
            Invoke("ReadKeyboardInput", shiftEnter);
            Assert.That(input.Text, Is.EqualTo("\n\n"));
        }

        [Test]
        public void InspectorValidationPreservesExistingCustomMaskSettings()
        {
            input.maskInput = true;
            input.allowCopy = false;
            Invoke("OnValidate");
            Assert.That(input.maskInput, Is.True);
            Assert.That(input.allowCopy, Is.False);
        }

        private void CreateNativeEditor()
        {
            SetField("rectTransformCache", owner.GetComponent<RectTransform>());
            Invoke("RecreateInputBox");
            editor = (InputBox)typeof(TextInput).GetField("inputBox", PrivateInstance)!.GetValue(input)!;
        }

        private void ReadFrame(params HybridInputEvent[] events)
        {
            Invoke("ReadKeyboardInput", new HybridInputFrame(1, 1d, "test", 1, 1, events, Array.Empty<KeyCode>()));
        }

        private void SetField(string name, object? value)
        {
            typeof(TextInput).GetField(name, PrivateInstance)!.SetValue(input, value);
        }

        private void Invoke(string name, params object[] arguments)
        {
            typeof(TextInput).GetMethod(name, PrivateInstance)!.Invoke(input, arguments);
        }
    }
}
