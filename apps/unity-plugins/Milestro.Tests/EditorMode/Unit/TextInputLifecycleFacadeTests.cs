using System.Linq;
using System.Reflection;
using Milestro.Components;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace Milestro.Tests.TextInputLifecycle.Unit
{
    public class TextInputLifecycleFacadeTests
    {
        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [Test]
        public void LifecycleFacadeUsesSerializedGetterOnlyUnityEvents()
        {
            var owner = new GameObject("text-input");
            owner.SetActive(false);
            try
            {
                var input = owner.AddComponent<TextInput>();

                AssertEventShape<string>(input, "m_OnValueChanged", "onValueChanged");
                AssertEventShape<string>(input, "m_OnEndEdit", "onEndEdit");
                AssertEventShape(input, "m_OnFocusGained", "onFocusGained");
                AssertEventShape(input, "m_OnFocusLost", "onFocusLost");

                Assert.That(typeof(TextInput).GetEvents(BindingFlags.Instance |
                                                        BindingFlags.Public |
                                                        BindingFlags.DeclaredOnly),
                    Is.Empty,
                    "TextInput must expose only UnityEvent properties, not a second public C# event channel.");
                Assert.That(typeof(TextInput).GetEvent("InternalValueChanged", DeclaredInstance), Is.Null);
                Assert.That(typeof(TextInput).GetEvent("InternalEndEdit", DeclaredInstance), Is.Null);
                Assert.That(typeof(TextInput).GetEvent("InternalFocusGained", DeclaredInstance), Is.Null);
                Assert.That(typeof(TextInput).GetEvent("InternalFocusLost", DeclaredInstance), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DisabledProgrammaticTextNotifiesOnlyForDifferentNonSilentValue()
        {
            var owner = new GameObject("text-input");
            owner.SetActive(false);
            try
            {
                var input = owner.AddComponent<TextInput>();
                input.enabled = false;
                owner.SetActive(true);
                var payloads = new System.Collections.Generic.List<string>();
                input.onValueChanged.AddListener(payloads.Add);

                input.Text = "disabled";
                input.Text = "disabled";
                input.SetTextWithoutNotify("silent");

                Assert.That(payloads, Is.EqualTo(new[] { "disabled" }));
                Assert.That(input.Text, Is.EqualTo("silent"));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void OnValidateCanonicalizesWithoutLifecycleNotification()
        {
            var owner = new GameObject("text-input");
            owner.SetActive(false);
            try
            {
                var input = owner.AddComponent<TextInput>();
                input.lineMode = TextInputLineMode.MultiLine;
                var calls = 0;
                input.onValueChanged.AddListener(_ => ++calls);
                input.onEndEdit.AddListener(_ => ++calls);
                input.onFocusGained.AddListener(() => ++calls);
                input.onFocusLost.AddListener(() => ++calls);

                var textField = typeof(TextInput).GetField("m_text", DeclaredInstance);
                Assert.That(textField, Is.Not.Null);
                textField!.SetValue(input, new string(new[] { 'a', '\r', '\n', 'b', '\ud800' }));

                var onValidate = typeof(TextInput).GetMethod("OnValidate", DeclaredInstance);
                Assert.That(onValidate, Is.Not.Null);
                onValidate!.Invoke(input, null);

                Assert.That(input.Text, Is.EqualTo("a\nb"));
                Assert.That(calls, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void AssertEventShape<T>(TextInput input, string fieldName, string propertyName)
        {
            AssertEventShape(input, fieldName, propertyName, typeof(UnityEvent<T>));
        }

        private static void AssertEventShape(TextInput input, string fieldName, string propertyName)
        {
            AssertEventShape(input, fieldName, propertyName, typeof(UnityEvent));
        }

        private static void AssertEventShape(TextInput input,
            string fieldName,
            string propertyName,
            System.Type eventType)
        {
            var field = typeof(TextInput).GetField(fieldName, DeclaredInstance);
            Assert.That(field, Is.Not.Null);
            Assert.That(field!.IsPrivate, Is.True);
            Assert.That(field.FieldType, Is.EqualTo(eventType));
            Assert.That(field.GetCustomAttributes(typeof(SerializeField), inherit: false).Any(), Is.True);

            var property = typeof(TextInput).GetProperty(propertyName, DeclaredInstance);
            Assert.That(property, Is.Not.Null);
            Assert.That(property!.PropertyType, Is.EqualTo(eventType));
            Assert.That(property.GetMethod, Is.Not.Null);
            Assert.That(property.GetMethod!.IsPublic, Is.True);
            Assert.That(property.SetMethod, Is.Null);
            Assert.That(property.GetValue(input), Is.Not.Null);
            Assert.That(property.GetValue(input), Is.SameAs(property.GetValue(input)),
                "The getter must be stable for one component lifetime.");
        }
    }
}
