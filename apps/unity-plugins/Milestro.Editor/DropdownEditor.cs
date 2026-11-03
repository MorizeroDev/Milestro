using Milestro.Components.Internal;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Editor
{
    public static class DropdownEditor
    {
        [MenuItem("GameObject/UI/Milestro Dropdown", false, 10)]
        private static void CreateDropdown()
        {
            var parent = Selection.activeGameObject != null ? Selection.activeGameObject.transform : null;
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Milestro Dropdown Canvas");
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                parent = canvasObject.transform;
            }

            var dropdown = MilestroDropdownDefaults.Create(parent);
            Undo.RegisterCreatedObjectUndo(dropdown.gameObject, "Create Milestro Dropdown");
            Selection.activeGameObject = dropdown.gameObject;
        }
    }
}
