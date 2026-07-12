using System;
using UnityEngine;

namespace Milestro.Input
{
    internal interface IHybridInputImeSessionCanceller
    {
        bool IsRequired { get; }
        HybridInputImeCancellationResult CancelComposition();
    }

    internal sealed class HybridInputNativeImeSessionCanceller : IHybridInputImeSessionCanceller
    {
        internal static readonly HybridInputNativeImeSessionCanceller Shared =
            new HybridInputNativeImeSessionCanceller();

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || (UNITY_IOS && !UNITY_EDITOR)
        public bool IsRequired => true;
#else
        public bool IsRequired => false;
#endif

        public HybridInputImeCancellationResult CancelComposition()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || (UNITY_IOS && !UNITY_EDITOR)
            try
            {
                var exitCode = Binding.BindingC.ImeCancelComposition(out var result);
                return exitCode == 0 && Enum.IsDefined(typeof(HybridInputImeCancellationResult), result)
                    ? (HybridInputImeCancellationResult)result
                    : HybridInputImeCancellationResult.Failed;
            }
            catch (DllNotFoundException)
            {
                return HybridInputImeCancellationResult.NativeUnavailable;
            }
            catch (EntryPointNotFoundException)
            {
                return HybridInputImeCancellationResult.NativeUnavailable;
            }
            catch (BadImageFormatException)
            {
                return HybridInputImeCancellationResult.NativeUnavailable;
            }
#else
            return HybridInputImeCancellationResult.Unsupported;
#endif
        }
    }

    internal sealed class HybridInputNoopImeSessionCanceller : IHybridInputImeSessionCanceller
    {
        internal static readonly HybridInputNoopImeSessionCanceller Shared =
            new HybridInputNoopImeSessionCanceller();

        public bool IsRequired => false;
        public HybridInputImeCancellationResult CancelComposition() =>
            HybridInputImeCancellationResult.Unsupported;
    }

    internal static class HybridInputImeCancellationDiagnostics
    {
        private static bool warningReported;

        internal static HybridInputImeCancellationResult LastResult { get; private set; }
        internal static int FailureCount { get; private set; }

        internal static void Record(HybridInputImeCancellationResult result)
        {
            LastResult = result;
            if (result == HybridInputImeCancellationResult.Succeeded)
            {
                return;
            }

            ++FailureCount;
            if (warningReported)
            {
                return;
            }

            warningReported = true;
            Debug.LogWarning($"Milestro could not cancel the active platform IME composition ({result}); " +
                             "IME focus handoff will remain blocked until an empty composition acknowledgement.");
        }
    }
}
