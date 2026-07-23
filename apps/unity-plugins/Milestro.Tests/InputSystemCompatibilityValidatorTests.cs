using Milestro.Editor;
using Milestro.Input;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class InputSystemCompatibilityValidatorTests
    {
        [TestCase(false, null, HybridInputSystemPackageStatus.Missing)]
        [TestCase(true, "1.15.9", HybridInputSystemPackageStatus.BelowMinimum)]
        [TestCase(true, "1.16.0", HybridInputSystemPackageStatus.Supported)]
        [TestCase(true, "1.19.7", HybridInputSystemPackageStatus.Supported)]
        [TestCase(true, "2.0.0", HybridInputSystemPackageStatus.Unsupported)]
        [TestCase(true, "1.16.0-preview.1", HybridInputSystemPackageStatus.Unsupported)]
        [TestCase(true, "garbage", HybridInputSystemPackageStatus.Unsupported)]
        public void ClassifierCoversSupportedRangeAndInvalidStates(bool present,
            string? version,
            HybridInputSystemPackageStatus expected)
        {
            var snapshot = present
                ? InputSystemPackageSnapshot.Present(version ?? string.Empty)
                : InputSystemPackageSnapshot.Missing();

            Assert.That(InputSystemCompatibilityPolicy.Classify(snapshot), Is.EqualTo(expected));
        }

        [TestCase("1.15.99", HybridInputSystemPackageStatus.BelowMinimum)]
        [TestCase("1.16.0+build.1", HybridInputSystemPackageStatus.Supported)]
        [TestCase("1.17.0-pre.1", HybridInputSystemPackageStatus.Unsupported)]
        [TestCase("01.16.0", HybridInputSystemPackageStatus.Unsupported)]
        [TestCase("1.16", HybridInputSystemPackageStatus.Unsupported)]
        [TestCase("", HybridInputSystemPackageStatus.Unsupported)]
        public void ClassifierIsStrictForStableSemanticVersions(string version,
            HybridInputSystemPackageStatus expected)
        {
            Assert.That(InputSystemCompatibilityPolicy.Classify(
                    InputSystemPackageSnapshot.Present(version)),
                Is.EqualTo(expected));
        }

        [Test]
        public void PackageQueryFailureIsUnsupportedRatherThanMissingOrBelow()
        {
            var snapshot = InputSystemPackageSnapshot.QueryFailed("failure");

            Assert.That(InputSystemCompatibilityPolicy.Classify(snapshot),
                Is.EqualTo(HybridInputSystemPackageStatus.Unsupported));
        }

        [TestCase(InputHandlingMode.LegacyOnly, InputSystemValidationSeverity.None, false)]
        [TestCase(InputHandlingMode.Both, InputSystemValidationSeverity.Warning, false)]
        [TestCase(InputHandlingMode.InputSystemOnly, InputSystemValidationSeverity.Error, true)]
        public void UnsupportedPackageDecisionDependsOnlyOnInputHandlingMode(InputHandlingMode mode,
            InputSystemValidationSeverity expectedSeverity,
            bool expectedBuildBlock)
        {
            var decision = InputSystemCompatibilityPolicy.Decide(
                InputSystemPackageSnapshot.Present("1.14.2"),
                mode);

            Assert.That(decision.PackageStatus,
                Is.EqualTo(HybridInputSystemPackageStatus.BelowMinimum));
            Assert.That(decision.Severity, Is.EqualTo(expectedSeverity));
            Assert.That(decision.BlocksBuild, Is.EqualTo(expectedBuildBlock));
        }

        [Test]
        public void SupportedPackageNeverWarnsOrBlocks()
        {
            foreach (InputHandlingMode mode in System.Enum.GetValues(typeof(InputHandlingMode)))
            {
                var decision = InputSystemCompatibilityPolicy.Decide(
                    InputSystemPackageSnapshot.Present("1.16.0"),
                    mode);

                Assert.That(decision.Severity, Is.EqualTo(InputSystemValidationSeverity.None));
                Assert.That(decision.BlocksBuild, Is.False);
                Assert.That(decision.Message, Is.Empty);
            }
        }

        [TestCase(false, null)]
        [TestCase(true, "1.14.2")]
        [TestCase(true, "2.0.0")]
        [TestCase(true, "1.17.0-preview.1")]
        [TestCase(true, "garbage")]
        public void PureInputSystemErrorNamesCurrentMinimumAndAllFixes(bool present,
            string? version)
        {
            var snapshot = present
                ? InputSystemPackageSnapshot.Present(version ?? string.Empty)
                : InputSystemPackageSnapshot.Missing();

            var decision = InputSystemCompatibilityPolicy.Decide(snapshot,
                InputHandlingMode.InputSystemOnly);

            Assert.That(decision.Severity, Is.EqualTo(InputSystemValidationSeverity.Error));
            Assert.That(decision.BlocksBuild, Is.True);
            Assert.That(decision.Message, Does.Contain("current:"));
            Assert.That(decision.Message, Does.Contain("minimum: 1.16.0"));
            Assert.That(decision.Message, Does.Contain("Upgrade com.unity.inputsystem"));
            Assert.That(decision.Message, Does.Contain("Active Input Handling to Both"));
            Assert.That(decision.Message, Does.Contain("Input Manager (Old)"));
        }

        [Test]
        public void BothWarningNamesFallbackWithoutClaimingStrictTextInputSupport()
        {
            var decision = InputSystemCompatibilityPolicy.Decide(
                InputSystemPackageSnapshot.Present("1.14.2"),
                InputHandlingMode.Both);

            Assert.That(decision.Severity, Is.EqualTo(InputSystemValidationSeverity.Warning));
            Assert.That(decision.BlocksBuild, Is.False);
            Assert.That(decision.Message, Does.Contain("legacy delta-only scroll fallback"));
            Assert.That(decision.Message, Does.Contain("strict TextInput focus remains unavailable"));
        }
    }
}
