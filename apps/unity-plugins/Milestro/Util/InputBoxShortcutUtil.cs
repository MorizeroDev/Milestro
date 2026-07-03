using Milestro.Configuration;
using UnityEngine;

namespace Milestro.Util
{
    internal static class InputBoxShortcutUtil
    {
        internal static bool IsSelectionExtendDown()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool IsSelectionModifierPressed()
        {
            return IsSelectionExtendDown();
        }

        private static bool IsControlPressed()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static bool IsCommandPressed()
        {
            return Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        }

        private static bool IsAltPressed()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        private static bool IsExactCommandOrControlModifier(bool shift)
        {
            return IsControlPressed() != IsCommandPressed() &&
                   IsSelectionModifierPressed() == shift &&
                   !IsAltPressed();
        }

        private static bool IsExactControlModifier(bool shift)
        {
            return IsControlPressed() &&
                   !IsCommandPressed() &&
                   IsSelectionModifierPressed() == shift &&
                   !IsAltPressed();
        }

        private static bool IsExactShiftOnlyModifier()
        {
            return IsSelectionModifierPressed() &&
                   !IsControlPressed() &&
                   !IsCommandPressed() &&
                   !IsAltPressed();
        }

        private static bool IsClipboardCommandModifier()
        {
            return IsExactCommandOrControlModifier(false) ||
                   (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptShiftClipboardShortcuts &&
                    IsExactCommandOrControlModifier(true));
        }

        internal static bool IsUndoDown()
        {
            return IsExactCommandOrControlModifier(false) &&
                   Input.GetKeyDown(KeyCode.Z);
        }

        internal static bool IsRedoDown()
        {
            return (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptRedoWithShiftZ &&
                    IsExactCommandOrControlModifier(true) &&
                    Input.GetKeyDown(KeyCode.Z)) ||
                   (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptRedoWithControlY &&
                    IsExactControlModifier(false) &&
                    Input.GetKeyDown(KeyCode.Y));
        }

        internal static bool IsSelectAllDown()
        {
            return IsExactCommandOrControlModifier(false) && Input.GetKeyDown(KeyCode.A);
        }

        internal static bool IsDocumentBoundaryModifierDown()
        {
            return IsControlPressed() != IsCommandPressed() && !IsAltPressed();
        }

        internal static bool IsCopyDown()
        {
            return (IsClipboardCommandModifier() && Input.GetKeyDown(KeyCode.C)) ||
                   (IsExactControlModifier(false) && Input.GetKeyDown(KeyCode.Insert));
        }

        internal static bool IsCutDown()
        {
            return (IsClipboardCommandModifier() && Input.GetKeyDown(KeyCode.X)) ||
                   (IsExactShiftOnlyModifier() && Input.GetKeyDown(KeyCode.Delete));
        }

        internal static bool IsPasteDown()
        {
            return (IsClipboardCommandModifier() && Input.GetKeyDown(KeyCode.V)) ||
                   (IsExactShiftOnlyModifier() && Input.GetKeyDown(KeyCode.Insert));
        }

        internal static bool IsCommittedTextInputSuppressed()
        {
            return IsCommittedTextShortcutSuppressed() || IsEditingKeyPressed();
        }

        private static bool IsCommittedTextShortcutSuppressed()
        {
            return IsUndoDown() ||
                   IsRedoDown() ||
                   IsSelectAllDown() ||
                   IsCopyDown() ||
                   IsCutDown() ||
                   IsPasteDown();
        }

        private static bool IsEditingKeyPressed()
        {
            return Input.GetKey(KeyCode.LeftArrow) ||
                   Input.GetKey(KeyCode.RightArrow) ||
                   Input.GetKey(KeyCode.UpArrow) ||
                   Input.GetKey(KeyCode.DownArrow) ||
                   Input.GetKey(KeyCode.Backspace) ||
                   Input.GetKey(KeyCode.Delete) ||
                   Input.GetKey(KeyCode.Home) ||
                   Input.GetKey(KeyCode.End) ||
                   Input.GetKey(KeyCode.PageUp) ||
                   Input.GetKey(KeyCode.PageDown);
        }
    }
}
