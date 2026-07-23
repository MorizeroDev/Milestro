using Milestro.Input;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class HybridInputSystemCompatibilityTests
    {
        [TestCase(HybridInputSystemPackageStatus.NotApplicable)]
        [TestCase(HybridInputSystemPackageStatus.Missing)]
        [TestCase(HybridInputSystemPackageStatus.BelowMinimum)]
        [TestCase(HybridInputSystemPackageStatus.Supported)]
        [TestCase(HybridInputSystemPackageStatus.Unsupported)]
        public void PackageStatusIsAdditiveToExistingDiagnostics(
            HybridInputSystemPackageStatus packageStatus)
        {
            var originalStatus = HybridInputSystemCompatibility.PackageStatus;
            var before = HybridInputRuntime.Diagnostics;
            try
            {
                HybridInputSystemCompatibility.SetPackageStatus(packageStatus);

                var after = HybridInputRuntime.Diagnostics;

                Assert.That(after.InputSystemPackageStatus, Is.EqualTo(packageStatus));
                Assert.That(after.SelectionStatus, Is.EqualTo(before.SelectionStatus));
                Assert.That(after.ProviderId, Is.EqualTo(before.ProviderId));
                Assert.That(after.ProviderKind, Is.EqualTo(before.ProviderKind));
                Assert.That(after.Capabilities, Is.EqualTo(before.Capabilities));
                Assert.That(after.ScrollCapability, Is.EqualTo(before.ScrollCapability));
                Assert.That(after.ActiveModuleType, Is.EqualTo(before.ActiveModuleType));
                Assert.That(after.EventSystemCount, Is.EqualTo(before.EventSystemCount));
                Assert.That(after.ApplicationFocused, Is.EqualTo(before.ApplicationFocused));
                Assert.That(after.LastDiagnostic, Is.EqualTo(before.LastDiagnostic));
                Assert.That(after.DiagnosticCount, Is.EqualTo(before.DiagnosticCount));
            }
            finally
            {
                HybridInputSystemCompatibility.SetPackageStatus(originalStatus);
            }
        }

        [Test]
        public void SupportedRangeConstantsMatchAssemblyVersionDefines()
        {
            Assert.That(HybridInputSystemCompatibility.MinimumVersion, Is.EqualTo("1.16.0"));
            Assert.That(HybridInputSystemCompatibility.MaximumVersionExclusive,
                Is.EqualTo("2.0.0-0"));
        }
    }
}
