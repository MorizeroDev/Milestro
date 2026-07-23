using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Input
{
    internal static class HybridInputSystemCompatibility
    {
        internal const string MinimumVersion = "1.16.0";
        internal const string MaximumVersionExclusive = "2.0.0-0";
        internal const string InputSystemUiModuleTypeName =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule";

        private static HybridInputSystemPackageStatus packageStatus = DefaultPackageStatus;
        private static bool runtimeIssueReported;

        internal static HybridInputSystemPackageStatus PackageStatus => packageStatus;

        private static HybridInputSystemPackageStatus DefaultPackageStatus
        {
            get
            {
#if MILESTRO_INPUT_SYSTEM_SUPPORTED
                return HybridInputSystemPackageStatus.Supported;
#elif ENABLE_INPUT_SYSTEM
                return HybridInputSystemPackageStatus.Unsupported;
#else
                return HybridInputSystemPackageStatus.NotApplicable;
#endif
            }
        }

        internal static bool IsExactInputSystemUiModule(BaseInputModule? module)
        {
            return module != null &&
                   module.GetType().FullName == InputSystemUiModuleTypeName;
        }

        internal static void SetPackageStatus(HybridInputSystemPackageStatus status)
        {
            packageStatus = status;
        }

        internal static void ResetRuntimeReport()
        {
            runtimeIssueReported = false;
        }

        internal static void ReportRuntimeIssueIfNeeded(HybridInputEnvironment environment)
        {
#if !UNITY_EDITOR && ENABLE_INPUT_SYSTEM && !MILESTRO_INPUT_SYSTEM_SUPPORTED
            if (runtimeIssueReported)
            {
                return;
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!IsExactInputSystemUiModule(environment.ActiveModule) || environment.EventSystemCount != 1)
            {
                return;
            }

            runtimeIssueReported = true;
            Debug.LogWarning(
                "Milestro Input System integration is unavailable. Using the legacy delta-only " +
                "scroll fallback for InputSystemUIInputModule. Supported com.unity.inputsystem " +
                "versions are [1.16.0,2.0.0-0). Upgrade the package, change Active Input Handling " +
                "to Both, or change it to Input Manager (Old).");
#else
            runtimeIssueReported = true;
            Debug.LogError(
                "Milestro Input System integration is unavailable. Supported com.unity.inputsystem " +
                "versions are [1.16.0,2.0.0-0). Upgrade the package, change Active Input Handling " +
                "to Both, or change it to Input Manager (Old).");
#endif
#endif
        }
    }
}
