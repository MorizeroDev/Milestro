using System;
using UnityEngine;

namespace Milestro.Binding
{
    public static class ExitCodeUtil
    {
        private const int SeverityShift = 62;
        private const int FlagsShift = 59;
        private const int MagicShift = 51;
        private const ulong SeverityMask = 0x3UL;
        private const ulong FlagsMask = 0x7UL;
        private const ulong MagicMask = 0xFFUL;
        private const ulong Magic = 0x5AUL;
        private const ulong ConsumedFlag = 0x4UL;
        private const ulong SeveritySuccess = 0;
        private const ulong SeverityWarning = 1;

        internal static void ThrowIfFailed(long status)
        {
            var raw = unchecked((ulong)status);
            if (((raw >> MagicShift) & MagicMask) == Magic)
            {
                var severity = (raw >> SeverityShift) & SeverityMask;
                var flags = (raw >> FlagsShift) & FlagsMask;
                if (severity == SeveritySuccess)
                {
                    return;
                }

                if (severity == SeverityWarning)
                {
                    Debug.LogWarning($"Milestro native call returned warning status {status}.");
                    return;
                }

                if ((flags & ConsumedFlag) != 0)
                {
                    Debug.LogError($"Milestro native call failed after consuming its resource (status {status}).");
                    return;
                }
            }

            if (status < 0)
            {
                throw new Exception($"Milestro native call failed with status {status}.");
            }
        }
    }
}
