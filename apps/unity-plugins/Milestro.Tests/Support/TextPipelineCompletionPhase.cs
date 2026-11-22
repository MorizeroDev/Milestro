namespace Milestro.Tests
{
    internal sealed class TextPipelineCompletionPhase
    {
        public double wallMs;
        public int frames;
        public long requestedChanges;
        public long preparationAttempts;
        public long acceptedSubmissions;
        public long completedSubmissions;
        public long completedRequestedVersions;
        public long carryoverCompletions;
        public long deferredFastSubmitAttempts;
        public long gcAllocBytes;
        public int gen0Collections;
        public bool applicationFocusedAtEnd;
    }
}
