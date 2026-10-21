using Milestro.Components;
using UnityEditor;

namespace Milestro.Editor
{
    [CustomEditor(typeof(TextBox), true)]
    [CanEditMultipleObjects]
    public sealed class TextBoxEditor : UnityEditor.Editor
    {
        private static readonly string[] LinkHoverProperties =
        {
            "m_linkHoverColor",
            "m_linkHoverUnderlineColor",
            "m_linkHoverUnderlineThickness",
            "m_linkHoverCursorEnabled",
            "m_linkHoverCursor",
            "m_linkHoverCursorHotspot",
            "m_linkHoverCursorMode",
            "m_OnLinkHoverChanged"
        };

        private bool linkHoverExpanded;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, LinkHoverProperties);
            linkHoverExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(linkHoverExpanded, "Link Hover");
            if (linkHoverExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var propertyName in LinkHoverProperties)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), includeChildren: true);
                    }
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
