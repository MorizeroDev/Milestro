using System;
using System.Globalization;
using Milestro.Input;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Milestro.Editor
{
    internal enum InputHandlingMode
    {
        LegacyOnly = 0,
        InputSystemOnly = 1,
        Both = 2
    }

    internal enum InputSystemValidationSeverity
    {
        None = 0,
        Warning = 1,
        Error = 2
    }

    internal readonly struct InputSystemPackageSnapshot
    {
        private InputSystemPackageSnapshot(bool isPresent,
            string version,
            string queryError)
        {
            IsPresent = isPresent;
            Version = version ?? string.Empty;
            QueryError = queryError ?? string.Empty;
        }

        internal bool IsPresent { get; }
        internal string Version { get; }
        internal string QueryError { get; }

        internal static InputSystemPackageSnapshot Missing()
        {
            return new InputSystemPackageSnapshot(false, string.Empty, string.Empty);
        }

        internal static InputSystemPackageSnapshot Present(string version)
        {
            return new InputSystemPackageSnapshot(true, version, string.Empty);
        }

        internal static InputSystemPackageSnapshot QueryFailed(string error)
        {
            return new InputSystemPackageSnapshot(false, string.Empty, error);
        }
    }

    internal readonly struct InputSystemValidationDecision
    {
        internal InputSystemValidationDecision(HybridInputSystemPackageStatus packageStatus,
            InputSystemValidationSeverity severity,
            bool blocksBuild,
            string message)
        {
            PackageStatus = packageStatus;
            Severity = severity;
            BlocksBuild = blocksBuild;
            Message = message ?? string.Empty;
        }

        internal HybridInputSystemPackageStatus PackageStatus { get; }
        internal InputSystemValidationSeverity Severity { get; }
        internal bool BlocksBuild { get; }
        internal string Message { get; }
    }

    internal static class InputSystemCompatibilityPolicy
    {
        internal static HybridInputSystemPackageStatus Classify(
            InputSystemPackageSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot.QueryError))
            {
                return HybridInputSystemPackageStatus.Unsupported;
            }
            if (!snapshot.IsPresent)
            {
                return HybridInputSystemPackageStatus.Missing;
            }
            if (!TryParseSemanticVersion(snapshot.Version,
                    out var major,
                    out var minor,
                    out var patch,
                    out var isPrerelease))
            {
                return HybridInputSystemPackageStatus.Unsupported;
            }
            if (major < 1 || major == 1 &&
                (minor < 16 || minor == 16 && patch == 0 && isPrerelease))
            {
                return HybridInputSystemPackageStatus.BelowMinimum;
            }
            return major == 1
                ? HybridInputSystemPackageStatus.Supported
                : HybridInputSystemPackageStatus.Unsupported;
        }

        internal static InputSystemValidationDecision Decide(
            InputSystemPackageSnapshot snapshot,
            InputHandlingMode mode)
        {
            var packageStatus = Classify(snapshot);
            if (mode == InputHandlingMode.LegacyOnly ||
                packageStatus == HybridInputSystemPackageStatus.Supported)
            {
                return new InputSystemValidationDecision(packageStatus,
                    InputSystemValidationSeverity.None,
                    false,
                    string.Empty);
            }

            var currentVersion = CurrentVersionDescription(snapshot);
            var fixes = "Upgrade com.unity.inputsystem to a compatible version in [1.16.0,2.0.0-0), " +
                        "change Active Input Handling to Both, or change Active Input Handling " +
                        "to Input Manager (Old).";
            if (mode == InputHandlingMode.Both)
            {
                return new InputSystemValidationDecision(packageStatus,
                    InputSystemValidationSeverity.Warning,
                    false,
                    "Milestro Input System provider is unavailable (current: " + currentVersion +
                    "; minimum: 1.16.0). InputSystemUIInputModule will use the legacy " +
                    "delta-only scroll fallback; strict TextInput focus remains unavailable. " + fixes);
            }

            return new InputSystemValidationDecision(packageStatus,
                InputSystemValidationSeverity.Error,
                true,
                "Milestro cannot build with Input System Package (New) because the Input System " +
                "provider is unavailable (current: " + currentVersion +
                "; minimum: 1.16.0). " + fixes);
        }

        private static bool TryParseSemanticVersion(string value,
            out int major,
            out int minor,
            out int patch,
            out bool isPrerelease)
        {
            major = 0;
            minor = 0;
            patch = 0;
            isPrerelease = false;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var metadataIndex = value.IndexOf('+');
            if (metadataIndex == 0 || metadataIndex == value.Length - 1 ||
                metadataIndex >= 0 && value.IndexOf('+', metadataIndex + 1) >= 0)
            {
                return false;
            }
            if (metadataIndex >= 0 &&
                !ValidateIdentifiers(value.Substring(metadataIndex + 1), false))
            {
                return false;
            }

            var coreAndPrerelease = metadataIndex < 0
                ? value
                : value.Substring(0, metadataIndex);
            var prereleaseIndex = coreAndPrerelease.IndexOf('-');
            var core = prereleaseIndex < 0
                ? coreAndPrerelease
                : coreAndPrerelease.Substring(0, prereleaseIndex);
            isPrerelease = prereleaseIndex >= 0;
            if (isPrerelease &&
                !ValidateIdentifiers(coreAndPrerelease.Substring(prereleaseIndex + 1), true))
            {
                return false;
            }

            var parts = core.Split('.');
            return parts.Length == 3 &&
                   TryParseVersionPart(parts[0], out major) &&
                   TryParseVersionPart(parts[1], out minor) &&
                   TryParseVersionPart(parts[2], out patch);
        }

        private static bool TryParseVersionPart(string value, out int result)
        {
            result = 0;
            return !string.IsNullOrEmpty(value) &&
                   (value.Length == 1 || value[0] != '0') &&
                   int.TryParse(value,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out result);
        }

        private static bool ValidateIdentifiers(string value,
            bool rejectLeadingZeroNumericIdentifiers)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var identifiers = value.Split('.');
            for (var i = 0; i < identifiers.Length; ++i)
            {
                var identifier = identifiers[i];
                if (string.IsNullOrEmpty(identifier))
                {
                    return false;
                }

                var isNumeric = true;
                for (var j = 0; j < identifier.Length; ++j)
                {
                    var character = identifier[j];
                    var isDigit = character >= '0' && character <= '9';
                    var isLetter = character >= 'A' && character <= 'Z' ||
                                   character >= 'a' && character <= 'z';
                    if (!isDigit && !isLetter && character != '-')
                    {
                        return false;
                    }
                    isNumeric &= isDigit;
                }

                if (rejectLeadingZeroNumericIdentifiers && isNumeric &&
                    identifier.Length > 1 && identifier[0] == '0')
                {
                    return false;
                }
            }

            return true;
        }

        private static string CurrentVersionDescription(InputSystemPackageSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot.QueryError))
            {
                return "unavailable (" + snapshot.QueryError + ")";
            }
            return snapshot.IsPresent
                ? string.IsNullOrEmpty(snapshot.Version) ? "unparseable" : snapshot.Version
                : "missing";
        }
    }

    [InitializeOnLoad]
    public sealed class InputSystemCompatibilityValidator : IPreprocessBuildWithReport
    {
        private const string PackageName = "com.unity.inputsystem";
        private const string SessionMessageKey = "Milestro.InputSystemCompatibility.Message";

        static InputSystemCompatibilityValidator()
        {
            EditorApplication.delayCall += ValidateEditor;
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var decision = EvaluateCurrent();
            if (decision.BlocksBuild)
            {
                throw new BuildFailedException(decision.Message);
            }
            LogOnce(decision);
        }

        private static void ValidateEditor()
        {
            LogOnce(EvaluateCurrent());
        }

        private static InputSystemValidationDecision EvaluateCurrent()
        {
            var decision = InputSystemCompatibilityPolicy.Decide(QueryPackage(), CurrentMode);
            HybridInputSystemCompatibility.SetPackageStatus(decision.PackageStatus);
            return decision;
        }

        private static InputSystemPackageSnapshot QueryPackage()
        {
            try
            {
                var packages = PackageInfo.GetAllRegisteredPackages();
                if (packages == null)
                {
                    return InputSystemPackageSnapshot.QueryFailed("package query returned null");
                }
                for (var i = 0; i < packages.Length; ++i)
                {
                    var package = packages[i];
                    if (package != null && package.name == PackageName)
                    {
                        return InputSystemPackageSnapshot.Present(package.version);
                    }
                }
                return InputSystemPackageSnapshot.Missing();
            }
            catch (Exception exception)
            {
                return InputSystemPackageSnapshot.QueryFailed(exception.GetType().Name);
            }
        }

        private static InputHandlingMode CurrentMode
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
                return InputHandlingMode.Both;
#elif ENABLE_INPUT_SYSTEM
                return InputHandlingMode.InputSystemOnly;
#else
                return InputHandlingMode.LegacyOnly;
#endif
            }
        }

        private static void LogOnce(InputSystemValidationDecision decision)
        {
            if (decision.Severity == InputSystemValidationSeverity.None)
            {
                SessionState.EraseString(SessionMessageKey);
                return;
            }
            if (SessionState.GetString(SessionMessageKey, string.Empty) == decision.Message)
            {
                return;
            }

            SessionState.SetString(SessionMessageKey, decision.Message);
            if (decision.Severity == InputSystemValidationSeverity.Error)
            {
                Debug.LogError(decision.Message);
            }
            else
            {
                Debug.LogWarning(decision.Message);
            }
        }
    }
}
