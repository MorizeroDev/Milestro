using System;
using Milestro.Skia;

namespace Milestro.Components.Internal
{
    internal sealed class SlimTextGlyphMeshOutput : IDisposable
    {
        internal SlimTextGlyphMeshOutput(TextGlyphMeshOutput output)
        {
            Output = output;
        }

        internal TextGlyphMeshOutput Output { get; }
        internal bool IsValid => Output.IsValid;
        public void Dispose() => Output.Dispose();
    }
}
