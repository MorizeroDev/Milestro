namespace Milestro.Tests
{
    internal static class TextPipelineCompletionGate
    {
        internal const int MaxFrames = 120;
        internal const double TimeoutMs = 5000;

        internal enum Decision
        {
            Waiting,
            Accepted,
            FrameLimit,
            TimeLimit
        }

        internal static Decision Observe(int frames, double wallMs, int labels, int currentCompletions,
            int validOutputs, int presenterSynced, int unsupportedLabels)
        {
            if (wallMs >= TimeoutMs) return Decision.TimeLimit;
            if (frames > MaxFrames) return Decision.FrameLimit;
            if (frames > 0 && labels > 0 && currentCompletions == labels && validOutputs == labels &&
                presenterSynced == labels && unsupportedLabels == 0) return Decision.Accepted;
            return frames >= MaxFrames ? Decision.FrameLimit : Decision.Waiting;
        }

        internal static bool AcceptFinal(Decision decision, TextPipelineVersionSnapshot[] outputs)
        {
            if (decision != Decision.Accepted || outputs == null || outputs.Length == 0) return false;
            foreach (var state in outputs)
            {
                if (state == null || state.unsupported || !state.hasOutput || state.requested != state.completed ||
                    state.output != state.presenter) return false;
            }
            return true;
        }

        internal static bool CaseCompleted(bool fixtureAccepted, bool trackCompletion, bool completionAccepted)
        {
            return fixtureAccepted && (!trackCompletion || completionAccepted);
        }

        internal static int ExitCode(bool fatal, int errors, int caseCount, bool hasFailedCase)
        {
            return fatal || errors > 0 || caseCount == 0 || hasFailedCase ? 1 : 0;
        }
    }
}
