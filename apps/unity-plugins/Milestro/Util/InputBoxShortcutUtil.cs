using Milestro.Configuration;
using Milestro.InputManagement;
using UnityEngine;

namespace Milestro.Util
{
    internal static class InputBoxShortcutUtil
    {
        internal static bool IsSelectionExtendDown()
        {
            return HybridInput.GetKey(KeyCode.LeftShift) || HybridInput.GetKey(KeyCode.RightShift);
        }

        private static bool IsSelectionModifierPressed()
        {
            return IsSelectionExtendDown();
        }

        private static bool IsControlPressed()
        {
            return HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.RightControl);
        }

        private static bool IsCommandPressed()
        {
            return HybridInput.GetKey(KeyCode.LeftCommand) || HybridInput.GetKey(KeyCode.RightCommand);
        }

        private static bool IsAltPressed()
        {
            return HybridInput.GetKey(KeyCode.LeftAlt) || HybridInput.GetKey(KeyCode.RightAlt);
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
                   HybridInput.GetKeyDown(KeyCode.Z);
        }

        internal static bool IsRedoDown()
        {
            return (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptRedoWithShiftZ &&
                    IsExactCommandOrControlModifier(true) &&
                    HybridInput.GetKeyDown(KeyCode.Z)) ||
                   (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptRedoWithControlY &&
                    IsExactControlModifier(false) &&
                    HybridInput.GetKeyDown(KeyCode.Y));
        }

        internal static bool IsSelectAllDown()
        {
            return IsExactCommandOrControlModifier(false) && HybridInput.GetKeyDown(KeyCode.A);
        }

        internal static bool IsDocumentBoundaryModifierDown()
        {
            return IsControlPressed() != IsCommandPressed() && !IsAltPressed();
        }

        internal static bool IsMacBoundaryArrowModifierDown()
        {
            return IsCommandPressed() && !IsControlPressed() && !IsAltPressed();
        }

        internal static bool IsCopyDown()
        {
            return (IsClipboardCommandModifier() && HybridInput.GetKeyDown(KeyCode.C)) ||
                   (IsExactControlModifier(false) && HybridInput.GetKeyDown(KeyCode.Insert));
        }

        internal static bool IsCutDown()
        {
            return (IsClipboardCommandModifier() && HybridInput.GetKeyDown(KeyCode.X)) ||
                   (IsExactShiftOnlyModifier() && HybridInput.GetKeyDown(KeyCode.Delete));
        }

        internal static bool IsPasteDown()
        {
            return (IsClipboardCommandModifier() && HybridInput.GetKeyDown(KeyCode.V)) ||
                   (IsExactShiftOnlyModifier() && HybridInput.GetKeyDown(KeyCode.Insert));
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
            return HybridInput.GetKey(KeyCode.LeftArrow) ||
                   HybridInput.GetKey(KeyCode.RightArrow) ||
                   HybridInput.GetKey(KeyCode.UpArrow) ||
                   HybridInput.GetKey(KeyCode.DownArrow) ||
                   HybridInput.GetKey(KeyCode.Backspace) ||
                   HybridInput.GetKey(KeyCode.Delete) ||
                   HybridInput.GetKey(KeyCode.Home) ||
                   HybridInput.GetKey(KeyCode.End) ||
                   HybridInput.GetKey(KeyCode.PageUp) ||
                   HybridInput.GetKey(KeyCode.PageDown);
        }
    }
}
