using Milestro.Components.Internal;
using Milestro.Input;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextInputTouchKeyboardControllerTests
    {
        private const string HelloText = "hello";

        [Test]
        public void HiddenKeyboardOpensWithOptionsAndClampsSelection()
        {
            var source = new FakeKeyboardSource
            {
                Platform = RuntimePlatform.IPhonePlayer,
                TouchSupported = true
            };
            var controller = new TextInputTouchKeyboardController(source);

            var opened = controller.Open(Settings(hideMobileInput: true,
                    keyboardType: TouchScreenKeyboardType.EmailAddress,
                    autocorrection: true,
                    multiline: true),
                HelloText,
                new RangeInt(2, 20));

            Assert.That(opened, Is.True);
            Assert.That(controller.IsActive, Is.True);
            Assert.That(controller.UsesHiddenInput, Is.True);
            Assert.That(source.HideInputValue, Is.True);
            Assert.That(source.OpenCount, Is.EqualTo(1));
            Assert.That(source.LastText, Is.EqualTo(HelloText));
            Assert.That(source.LastKeyboardType, Is.EqualTo(TouchScreenKeyboardType.EmailAddress));
            Assert.That(source.LastAutocorrection, Is.True);
            Assert.That(source.LastMultiline, Is.True);
            Assert.That(source.Keyboard.Selection, Is.EqualTo(new RangeInt(2, 3)));
        }

        [Test]
        public void HiddenKeyboardNeverPollsTextBack()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(hideMobileInput: true), HelloText, new RangeInt(5, 0));
            source.Keyboard.Text = "native typed";
            source.Keyboard.Status = TouchScreenKeyboard.Status.Visible;

            var poll = controller.Poll();

            Assert.That(poll.HasSnapshot, Is.False);
            Assert.That(poll.Text, Is.EqualTo(string.Empty));
            Assert.That(poll.Action, Is.EqualTo(TextInputTouchKeyboardAction.None));
        }

        [Test]
        public void HiddenKeyboardDoneHasNoSnapshot()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(hideMobileInput: true), HelloText, new RangeInt(5, 0));
            source.Keyboard.Text = "typed";
            source.Keyboard.Status = TouchScreenKeyboard.Status.Done;

            var poll = controller.Poll();

            Assert.That(poll.Action, Is.EqualTo(TextInputTouchKeyboardAction.Submit));
            Assert.That(poll.HasSnapshot, Is.False);
        }

        [Test]
        public void VisibleKeyboardPollsTextAndSelectionSnapshots()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.Android };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(hideMobileInput: false), string.Empty, new RangeInt(0, 0));
            source.Keyboard.Text = "abc";
            source.Keyboard.Selection = new RangeInt(1, 2);
            source.Keyboard.Status = TouchScreenKeyboard.Status.Visible;

            var poll = controller.Poll();

            Assert.That(controller.UsesHiddenInput, Is.False);
            Assert.That(poll.HasSnapshot, Is.True);
            Assert.That(poll.Action, Is.EqualTo(TextInputTouchKeyboardAction.Snapshot));
            Assert.That(poll.Text, Is.EqualTo("abc"));
            Assert.That(poll.Selection, Is.EqualTo(new RangeInt(1, 2)));
        }

        [Test]
        public void VisibleKeyboardClampsPolledSelectionToText()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.Android };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(hideMobileInput: false), string.Empty, new RangeInt(0, 0));
            source.Keyboard.Text = "abc";
            source.Keyboard.Selection = new RangeInt(2, 10);
            source.Keyboard.Status = TouchScreenKeyboard.Status.Visible;

            var poll = controller.Poll();

            Assert.That(poll.Selection, Is.EqualTo(new RangeInt(2, 1)));
        }

        [Test]
        public void VisibleKeyboardWithoutSelectionAccessOmitsSelection()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.Android };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(hideMobileInput: false), string.Empty, new RangeInt(0, 0));
            source.Keyboard.CanGetSelection = false;
            source.Keyboard.Status = TouchScreenKeyboard.Status.Visible;

            var poll = controller.Poll();

            Assert.That(poll.HasSnapshot, Is.True);
            Assert.That(poll.Selection.HasValue, Is.False);
        }

        [Test]
        public void VisibleKeyboardDoneAndCancelExposeFinalSnapshot()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.Android };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(hideMobileInput: false), string.Empty, new RangeInt(0, 0));
            source.Keyboard.Text = "final";
            source.Keyboard.Status = TouchScreenKeyboard.Status.Done;

            var donePoll = controller.Poll();

            Assert.That(donePoll.Action, Is.EqualTo(TextInputTouchKeyboardAction.Submit));
            Assert.That(donePoll.HasSnapshot, Is.True);
            Assert.That(donePoll.Text, Is.EqualTo("final"));

            source.Keyboard.Status = TouchScreenKeyboard.Status.Canceled;

            var cancelPoll = controller.Poll();

            Assert.That(cancelPoll.Action, Is.EqualTo(TextInputTouchKeyboardAction.Cancel));
            Assert.That(cancelPoll.HasSnapshot, Is.True);
            Assert.That(cancelPoll.Text, Is.EqualTo("final"));
        }

        [Test]
        public void StatusChangesPublishOncePerTransition()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(), HelloText, new RangeInt(0, 0));
            source.Keyboard.Status = TouchScreenKeyboard.Status.Visible;

            var first = controller.Poll();
            var second = controller.Poll();
            source.Keyboard.Status = TouchScreenKeyboard.Status.Done;
            var third = controller.Poll();

            Assert.That(first.StatusChanged, Is.True);
            Assert.That(first.Status, Is.EqualTo(TouchScreenKeyboard.Status.Visible));
            Assert.That(second.StatusChanged, Is.False);
            Assert.That(third.StatusChanged, Is.True);
            Assert.That(third.Status, Is.EqualTo(TouchScreenKeyboard.Status.Done));
        }

        [Test]
        public void CloseIsIdempotent()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(), HelloText, new RangeInt(0, 0));

            controller.Close();
            controller.Close();

            Assert.That(controller.IsActive, Is.False);
            Assert.That(source.Keyboard.DeactivateCount, Is.EqualTo(1));
        }

        [Test]
        public void AndroidSkipsSoftKeyboardWhenInPlaceEditingIsAllowed()
        {
            var source = new FakeKeyboardSource
            {
                Platform = RuntimePlatform.Android,
                IsInPlaceEditingAllowed = true
            };
            var controller = new TextInputTouchKeyboardController(source);

            var opened = controller.Open(Settings(), HelloText, new RangeInt(0, 0));

            Assert.That(opened, Is.False);
            Assert.That(controller.IsActive, Is.False);
            Assert.That(source.OpenCount, Is.EqualTo(0));
        }

        [Test]
        public void ReadOnlyDoesNotOpen()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);

            var opened = controller.Open(Settings(readOnly: true), HelloText, new RangeInt(0, 0));

            Assert.That(opened, Is.False);
            Assert.That(source.OpenCount, Is.EqualTo(0));
        }

        [Test]
        public void UnsupportedSourceDoesNotOpen()
        {
            var source = new FakeKeyboardSource
            {
                Platform = RuntimePlatform.IPhonePlayer,
                IsSupported = false
            };
            var controller = new TextInputTouchKeyboardController(source);

            var opened = controller.Open(Settings(), HelloText, new RangeInt(0, 0));

            Assert.That(opened, Is.False);
            Assert.That(source.OpenCount, Is.EqualTo(0));
        }

        [Test]
        public void ReopenWithSameConfigurationReusesSession()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(characterLimit: 4), HelloText, new RangeInt(0, 0));

            var reopened = controller.Open(Settings(characterLimit: 7), HelloText, new RangeInt(1, 0));

            Assert.That(reopened, Is.True);
            Assert.That(source.OpenCount, Is.EqualTo(1));
            Assert.That(source.Keyboard.CharacterLimit, Is.EqualTo(7));
            Assert.That(source.Keyboard.Selection, Is.EqualTo(new RangeInt(1, 0)));
            Assert.That(source.Keyboard.DeactivateCount, Is.EqualTo(0));
        }

        [Test]
        public void ConfigurationChangeReopensSession()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);
            controller.Open(Settings(keyboardType: TouchScreenKeyboardType.Default), HelloText,
                new RangeInt(0, 0));
            var oldKeyboard = source.Keyboard;

            var reopened = controller.Open(Settings(keyboardType: TouchScreenKeyboardType.NumberPad),
                HelloText,
                new RangeInt(0, 0));

            Assert.That(reopened, Is.True);
            Assert.That(source.OpenCount, Is.EqualTo(2));
            Assert.That(oldKeyboard.DeactivateCount, Is.EqualTo(1));
            Assert.That(source.Keyboard, Is.Not.SameAs(oldKeyboard));
            Assert.That(source.LastKeyboardType, Is.EqualTo(TouchScreenKeyboardType.NumberPad));
        }

        [Test]
        public void SecureDisablesAutocorrection()
        {
            var source = new FakeKeyboardSource { Platform = RuntimePlatform.IPhonePlayer };
            var controller = new TextInputTouchKeyboardController(source);

            controller.Open(Settings(autocorrection: true, secure: true), HelloText, new RangeInt(0, 0));

            Assert.That(source.LastAutocorrection, Is.False);
            Assert.That(source.LastSecure, Is.True);
        }

        private static TextInputTouchKeyboardSettings Settings(
            bool hideMobileInput = true,
            TouchScreenKeyboardType keyboardType = TouchScreenKeyboardType.Default,
            bool autocorrection = false,
            bool multiline = false,
            bool secure = false,
            bool readOnly = false,
            int characterLimit = 0)
        {
            return new TextInputTouchKeyboardSettings(
                new HybridInputKeyboardOptions(keyboardType, autocorrection, multiline, secure, readOnly),
                characterLimit,
                hideMobileInput);
        }

        private sealed class FakeKeyboardSource : ITextInputTouchKeyboardSource
        {
            public bool IsSupported { get; set; } = true;
            public bool IsInPlaceEditingAllowed { get; set; }
            public bool TouchSupported { get; set; } = true;
            public RuntimePlatform Platform { get; set; } = RuntimePlatform.IPhonePlayer;
            internal bool HideInputValue { get; private set; }
            internal int OpenCount { get; private set; }
            internal string LastText { get; private set; } = string.Empty;
            internal TouchScreenKeyboardType LastKeyboardType { get; private set; }
            internal bool LastAutocorrection { get; private set; }
            internal bool LastMultiline { get; private set; }
            internal bool LastSecure { get; private set; }
            internal int LastCharacterLimit { get; private set; }
            internal FakeKeyboardHandle Keyboard { get; private set; } = null!;

            public bool HideInput
            {
                set => HideInputValue = value;
            }

            public ITextInputTouchKeyboardHandle Open(string text,
                TouchScreenKeyboardType keyboardType,
                bool autocorrection,
                bool multiline,
                bool secure,
                bool alert,
                string textPlaceholder,
                int characterLimit)
            {
                ++OpenCount;
                LastText = text;
                LastKeyboardType = keyboardType;
                LastAutocorrection = autocorrection;
                LastMultiline = multiline;
                LastSecure = secure;
                LastCharacterLimit = characterLimit;
                Keyboard = new FakeKeyboardHandle(text, characterLimit);
                return Keyboard;
            }
        }

        private sealed class FakeKeyboardHandle : ITextInputTouchKeyboardHandle
        {
            private bool active = true;

            internal FakeKeyboardHandle(string text, int characterLimit)
            {
                Text = text;
                CharacterLimit = characterLimit;
            }

            public TouchScreenKeyboard Keyboard => null!;
            public string Text { get; set; }

            public bool Active
            {
                get => active;
                set
                {
                    if (active && !value)
                    {
                        ++DeactivateCount;
                    }
                    active = value;
                }
            }

            public TouchScreenKeyboard.Status Status { get; set; } = TouchScreenKeyboard.Status.Visible;
            public int CharacterLimit { get; set; }
            public bool CanGetSelection { get; internal set; } = true;
            public bool CanSetSelection { get; set; } = true;
            public RangeInt Selection { get; set; }
            internal int DeactivateCount { get; private set; }
        }
    }
}
