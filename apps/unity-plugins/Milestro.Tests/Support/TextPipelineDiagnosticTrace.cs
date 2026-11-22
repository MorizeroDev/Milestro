using System;
using System.Diagnostics;
using Milestro.Components.Internal;

namespace Milestro.Tests
{
    internal sealed class TextPipelineDiagnosticTrace
    {
        private readonly Observation[] observationBuffer;
        private readonly LabelSample[] labelBuffer;
        private int observationCount;
        public readonly Request[] requests;
        public readonly int labels;
        public readonly double requestIntervalMs;
        public ArraySegment<Observation> observations => new ArraySegment<Observation>(observationBuffer, 0, observationCount);
        public ArraySegment<LabelSample> labelSamples => new ArraySegment<LabelSample>(labelBuffer, 0, observationCount * labels);

        internal TextPipelineDiagnosticTrace(int requestCount, int labelCount, double intervalMs, int capacity = 8192)
        {
            Deadline(0, 0, intervalMs, Stopwatch.Frequency);
            if (requestCount <= 0 || labelCount <= 0 || capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestCount));
            }
            requests = new Request[requestCount];
            labels = labelCount;
            requestIntervalMs = intervalMs;
            observationBuffer = new Observation[capacity];
            labelBuffer = new LabelSample[checked(capacity * labelCount)];
        }

        internal static long Deadline(long start, int requestIndex, double intervalMs, long frequency)
        {
            if (requestIndex < 0 || frequency <= 0 || double.IsNaN(intervalMs) ||
                double.IsInfinity(intervalMs) || intervalMs < 0 || intervalMs > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalMs));
            }
            return checked(start + (long)Math.Round(requestIndex * intervalMs * frequency / 1000d));
        }

        internal int BeginObservation(string phase, int frame, double frameMs, double mainThreadMs, bool focused)
        {
            if (observationCount == observationBuffer.Length)
            {
                throw new InvalidOperationException("Diagnostic trace capacity exhausted; no observations were discarded.");
            }
            var index = observationCount++;
            observationBuffer[index] = new Observation
            {
                phase = phase, frame = frame, frameMs = frameMs, mainThreadMs = mainThreadMs,
                applicationFocused = focused, timestamp = Stopwatch.GetTimestamp()
            };
            return index;
        }

        internal void CaptureLabel(int observation, int label, TextPipelineFreshness state,
            long output, long presenter, bool hasOutput)
        {
            labelBuffer[observation * labels + label] = new LabelSample
            {
                requested = state.RequestedVersion, prepared = state.PreparedVersion,
                submitted = state.SubmittedVersion, completed = state.CompletedVersion,
                requestedFrame = state.RequestedFrame, completedRequestFrame = state.CompletedRequestFrame,
                completedFrame = state.CompletedFrame, preparations = state.PreparationAttempts,
                accepted = state.AcceptedSubmissions, completions = state.Completions,
                unique = state.WindowCompletedVersions, carryover = state.WindowCarryoverCompletions,
                output = output, presenter = presenter, hasOutput = hasOutput, unsupported = state.UnsupportedSubmission
            };
        }

        internal void EndObservation(int observation)
        {
            observationBuffer[observation].endTimestamp = Stopwatch.GetTimestamp();
        }

        internal struct Request
        {
            public long deadline;
            public long beforeSetTimestamp;
            public long afterSetTimestamp;
            public int frame;
        }

        internal struct Observation
        {
            public string phase;
            public int frame;
            public long timestamp;
            public long endTimestamp;
            public double frameMs;
            public double mainThreadMs;
            public bool applicationFocused;
        }

        internal struct LabelSample
        {
            public long requested;
            public long prepared;
            public long submitted;
            public long completed;
            public int requestedFrame;
            public int completedRequestFrame;
            public int completedFrame;
            public long preparations;
            public long accepted;
            public long completions;
            public long unique;
            public long carryover;
            public long output;
            public long presenter;
            public bool hasOutput;
            public bool unsupported;
        }
    }
}
