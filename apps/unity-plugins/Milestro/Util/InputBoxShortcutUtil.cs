using Milestro.Configuration;
using Milestro.Input;
using UnityEngine;

namespace Milestro.Util
{
    internal static class InputBoxShortcutUtil
    {
        internal static bool IsSelectionExtendDown(HybridInputFrame frame)
        {
            return frame.IsKeyPressed(KeyCode.LeftShift) || frame.IsKeyPressed(KeyCode.RightShift);
        }

        private static bool IsSelectionModifierPressed(HybridInputFrame frame)
        {
            return IsSelectionExtendDown(frame);
        }

        private static bool IsControlPressed(HybridInputFrame frame)
        {
            return frame.IsKeyPressed(KeyCode.LeftControl) || frame.IsKeyPressed(KeyCode.RightControl);
        }

        private static bool IsCommandPressed(HybridInputFrame frame)
        {
            return frame.IsKeyPressed(KeyCode.LeftCommand) || frame.IsKeyPressed(KeyCode.RightCommand);
        }

        private static bool IsAltPressed(HybridInputFrame frame)
        {
            return frame.IsKeyPressed(KeyCode.LeftAlt) || frame.IsKeyPressed(KeyCode.RightAlt);
        }

        private static bool IsExactCommandOrControlModifier(HybridInputFrame frame, bool shift)
        {
            return IsControlPressed(frame) != IsCommandPressed(frame) &&
                   IsSelectionModifierPressed(frame) == shift &&
                   !IsAltPressed(frame);
        }

        private static bool IsExactControlModifier(HybridInputFrame frame, bool shift)
        {
            return IsControlPressed(frame) &&
                   !IsCommandPressed(frame) &&
                   IsSelectionModifierPressed(frame) == shift &&
                   !IsAltPressed(frame);
        }

        private static bool IsExactShiftOnlyModifier(HybridInputFrame frame)
        {
            return IsSelectionModifierPressed(frame) &&
                   !IsControlPressed(frame) &&
                   !IsCommandPressed(frame) &&
                   !IsAltPressed(frame);
        }

        private static bool IsClipboardCommandModifier(HybridInputFrame frame)
        {
            return IsExactCommandOrControlModifier(frame, false) ||
                   (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptShiftClipboardShortcuts &&
                    IsExactCommandOrControlModifier(frame, true));
        }

        internal static bool IsUndoDown(HybridInputFrame frame)
        {
            return IsExactCommandOrControlModifier(frame, false) && frame.WasKeyPressed(KeyCode.Z);
        }

        internal static bool IsRedoDown(HybridInputFrame frame)
        {
            return (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptRedoWithShiftZ &&
                    IsExactCommandOrControlModifier(frame, true) && frame.WasKeyPressed(KeyCode.Z)) ||
                   (MilestroConfiguration.Configuration.InputBoxShortcut.AcceptRedoWithControlY &&
                    IsExactControlModifier(frame, false) && frame.WasKeyPressed(KeyCode.Y));
        }

        internal static bool IsSelectAllDown(HybridInputFrame frame)
        {
            return IsExactCommandOrControlModifier(frame, false) && frame.WasKeyPressed(KeyCode.A);
        }

        internal static bool IsDocumentBoundaryModifierDown(HybridInputFrame frame)
        {
            return IsControlPressed(frame) != IsCommandPressed(frame) && !IsAltPressed(frame);
        }

        internal static bool IsMacBoundaryArrowModifierDown(HybridInputFrame frame)
        {
            return IsCommandPressed(frame) && !IsControlPressed(frame) && !IsAltPressed(frame);
        }

        internal static bool IsCopyDown(HybridInputFrame frame)
        {
            return (IsClipboardCommandModifier(frame) && frame.WasKeyPressed(KeyCode.C)) ||
                   (IsExactControlModifier(frame, false) && frame.WasKeyPressed(KeyCode.Insert));
        }

        internal static bool IsCutDown(HybridInputFrame frame)
        {
            return (IsClipboardCommandModifier(frame) && frame.WasKeyPressed(KeyCode.X)) ||
                   (IsExactShiftOnlyModifier(frame) && frame.WasKeyPressed(KeyCode.Delete));
        }

        internal static bool IsPasteDown(HybridInputFrame frame)
        {
            return (IsClipboardCommandModifier(frame) && frame.WasKeyPressed(KeyCode.V)) ||
                   (IsExactShiftOnlyModifier(frame) && frame.WasKeyPressed(KeyCode.Insert));
        }

        internal static bool IsCommittedTextInputSuppressed(HybridInputFrame frame)
        {
            return IsCommittedTextShortcutSuppressed(frame) || IsEditingKeyPressed(frame);
        }

        private static bool IsCommittedTextShortcutSuppressed(HybridInputFrame frame)
        {
            return IsUndoDown(frame) ||
                   IsRedoDown(frame) ||
                   IsSelectAllDown(frame) ||
                   IsCopyDown(frame) ||
                   IsCutDown(frame) ||
                   IsPasteDown(frame);
        }

        private static bool IsEditingKeyPressed(HybridInputFrame frame)
        {
            return frame.IsKeyPressed(KeyCode.LeftArrow) ||
                   frame.IsKeyPressed(KeyCode.RightArrow) ||
                   frame.IsKeyPressed(KeyCode.UpArrow) ||
                   frame.IsKeyPressed(KeyCode.DownArrow) ||
                   frame.IsKeyPressed(KeyCode.Backspace) ||
                   frame.IsKeyPressed(KeyCode.Delete) ||
                   frame.IsKeyPressed(KeyCode.Home) ||
                   frame.IsKeyPressed(KeyCode.End) ||
                   frame.IsKeyPressed(KeyCode.PageUp) ||
                   frame.IsKeyPressed(KeyCode.PageDown);
        }
    }
}
