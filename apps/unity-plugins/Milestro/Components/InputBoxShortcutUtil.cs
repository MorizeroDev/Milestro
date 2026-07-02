using UnityEngine;

namespace Milestro.Components
{
    internal static class InputBoxShortcutUtil
    {
        internal static bool IsSelectionModifierPressed()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        internal static bool IsCommandOrControlPressed()
        {
            return Input.GetKey(KeyCode.LeftControl) ||
                   Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftCommand) ||
                   Input.GetKey(KeyCode.RightCommand);
        }

        internal static bool IsControlPressed()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        internal static bool IsUndoShortcutDown()
        {
            return IsCommandOrControlPressed() &&
                   !IsSelectionModifierPressed() &&
                   Input.GetKeyDown(KeyCode.Z);
        }

        internal static bool IsRedoShortcutDown()
        {
            return (IsCommandOrControlPressed() &&
                    IsSelectionModifierPressed() &&
                    Input.GetKeyDown(KeyCode.Z)) ||
                   (IsControlPressed() && Input.GetKeyDown(KeyCode.Y));
        }

        internal static bool IsSelectAllShortcutDown()
        {
            return IsCommandOrControlPressed() && Input.GetKeyDown(KeyCode.A);
        }

        internal static bool IsCopyShortcutDown()
        {
            return (IsCommandOrControlPressed() && Input.GetKeyDown(KeyCode.C)) ||
                   (IsControlPressed() && Input.GetKeyDown(KeyCode.Insert));
        }

        internal static bool IsCutShortcutDown()
        {
            return (IsCommandOrControlPressed() && Input.GetKeyDown(KeyCode.X)) ||
                   (IsSelectionModifierPressed() && Input.GetKeyDown(KeyCode.Delete));
        }

        internal static bool IsPasteShortcutDown()
        {
            return (IsCommandOrControlPressed() && Input.GetKeyDown(KeyCode.V)) ||
                   (IsSelectionModifierPressed() && Input.GetKeyDown(KeyCode.Insert));
        }

        internal static bool IsCommittedTextShortcutSuppressed()
        {
            if (IsCommandOrControlPressed() &&
                (Input.GetKeyDown(KeyCode.A) ||
                 Input.GetKeyDown(KeyCode.C) ||
                 Input.GetKeyDown(KeyCode.V) ||
                 Input.GetKeyDown(KeyCode.X) ||
                 Input.GetKeyDown(KeyCode.Z)))
            {
                return true;
            }

            if (IsControlPressed() && Input.GetKeyDown(KeyCode.Y))
            {
                return true;
            }

            return (IsSelectionModifierPressed() || IsControlPressed()) &&
                   (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Insert));
        }
    }
}
