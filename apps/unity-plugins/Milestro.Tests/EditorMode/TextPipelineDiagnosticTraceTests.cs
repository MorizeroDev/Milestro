using System;
using Milestro.Components.Internal;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class TextPipelineDiagnosticTraceTests
    {
        [TestCase(0, 0, 1000)]
        [TestCase(1, 4, 41000)]
        [TestCase(239, 8, 19121000)]
        public void DeadlinesUseAbsoluteOriginWithoutAccumulatedDelay(int index, double interval, long expected)
        {
            Assert.That(TextPipelineDiagnosticTrace.Deadline(1000, index, interval, 10000000), Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(101)]
        public void InvalidCadenceIsRejected(double interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TextPipelineDiagnosticTrace.Deadline(0, 0, interval, 1000));
        }

        [Test]
        public void TraceRetainsCompletedAndPresentedSequencesIndependently()
        {
            var trace = new TextPipelineDiagnosticTrace(240, 1, 4, 2);
            var observation = trace.BeginObservation("wait", 20, 1, 0.8, true);
            var state = new TextPipelineFreshness
            {
                RequestedVersion = 7,
                PreparedVersion = 7,
                SubmittedVersion = 6,
                CompletedVersion = 6,
                AcceptedSubmissions = 5,
                Completions = 5,
                WindowCompletedVersions = 4
            };
            trace.CaptureLabel(observation, 0, state, 100, 99, true);
            trace.EndObservation(observation);
            Assert.That(trace.observations.Count, Is.EqualTo(1));
            Assert.That(trace.labelSamples.Array[0].completed, Is.EqualTo(6));
            Assert.That(trace.labelSamples.Array[0].output, Is.EqualTo(100));
            Assert.That(trace.labelSamples.Array[0].presenter, Is.EqualTo(99));
            Assert.That(trace.observations.Array[0].endTimestamp, Is.GreaterThanOrEqualTo(trace.observations.Array[0].timestamp));
        }

        [Test]
        public void TraceCapacityFailsInsteadOfDeletingOldObservations()
        {
            var trace = new TextPipelineDiagnosticTrace(240, 1, 0, 1);
            trace.BeginObservation("request", 1, 1, 1, true);
            Assert.Throws<InvalidOperationException>(() => trace.BeginObservation("wait", 2, 1, 1, true));
            Assert.That(trace.observations.Count, Is.EqualTo(1));
            Assert.That(trace.observations.Array[0].frame, Is.EqualTo(1));
        }
    }
}
