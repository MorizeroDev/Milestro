using Milestro.Components.Internal;
using UnityEditor;

namespace Milestro.Editor
{
    [CustomEditor(typeof(TextBoxRenderTextureProducer), true)]
    [CanEditMultipleObjects]
    public sealed class TextBoxRenderTextureProducerEditor : UnityEditor.Editor
    {
        private static readonly string[] ParagraphProperties =
        {
            "m_paragraphStyle",
            "m_wrapMode",
            "m_textOverflow"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, ParagraphProperties);

            var paragraphStyle = serializedObject.FindProperty("m_paragraphStyle");
            paragraphStyle.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(paragraphStyle.isExpanded, "Paragraph Style");
            if (paragraphStyle.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var property = paragraphStyle.Copy();
                    var endProperty = paragraphStyle.GetEndProperty();
                    var enterChildren = true;
                    while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, endProperty))
                    {
                        enterChildren = false;
                        EditorGUILayout.PropertyField(property, includeChildren: true);
                    }

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_wrapMode"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_textOverflow"));
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
