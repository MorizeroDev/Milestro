using Unity.Profiling;

namespace Milestro.Components.Internal
{
    internal static class TextBoxPipelineProfiler
    {
        internal static bool Enabled;

        internal static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("Milestro.TextBox.Update");
        internal static readonly ProfilerMarker RebuildMarker =
            new ProfilerMarker("Milestro.TextBox.Rebuild");
        internal static readonly ProfilerMarker ApplyOutputMarker =
            new ProfilerMarker("Milestro.TextBox.ApplyOutput");
        internal static readonly ProfilerMarker RasterMeasureMarker =
            new ProfilerMarker("Milestro.TextBox.RasterMeasure");
        internal static readonly ProfilerMarker SettingsMarker =
            new ProfilerMarker("Milestro.TextBox.Settings");
        internal static readonly ProfilerMarker ViewportMarker =
            new ProfilerMarker("Milestro.TextBox.Viewport");
        internal static readonly ProfilerMarker TargetRebuildMarker =
            new ProfilerMarker("Milestro.TextBox.TargetRebuild");
        internal static readonly ProfilerMarker SizeMarker =
            new ProfilerMarker("Milestro.TextBox.Size");
        internal static readonly ProfilerMarker BuildParagraphsMarker =
            new ProfilerMarker("Milestro.TextBox.BuildParagraphs");
        internal static readonly ProfilerMarker ResizeParagraphsMarker =
            new ProfilerMarker("Milestro.TextBox.ResizeParagraphs");
        internal static readonly ProfilerMarker BuildCommandsMarker =
            new ProfilerMarker("Milestro.TextBox.BuildCommands");
        internal static readonly ProfilerMarker SubmitMarker =
            new ProfilerMarker("Milestro.TextBox.Submit");
        internal static readonly ProfilerMarker ParseRichTextMarker =
            new ProfilerMarker("Milestro.TextBox.ParseRichText");
        internal static readonly ProfilerMarker ConvertRichTextMarker =
            new ProfilerMarker("Milestro.TextBox.ConvertRichText");
        internal static readonly ProfilerMarker CreateNativeParagraphsMarker =
            new ProfilerMarker("Milestro.TextBox.CreateNativeParagraphs");
        internal static readonly ProfilerMarker PlainTextMarker =
            new ProfilerMarker("Milestro.TextBox.PlainText");
        internal static readonly ProfilerMarker BlockMetadataMarker =
            new ProfilerMarker("Milestro.TextBox.BlockMetadata");
        internal static readonly ProfilerMarker ContentSnapshotMarker =
            new ProfilerMarker("Milestro.TextBox.ContentSnapshot");
        internal static readonly ProfilerMarker RetireParagraphsMarker =
            new ProfilerMarker("Milestro.TextBox.RetireParagraphs");
        internal static readonly ProfilerMarker CreateBaseStylesMarker =
            new ProfilerMarker("Milestro.TextBox.CreateBaseStyles");
        internal static readonly ProfilerMarker NoWrapProbeLayoutMarker =
            new ProfilerMarker("Milestro.TextBox.NoWrapProbeLayout");
        internal static readonly ProfilerMarker NoWrapMeasureMarker =
            new ProfilerMarker("Milestro.TextBox.NoWrapMeasure");
        internal static readonly ProfilerMarker NoWrapFinalLayoutMarker =
            new ProfilerMarker("Milestro.TextBox.NoWrapFinalLayout");
        internal static readonly ProfilerMarker NoWrapHeightMarker =
            new ProfilerMarker("Milestro.TextBox.NoWrapHeight");
    }
}
