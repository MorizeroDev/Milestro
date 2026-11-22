using NUnit.Framework;

namespace Milestro.Tests
{
    public class TextPipelineCompletionGateTests
    {
        private static TextPipelineVersionSnapshot Output()
        {
            return new TextPipelineVersionSnapshot
            {
                requested = 37,
                completed = 37,
                output = 12,
                presenter = 12,
                hasOutput = true
            };
        }

        [TestCase(1)]
        [TestCase(119)]
        [TestCase(120)]
        public void ConvergenceAcceptsThroughBoundaryFrameWithDistinctVersionSequences(int frames)
        {
            var decision = TextPipelineCompletionGate.Observe(frames, 4999.9, 1, 1, 1, 1, 0);
            Assert.That(decision, Is.EqualTo(TextPipelineCompletionGate.Decision.Accepted));
            Assert.That(TextPipelineCompletionGate.AcceptFinal(decision, new[] { Output() }), Is.True);
        }

        [Test]
        public void ConvergenceRequiresAtLeastOneScheduledObservationFrame()
        {
            Assert.That(TextPipelineCompletionGate.Observe(0, 0, 1, 1, 1, 1, 0),
                Is.EqualTo(TextPipelineCompletionGate.Decision.Waiting));
        }

        [TestCase(1, 0, 1, 1, 0)]
        [TestCase(1, 1, 1, 0, 0)]
        [TestCase(1, 1, 0, 1, 0)]
        [TestCase(1, 1, 1, 1, 1)]
        public void IncompletePresenterMissingOutputAndUnsupportedWaitUntilFrameLimit(
            int labels, int current, int valid, int synced, int unsupported)
        {
            for (var frames = 1; frames <= TextPipelineCompletionGate.MaxFrames; ++frames)
            {
                var decision = TextPipelineCompletionGate.Observe(frames, frames, labels, current, valid, synced, unsupported);
                Assert.That(decision, Is.EqualTo(frames < TextPipelineCompletionGate.MaxFrames
                    ? TextPipelineCompletionGate.Decision.Waiting : TextPipelineCompletionGate.Decision.FrameLimit));
            }
        }

        [TestCase(1, 5000)]
        [TestCase(120, 5000)]
        [TestCase(120, 6000)]
        public void TimeLimitTakesPriorityOverConvergenceAndFrameLimit(int frames, double wallMs)
        {
            var decision = TextPipelineCompletionGate.Observe(frames, wallMs, 1, 1, 1, 1, 0);
            Assert.That(decision, Is.EqualTo(TextPipelineCompletionGate.Decision.TimeLimit));
            Assert.That(TextPipelineCompletionGate.AcceptFinal(decision, new[] { Output() }), Is.False);
        }

        [Test]
        public void ObservationAfterFrameBudgetCannotPassEvenWithConvergence()
        {
            Assert.That(TextPipelineCompletionGate.Observe(121, 100, 1, 1, 1, 1, 0),
                Is.EqualTo(TextPipelineCompletionGate.Decision.FrameLimit));
        }

        [TestCase(1)]
        [TestCase(120)]
        public void NonconvergedObservationAlsoStopsAtTimeLimit(int frames)
        {
            Assert.That(TextPipelineCompletionGate.Observe(frames, 5000, 1, 0, 1, 1, 0),
                Is.EqualTo(TextPipelineCompletionGate.Decision.TimeLimit));
        }

        [TestCase("incomplete")]
        [TestCase("presenter")]
        [TestCase("output")]
        [TestCase("unsupported")]
        public void FinalSnapshotsIndependentlyRejectInvalidLabels(string fault)
        {
            var invalid = Output();
            if (fault == "incomplete") --invalid.completed;
            if (fault == "presenter") --invalid.presenter;
            if (fault == "output") invalid.hasOutput = false;
            if (fault == "unsupported") invalid.unsupported = true;
            Assert.That(TextPipelineCompletionGate.AcceptFinal(TextPipelineCompletionGate.Decision.Accepted,
                new[] { Output(), invalid }), Is.False);
        }

        [Test]
        public void EmptySnapshotCannotVacuouslyPass()
        {
            Assert.That(TextPipelineCompletionGate.AcceptFinal(TextPipelineCompletionGate.Decision.Accepted,
                new TextPipelineVersionSnapshot[0]), Is.False);
        }

        [TestCase("FrameLimit")]
        [TestCase("TimeLimit")]
        [TestCase("Waiting")]
        public void FailureCannotBeRescuedByLaterSnapshotAndPropagatesToReportExit(
            string decisionName)
        {
            var decision = (TextPipelineCompletionGate.Decision)System.Enum.Parse(
                typeof(TextPipelineCompletionGate.Decision), decisionName);
            var completionAccepted = TextPipelineCompletionGate.AcceptFinal(decision, new[] { Output() });
            var completed = TextPipelineCompletionGate.CaseCompleted(true, true, completionAccepted);
            var exitCode = TextPipelineCompletionGate.ExitCode(false, 0, 1, !completed);
            Assert.That(completionAccepted, Is.False);
            Assert.That(completed, Is.False);
            Assert.That(exitCode != 0, Is.True);
            Assert.That(exitCode, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulCompletionAndTimingOnlyCanPassButFixtureFailureCannot()
        {
            Assert.That(TextPipelineCompletionGate.CaseCompleted(true, true, true), Is.True);
            Assert.That(TextPipelineCompletionGate.CaseCompleted(true, false, false), Is.True);
            Assert.That(TextPipelineCompletionGate.CaseCompleted(false, true, true), Is.False);
            Assert.That(TextPipelineCompletionGate.ExitCode(false, 0, 1, false), Is.Zero);
        }

        [TestCase(true, 0, 1, false)]
        [TestCase(false, 1, 1, false)]
        [TestCase(false, 0, 0, false)]
        [TestCase(false, 0, 1, true)]
        public void FatalErrorsMissingCasesAndFailedCasesHaveNonzeroExit(bool fatal, int errors, int cases, bool failedCase)
        {
            Assert.That(TextPipelineCompletionGate.ExitCode(fatal, errors, cases, failedCase), Is.EqualTo(1));
        }
    }
}
