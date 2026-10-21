using System.Linq;
using Milestro.Components;
using Milestro.Model;
using UnityEditor;

namespace Milestro.Editor
{
    [CustomEditor(typeof(TextBox), true)]
    [CanEditMultipleObjects]
    public sealed class TextBoxEditor : UnityEditor.Editor
    {
        private static readonly string[] ColorGradientProperties =
        {
            "m_enableColorGradient",
            "m_colorGradient",
            "m_colorGradientPreset"
        };

        private static readonly string[] ScrollProperties =
        {
            "m_scrollWheelStepPixels",
            "m_smoothScroll",
            "m_scrollTweenDurationSeconds",
            "m_scrollElastic"
        };

        private static readonly string[] LinkProperties =
        {
            "m_linkHoverColor",
            "m_linkHoverUnderlineColor",
            "m_linkHoverUnderlineThickness",
            "m_linkHoverCursorEnabled",
            "m_linkHoverCursor",
            "m_linkHoverCursorHotspot",
            "m_linkHoverCursorMode",
            "m_OnLinkClicked",
            "m_OnLinkHoverChanged"
        };

        private static readonly string[] FoldoutProperties =
            ColorGradientProperties.Concat(ScrollProperties).Concat(LinkProperties).ToArray();

        private bool scrollExpanded;
        private bool linkExpanded;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, FoldoutProperties);
            DrawColorGradient();
            DrawFoldout(ref scrollExpanded, "Scroll", ScrollProperties);
            DrawFoldout(ref linkExpanded, "Link", LinkProperties);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFoldout(ref bool expanded, string label, string[] propertyNames)
        {
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, label);
            if (expanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var propertyName in propertyNames)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), includeChildren: true);
                    }
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawColorGradient()
        {
            var gradient = serializedObject.FindProperty("m_colorGradient");
            gradient.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(gradient.isExpanded, "Color Gradient");
            if (gradient.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_enableColorGradient"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_colorGradientPreset"));

                    var mode = gradient.FindPropertyRelative("m_mode");
                    EditorGUILayout.PropertyField(mode);
                    var showAllModes = mode.hasMultipleDifferentValues;
                    var gradientMode = (TextGradientMode)mode.intValue;

                    if (showAllModes || gradientMode == TextGradientMode.FourCorners)
                    {
                        EditorGUILayout.PropertyField(gradient.FindPropertyRelative("m_fourCorners"), includeChildren: true);
                    }

                    if (showAllModes || gradientMode == TextGradientMode.Linear || gradientMode == TextGradientMode.Radial)
                    {
                        EditorGUILayout.PropertyField(gradient.FindPropertyRelative("m_stops"));
                    }

                    if (showAllModes || gradientMode == TextGradientMode.Linear)
                    {
                        EditorGUILayout.PropertyField(gradient.FindPropertyRelative("m_angleDegrees"));
                    }

                    if (showAllModes || gradientMode == TextGradientMode.Radial)
                    {
                        EditorGUILayout.PropertyField(gradient.FindPropertyRelative("m_radialCenter"));
                    }

                    if (showAllModes || gradientMode == TextGradientMode.Linear || gradientMode == TextGradientMode.Radial)
                    {
                        EditorGUILayout.PropertyField(gradient.FindPropertyRelative("m_meshResolution"));
                    }
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
