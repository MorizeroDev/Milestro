using System;
using Milestro.RichTextParser;
using UnityEditor;
using UnityEngine;

namespace Milestro.Editor
{
    [CustomPropertyDrawer(typeof(RichTextParserIdAttribute))]
    public sealed class RichTextParserIdDrawer : PropertyDrawer
    {
        private const float DropdownButtonWidth = 20f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var contentRect = EditorGUI.PrefixLabel(position, label);
            var textRect = new Rect(contentRect.x, contentRect.y,
                contentRect.width - DropdownButtonWidth - 2f, contentRect.height);
            var buttonRect = new Rect(textRect.xMax + 2f, contentRect.y,
                DropdownButtonWidth, contentRect.height);
            var current = property.stringValue;
            EditorGUI.BeginChangeCheck();
            var edited = EditorGUI.TextField(textRect, current);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = edited;
            }

            if (EditorGUI.DropdownButton(buttonRect, GUIContent.none, FocusType.Passive))
            {
                ShowMenu(buttonRect, property, current);
            }

            EditorGUI.EndProperty();
        }

        private static void ShowMenu(Rect buttonRect, SerializedProperty property, string current)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("None (Plain Text)"), string.IsNullOrEmpty(current),
                () => Assign(property, ""));
            foreach (var parserProvider in RichTextParserService.Parsers)
            {
                var captured = parserProvider;
                menu.AddItem(new GUIContent(captured.DisplayName),
                    string.Equals(current, captured.Id, StringComparison.Ordinal),
                    () => Assign(property, captured.Id));
            }

            menu.DropDown(buttonRect);
        }

        private static void Assign(SerializedProperty property, string value)
        {
            property.stringValue = value;
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
