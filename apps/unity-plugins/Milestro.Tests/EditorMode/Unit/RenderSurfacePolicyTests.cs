using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Milestro.Configuration;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class RenderSurfacePolicyTests
    {
        private static readonly RenderSurfaceRuntimeCaps DefaultCaps = new RenderSurfaceRuntimeCaps(16384, 16384);

        [TestCase(1f, 100, 80)]
        [TestCase(1.25f, 125, 100)]
        [TestCase(1.5f, 150, 120)]
        [TestCase(2f, 200, 160)]
        public void CommonScalesProduceCheckedUniformRasterTargets(float scale, int width, int height)
        {
            Assert.That(TryPlan(100, 80, scale, out var plan, out var failure), Is.True, failure.ToString());
            Assert.That(plan.RequestedScale, Is.EqualTo(scale));
            Assert.That(plan.ClampedScale, Is.EqualTo(scale).Within(0.0001f));
            Assert.That(plan.Candidates[0].RasterWidth, Is.EqualTo(width));
            Assert.That(plan.Candidates[0].RasterHeight, Is.EqualTo(height));
            Assert.That(plan.Candidates[0].EffectiveScale, Is.EqualTo(scale).Within(0.0001f));
        }

        [Test]
        public void Logical1080pAtTwoXProducesPhysical4KWithinDefaultBudget()
        {
            Assert.That(TryPlan(1920, 1080, 2f, out var plan, out var failure), Is.True, failure.ToString());
            var candidate = plan.Candidates[0];
            Assert.That(candidate.RasterWidth, Is.EqualTo(3840));
            Assert.That(candidate.RasterHeight, Is.EqualTo(2160));
            Assert.That(candidate.ByteCount, Is.EqualTo(33_177_600L));
            Assert.That(candidate.ByteCount, Is.LessThan(RenderSurfaceConfiguration.DefaultMaxBytesPerSurface));
        }

        [Test]
        public void Logical4KAtTwoXIsObservablyClampedBy4096SquaredPixelAnd64MiBBudget()
        {
            Assert.That(TryPlan(3840, 2160, 2f, out var plan, out var failure), Is.True, failure.ToString());
            var candidate = plan.Candidates[0];
            Assert.That(plan.RequestedScale, Is.EqualTo(2f));
            Assert.That(plan.ClampedScale, Is.InRange(1.422f, 1.4222f));
            Assert.That(candidate.EffectiveScale, Is.EqualTo(plan.ClampedScale).Within(0.0001f));
            Assert.That(candidate.RasterWidth, Is.EqualTo(5461));
            Assert.That(candidate.RasterHeight, Is.EqualTo(3072));
            Assert.That(candidate.PixelCount, Is.EqualTo(16_776_192L));
            Assert.That(candidate.ByteCount, Is.EqualTo(67_104_768L));
            Assert.That(candidate.ByteCount, Is.LessThan(64L * 1024L * 1024L));
        }

        [Test]
        public void Square4096AtTwoXClampsExactlyToOneX()
        {
            Assert.That(TryPlan(4096, 4096, 2f, out var plan, out var failure), Is.True, failure.ToString());
            var candidate = plan.Candidates[0];
            Assert.That(plan.ClampedScale, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(candidate.RasterWidth, Is.EqualTo(4096));
            Assert.That(candidate.RasterHeight, Is.EqualTo(4096));
            Assert.That(candidate.ByteCount, Is.EqualTo(64L * 1024L * 1024L));
        }

        [TestCase(0, 100, 1f)]
        [TestCase(-1, 100, 1f)]
        [TestCase(100, 0, 1f)]
        [TestCase(100, 100, 0f)]
        [TestCase(100, 100, -1f)]
        public void NonPositiveRequestsFailClosed(int width, int height, float scale)
        {
            Assert.That(TryPlan(width, height, scale, out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(RenderSurfaceFailureReason.InvalidRequest));
        }

        [Test]
        public void NonFiniteAndExtremeRequestsFailClosedWithoutOverflow()
        {
            Assert.That(TryPlan(100, 100, float.NaN, out _, out var nanFailure), Is.False);
            Assert.That(nanFailure, Is.EqualTo(RenderSurfaceFailureReason.InvalidRequest));
            Assert.That(TryPlan(100, 100, float.PositiveInfinity, out _, out var infinityFailure), Is.False);
            Assert.That(infinityFailure, Is.EqualTo(RenderSurfaceFailureReason.InvalidRequest));
            Assert.That(TryPlan(int.MaxValue, int.MaxValue, 2f, out _, out _), Is.False);
        }

        [Test]
        public void UnknownFormatAndUnsupportedMsaaDoNotAssumeFourBytesPerPixel()
        {
            var textureDescriptor = TextureDescriptor();
            textureDescriptor.PreferredFormat = (UnitySkiaRenderTextureFormat)999;
            Assert.That(RenderSurfacePolicy.TryBuildCandidatePlan(new RenderSurfaceRasterRequest(100, 100, 1f),
                DefaultCaps,
                new RenderSurfaceConfiguration(),
                SurfaceDescriptor(textureDescriptor),
                out _,
                out var formatFailure), Is.False);
            Assert.That(formatFailure, Is.EqualTo(RenderSurfaceFailureReason.InvalidConfiguration));

            textureDescriptor = TextureDescriptor();
            textureDescriptor.MsaaSamples = 4;
            Assert.That(RenderSurfaceDescriptorAccounting.TryResolveBytesPerPixel(
                SurfaceDescriptor(textureDescriptor), out _), Is.False);
        }

        [Test]
        public void UnknownBackendAndMipAssumptionsFailClosed()
        {
            Assert.That(RenderSurfaceDescriptorAccounting.TryResolveBytesPerPixel(
                new RenderSurfaceDescriptor((UnitySkiaGraphicsBackend)999, TextureDescriptor(), 1),
                out _), Is.False);
            Assert.That(RenderSurfaceDescriptorAccounting.TryResolveBytesPerPixel(
                new RenderSurfaceDescriptor(UnitySkiaGraphicsBackend.OpenGL, TextureDescriptor(), 0),
                out _), Is.False);
            Assert.That(RenderSurfaceDescriptorAccounting.TryResolveBytesPerPixel(
                new RenderSurfaceDescriptor(UnitySkiaGraphicsBackend.OpenGL, TextureDescriptor(), 2),
                out _), Is.False);
        }

        [Test]
        public void QuantizationRoundsUpToStableQuarterScaleAndRejectsInvalidValues()
        {
            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(1.26f, 0.25f, 2f, out var scale), Is.True);
            Assert.That(scale, Is.EqualTo(1.5f));
            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(float.NaN, 0.25f, 2f, out _), Is.False);
            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(1f, 0f, 2f, out _), Is.False);
        }

        [Test]
        public void QuantizationHysteresisPreventsResizeChurnAroundBothBucketEdges()
        {
            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(1.251f,
                0.25f,
                0.05f,
                2f,
                1.25f,
                out var heldBelowUpperBoundary), Is.True);
            Assert.That(heldBelowUpperBoundary, Is.EqualTo(1.25f));

            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(1.301f,
                0.25f,
                0.05f,
                2f,
                1.25f,
                out var crossedUpperBoundary), Is.True);
            Assert.That(crossedUpperBoundary, Is.EqualTo(1.5f));

            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(1.249f,
                0.25f,
                0.05f,
                2f,
                1.5f,
                out var heldAboveLowerBoundary), Is.True);
            Assert.That(heldAboveLowerBoundary, Is.EqualTo(1.5f));

            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(1.199f,
                0.25f,
                0.05f,
                2f,
                1.5f,
                out var crossedLowerBoundary), Is.True);
            Assert.That(crossedLowerBoundary, Is.EqualTo(1.25f));
        }

        [Test]
        public void CandidateCountIsBoundedAndStrictlyDecreasesRasterDimensions()
        {
            var configuration = new RenderSurfaceConfiguration
            {
                MaxAttemptsPerRequestAndEpoch = 3
            };
            Assert.That(RenderSurfacePolicy.TryBuildCandidatePlan(
                new RenderSurfaceRasterRequest(101, 79, 2f),
                DefaultCaps,
                configuration,
                SurfaceDescriptor(),
                out var plan,
                out var failure), Is.True, failure.ToString());
            Assert.That(plan.Candidates, Has.Count.EqualTo(3));
            for (var index = 1; index < plan.Candidates.Count; ++index)
            {
                Assert.That(plan.Candidates[index].EffectiveScale,
                    Is.LessThan(plan.Candidates[index - 1].EffectiveScale));
                Assert.That(plan.Candidates[index].RasterWidth,
                    Is.LessThan(plan.Candidates[index - 1].RasterWidth));
                Assert.That(plan.Candidates[index].RasterHeight,
                    Is.LessThan(plan.Candidates[index - 1].RasterHeight));
            }
        }

        [Test]
        public void InvalidHysteresisConfigurationFailsClosed()
        {
            var configuration = new RenderSurfaceConfiguration
            {
                ScaleQuantum = 0.25f,
                ScaleHysteresis = 0.25f
            };
            Assert.That(RenderSurfacePolicy.TryBuildCandidatePlan(
                new RenderSurfaceRasterRequest(100, 100, 1f),
                DefaultCaps,
                configuration,
                SurfaceDescriptor(),
                out _,
                out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(RenderSurfaceFailureReason.InvalidConfiguration));
        }

        [Test]
        public void LedgerReserveCommitRetireReleaseIsIdempotentAndAdvancesGenerationOnce()
        {
            var ledger = new RenderSurfaceBudgetLedger();
            var configuration = new RenderSurfaceConfiguration();
            Assert.That(ledger.TryReserve(1024, 0, configuration, out var reservation, out _), Is.True);
            var reserved = ledger.Snapshot();
            Assert.That(reserved.ReservedBytes, Is.EqualTo(1024));
            Assert.That(reserved.CommittedBytes, Is.Zero);

            var lease = reservation.Commit();
            reservation.Dispose();
            var committed = ledger.Snapshot();
            Assert.That(committed.ReservedBytes, Is.Zero);
            Assert.That(committed.CommittedBytes, Is.EqualTo(1024));

            lease.MarkRetired();
            lease.MarkRetired();
            var beforeReleaseGeneration = ledger.Generation;
            lease.Dispose();
            lease.Dispose();
            var released = ledger.Snapshot();
            Assert.That(released.CommittedBytes, Is.Zero);
            Assert.That(released.RetireCount, Is.EqualTo(1));
            Assert.That(released.ReleaseCount, Is.EqualTo(1));
            Assert.That(released.Generation, Is.EqualTo(beforeReleaseGeneration + 1));
        }

        [Test]
        public void LedgerSeparatesTransitionAndGlobalBudgetFailuresAndReopensAfterRelease()
        {
            var ledger = new RenderSurfaceBudgetLedger();
            var configuration = new RenderSurfaceConfiguration
            {
                MaxGlobalBytes = 100,
                MaxTransitionBytes = 60
            };
            Assert.That(ledger.TryReserve(50, 0, configuration, out var reservation, out _), Is.True);
            var lease = reservation.Commit();

            Assert.That(ledger.TryReserve(20, 50, configuration, out _, out var transitionFailure), Is.False);
            Assert.That(transitionFailure, Is.EqualTo(RenderSurfaceFailureReason.TransitionBudget));

            configuration.MaxTransitionBytes = 100;
            Assert.That(ledger.TryReserve(60, 0, configuration, out _, out var globalFailure), Is.False);
            Assert.That(globalFailure, Is.EqualTo(RenderSurfaceFailureReason.GlobalBudget));

            var generation = ledger.Generation;
            lease.MarkRetired();
            lease.Dispose();
            Assert.That(ledger.Generation, Is.GreaterThan(generation));
            Assert.That(ledger.TryReserve(60, 0, configuration, out var recovered, out _), Is.True);
            recovered.Dispose();
        }

        [Test]
        public void LedgerConcurrentSettlementNeverLeaksOrDoubleDecrements()
        {
            const int iterationCount = 128;
            var ledger = new RenderSurfaceBudgetLedger();
            var configuration = new RenderSurfaceConfiguration
            {
                MaxGlobalBytes = 1024,
                MaxTransitionBytes = 1024
            };

            Parallel.For(0, iterationCount, _ =>
            {
                Assert.That(ledger.TryReserve(1,
                    0,
                    configuration,
                    out var reservation,
                    out var failure), Is.True, failure.ToString());
                var lease = reservation.Commit();
                reservation.Dispose();
                lease.MarkRetired();
                Parallel.For(0, 4, __ => lease.Dispose());
            });

            var snapshot = ledger.Snapshot();
            Assert.That(snapshot.CommittedBytes, Is.Zero);
            Assert.That(snapshot.ReservedBytes, Is.Zero);
            Assert.That(snapshot.ReservationCount, Is.EqualTo(iterationCount));
            Assert.That(snapshot.CommitCount, Is.EqualTo(iterationCount));
            Assert.That(snapshot.RetireCount, Is.EqualTo(iterationCount));
            Assert.That(snapshot.ReleaseCount, Is.EqualTo(iterationCount));
            Assert.That(snapshot.Generation, Is.EqualTo(1 + iterationCount));
        }

        [Test]
        public void AllocationFailuresUseDeterministicBoundedBackoff()
        {
            var controller = new RenderSurfaceRecoveryController();
            var key = RecoveryKey();
            Assert.That(controller.TryBeginAttempt(key, 0, 3, out var first, out _), Is.True);
            Assert.That(first.Index, Is.Zero);
            controller.RecordFailure(first, RenderSurfaceFailureReason.Allocation, 0);

            Assert.That(controller.TryBeginAttempt(key,
                0,
                3,
                out _,
                out var firstBackoff), Is.False);
            Assert.That(firstBackoff, Is.EqualTo(RenderSurfaceFailureReason.Allocation));
            key = RecoveryKey(ledgerGeneration: 2);
            Assert.That(controller.TryBeginAttempt(key, 1, 3, out var second, out _), Is.True);
            Assert.That(second.Index, Is.EqualTo(1));
            controller.RecordFailure(second, RenderSurfaceFailureReason.NativeHandle, 1);

            Assert.That(controller.TryBeginAttempt(key, 2, 3, out _, out _), Is.False);
            Assert.That(controller.TryBeginAttempt(key, 3, 3, out var third, out _), Is.True);
            Assert.That(third.Index, Is.EqualTo(2));
            controller.RecordFailure(third, RenderSurfaceFailureReason.TextureValidation, 3);

            Assert.That(controller.TryBeginAttempt(key,
                ulong.MaxValue,
                3,
                out _,
                out var exhausted), Is.False);
            Assert.That(exhausted, Is.EqualTo(RenderSurfaceFailureReason.RetryExhausted));
        }

        [Test]
        public void BudgetFailureReopensOnlyWhenLedgerGenerationChanges()
        {
            var controller = new RenderSurfaceRecoveryController();
            var blockedKey = RecoveryKey(ledgerGeneration: 7);
            Assert.That(controller.TryBeginAttempt(blockedKey, 0, 8, out var attempt, out _), Is.True);
            controller.RecordFailure(attempt, RenderSurfaceFailureReason.GlobalBudget, 0);
            Assert.That(controller.TryBeginAttempt(blockedKey, 100, 8, out _, out _), Is.False);

            var releasedBudgetKey = RecoveryKey(ledgerGeneration: 8);
            Assert.That(controller.TryBeginAttempt(releasedBudgetKey,
                100,
                8,
                out var reopened,
                out _), Is.True);
            Assert.That(reopened.Index, Is.Zero);
        }

        [Test]
        public void StaticFailureDoesNotReopenForUnrelatedLedgerRelease()
        {
            var controller = new RenderSurfaceRecoveryController();
            var invalidKey = RecoveryKey(ledgerGeneration: 4);
            Assert.That(controller.TryBeginAttempt(invalidKey, 0, 8, out var attempt, out _), Is.True);
            controller.RecordFailure(attempt, RenderSurfaceFailureReason.InvalidRequest, 0);

            var unrelatedReleaseKey = RecoveryKey(ledgerGeneration: 5);
            Assert.That(controller.TryBeginAttempt(unrelatedReleaseKey,
                100,
                8,
                out _,
                out var suppression), Is.False);
            Assert.That(suppression, Is.EqualTo(RenderSurfaceFailureReason.RetryExhausted));
        }

        [Test]
        public void EpochAndConfigurationChangesIgnoreStaleCompletionAndReopenImmediately()
        {
            var controller = new RenderSurfaceRecoveryController();
            var oldKey = RecoveryKey(deviceEpoch: 3);
            Assert.That(controller.TryBeginAttempt(oldKey, 0, 8, out var staleAttempt, out _), Is.True);

            var configuration = new RenderSurfaceConfiguration
            {
                MaxGlobalBytes = RenderSurfaceConfiguration.DefaultMaxGlobalBytes - 1
            };
            var newKey = RecoveryKey(deviceEpoch: 4, configuration: configuration);
            Assert.That(controller.TryBeginAttempt(newKey, 0, 8, out var currentAttempt, out _), Is.True);
            controller.RecordFailure(staleAttempt, RenderSurfaceFailureReason.NativeHandle, 0);
            controller.RecordSuccess(currentAttempt);

            Assert.That(controller.TryBeginAttempt(newKey, 0, 8, out var afterSuccess, out _), Is.True);
            Assert.That(afterSuccess.Index, Is.Zero);
        }

        [Test]
        public void ReplacementKeepsLastKnownGoodAndSettlesRetiredLeaseAfterCompletion()
        {
            var ledger = new RenderSurfaceBudgetLedger();
            var replacement = new RenderSurfaceReplacement<FakeTarget>(ledger);
            var configuration = new RenderSurfaceConfiguration
            {
                MaxGlobalBytes = 1000,
                MaxTransitionBytes = 1000
            };
            var deferred = new List<Action>();
            Action<FakeTarget, RenderSurfaceBudgetLedger.RenderSurfaceBudgetLease> retire = (target, lease) =>
                deferred.Add(() =>
                {
                    target.Dispose();
                    lease.Dispose();
                });

            var first = new FakeTarget("first");
            Assert.That(replacement.TryReplace(100,
                configuration,
                () => RenderSurfaceAllocationResult<FakeTarget>.Succeeded(first),
                target => target.Dispose(),
                retire,
                out var firstFailure), Is.True, firstFailure.ToString());
            Assert.That(replacement.CurrentTarget, Is.SameAs(first));
            Assert.That(ledger.Snapshot().CommittedBytes, Is.EqualTo(100));

            var rejected = new FakeTarget("rejected");
            Assert.That(replacement.TryReplace(200,
                configuration,
                () => RenderSurfaceAllocationResult<FakeTarget>.Failed(
                    RenderSurfaceFailureReason.NativeHandle,
                    rejected),
                target => target.Dispose(),
                retire,
                out var validationFailure), Is.False);
            Assert.That(validationFailure, Is.EqualTo(RenderSurfaceFailureReason.NativeHandle));
            Assert.That(rejected.Disposed, Is.True);
            Assert.That(replacement.CurrentTarget, Is.SameAs(first));
            Assert.That(ledger.Snapshot().CommittedBytes, Is.EqualTo(100));

            var second = new FakeTarget("second");
            Assert.That(replacement.TryReplace(200,
                configuration,
                () => RenderSurfaceAllocationResult<FakeTarget>.Succeeded(second),
                target => target.Dispose(),
                retire,
                out var secondFailure), Is.True, secondFailure.ToString());
            Assert.That(replacement.CurrentTarget, Is.SameAs(second));
            Assert.That(first.Disposed, Is.False);
            Assert.That(deferred, Has.Count.EqualTo(1));
            Assert.That(ledger.Snapshot().CommittedBytes, Is.EqualTo(300));

            deferred[0]();
            deferred[0]();
            Assert.That(first.Disposed, Is.True);
            Assert.That(ledger.Snapshot().CommittedBytes, Is.EqualTo(200));
            Assert.That(ledger.Snapshot().ReleaseCount, Is.EqualTo(1));

            replacement.Clear(retire);
            replacement.Clear(retire);
            Assert.That(replacement.CurrentTarget, Is.Null);
            Assert.That(deferred, Has.Count.EqualTo(2));
            deferred[1]();
            Assert.That(second.Disposed, Is.True);
            Assert.That(ledger.Snapshot().CommittedBytes, Is.Zero);
        }

        [Test]
        public void ReplacementBudgetFailureDoesNotInvokeAllocatorOrDropCurrentTarget()
        {
            var ledger = new RenderSurfaceBudgetLedger();
            var replacement = new RenderSurfaceReplacement<FakeTarget>(ledger);
            var configuration = new RenderSurfaceConfiguration
            {
                MaxGlobalBytes = 100,
                MaxTransitionBytes = 100
            };
            var current = new FakeTarget("current");
            Assert.That(replacement.TryReplace(80,
                configuration,
                () => RenderSurfaceAllocationResult<FakeTarget>.Succeeded(current),
                target => target.Dispose(),
                (target, lease) =>
                {
                    target.Dispose();
                    lease.Dispose();
                },
                out _), Is.True);

            var allocatorCalled = false;
            Assert.That(replacement.TryReplace(30,
                configuration,
                () =>
                {
                    allocatorCalled = true;
                    return RenderSurfaceAllocationResult<FakeTarget>.Succeeded(new FakeTarget("unexpected"));
                },
                target => target.Dispose(),
                (target, lease) =>
                {
                    target.Dispose();
                    lease.Dispose();
                },
                out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(RenderSurfaceFailureReason.TransitionBudget));
            Assert.That(allocatorCalled, Is.False);
            Assert.That(replacement.CurrentTarget, Is.SameAs(current));
            Assert.That(ledger.Snapshot().CommittedBytes, Is.EqualTo(80));
        }

        [Test]
        public void ReplacementReleaseExceptionsStillSettleReservationAndRetiredLease()
        {
            var ledger = new RenderSurfaceBudgetLedger();
            var replacement = new RenderSurfaceReplacement<FakeTarget>(ledger);
            var configuration = new RenderSurfaceConfiguration
            {
                MaxGlobalBytes = 1000,
                MaxTransitionBytes = 1000
            };
            var first = new FakeTarget("first");
            Assert.That(replacement.TryReplace(100,
                configuration,
                () => RenderSurfaceAllocationResult<FakeTarget>.Succeeded(first),
                target => target.Dispose(),
                (target, lease) =>
                {
                    target.Dispose();
                    lease.Dispose();
                },
                out _), Is.True);

            Assert.Throws<InvalidOperationException>(() => replacement.TryReplace(200,
                configuration,
                () => RenderSurfaceAllocationResult<FakeTarget>.Failed(
                    RenderSurfaceFailureReason.NativeHandle,
                    new FakeTarget("rejected")),
                _ => throw new InvalidOperationException("rejected release"),
                (target, lease) =>
                {
                    target.Dispose();
                    lease.Dispose();
                },
                out _));
            Assert.That(ledger.Snapshot().ReservedBytes, Is.Zero);
            Assert.That(ledger.Snapshot().CommittedBytes, Is.EqualTo(100));
            Assert.That(replacement.CurrentTarget, Is.SameAs(first));

            var second = new FakeTarget("second");
            Assert.Throws<InvalidOperationException>(() => replacement.TryReplace(200,
                configuration,
                () => RenderSurfaceAllocationResult<FakeTarget>.Succeeded(second),
                target => target.Dispose(),
                (_, __) => throw new InvalidOperationException("retirement scheduling"),
                out _));
            Assert.That(replacement.CurrentTarget, Is.SameAs(second));
            Assert.That(ledger.Snapshot().CommittedBytes, Is.EqualTo(200));
            Assert.That(ledger.Snapshot().ReservedBytes, Is.Zero);
            Assert.That(ledger.Snapshot().ReleaseCount, Is.EqualTo(1));

            replacement.Clear((target, lease) =>
            {
                target.Dispose();
                lease.Dispose();
            });
            Assert.That(ledger.Snapshot().CommittedBytes, Is.Zero);
        }

        [Test]
        public void DiagnosticsDeduplicateByReasonAndEpochWhileConfigurationChangesFingerprint()
        {
            var diagnostics = new RenderSurfaceDiagnosticDeduplicator();
            Assert.That(diagnostics.ShouldEmit(RenderSurfaceFailureReason.GlobalBudget, 7), Is.True);
            Assert.That(diagnostics.ShouldEmit(RenderSurfaceFailureReason.GlobalBudget, 7), Is.False);
            Assert.That(diagnostics.ShouldEmit(RenderSurfaceFailureReason.NativeHandle, 7), Is.True);
            Assert.That(diagnostics.ShouldEmit(RenderSurfaceFailureReason.GlobalBudget, 8), Is.True);

            var configuration = new RenderSurfaceConfiguration();
            var before = RenderSurfaceConfigurationFingerprint.Compute(configuration);
            configuration.MaxGlobalBytes--;
            Assert.That(RenderSurfaceConfigurationFingerprint.Compute(configuration), Is.Not.EqualTo(before));

            var ledger = new RenderSurfaceBudgetLedger();
            Assert.That(ledger.TryReserve(4096,
                0,
                configuration,
                out var reservation,
                out var failure), Is.True, failure.ToString());
            var budget = ledger.Snapshot();
            var diagnostic = new RenderSurfaceDiagnostic(RenderSurfaceFailureReason.NativeHandle,
                UnitySkiaGraphicsBackend.OpenGL,
                2f,
                1.5f,
                1.25f,
                3,
                11,
                budget.Generation,
                4096,
                budget);
            Assert.That(diagnostic.RequestedScale, Is.EqualTo(2f));
            Assert.That(diagnostic.ClampedScale, Is.EqualTo(1.5f));
            Assert.That(diagnostic.EffectiveScale, Is.EqualTo(1.25f));
            Assert.That(diagnostic.Backend, Is.EqualTo(UnitySkiaGraphicsBackend.OpenGL));
            Assert.That(diagnostic.AttemptIndex, Is.EqualTo(3));
            Assert.That(diagnostic.DeviceEpoch, Is.EqualTo(11));
            Assert.That(diagnostic.LedgerGeneration, Is.EqualTo(budget.Generation));
            Assert.That(diagnostic.RequestedBytes, Is.EqualTo(4096));
            Assert.That(diagnostic.ReservedBytes, Is.EqualTo(4096));
            Assert.That(diagnostics.ShouldEmit(diagnostic), Is.True);
            Assert.That(diagnostics.ShouldEmit(diagnostic), Is.False);
            reservation.Dispose();
        }

        private static bool TryPlan(int width,
            int height,
            float scale,
            out RenderSurfaceCandidatePlan plan,
            out RenderSurfaceFailureReason failure)
        {
            return RenderSurfacePolicy.TryBuildCandidatePlan(new RenderSurfaceRasterRequest(width, height, scale),
                DefaultCaps,
                new RenderSurfaceConfiguration(),
                SurfaceDescriptor(),
                out plan,
                out failure);
        }

        private static RenderSurfaceDescriptor SurfaceDescriptor()
        {
            return SurfaceDescriptor(TextureDescriptor());
        }

        private static RenderSurfaceDescriptor SurfaceDescriptor(
            UnitySkiaRenderTextureDescriptor textureDescriptor)
        {
            return new RenderSurfaceDescriptor(UnitySkiaGraphicsBackend.OpenGL, textureDescriptor, 1);
        }

        private static UnitySkiaRenderTextureDescriptor TextureDescriptor()
        {
            return new UnitySkiaRenderTextureDescriptor(1, 1, ColorSpace.Gamma);
        }

        private static RenderSurfaceRecoveryKey RecoveryKey(long ledgerGeneration = 1,
            ulong deviceEpoch = 1,
            RenderSurfaceConfiguration configuration = null)
        {
            configuration ??= new RenderSurfaceConfiguration();
            return new RenderSurfaceRecoveryKey(new RenderSurfaceRasterRequest(100, 80, 1.5f),
                SurfaceDescriptor(),
                deviceEpoch,
                ledgerGeneration,
                RenderSurfaceConfigurationFingerprint.Compute(configuration));
        }

        private sealed class FakeTarget : IDisposable
        {
            internal FakeTarget(string name)
            {
                Name = name;
            }

            internal string Name { get; }
            internal bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
