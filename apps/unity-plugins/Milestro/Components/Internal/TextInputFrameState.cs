using System.Text;
using Milestro.Input;

namespace Milestro.Components.Internal
{
    internal sealed class TextInputFrameState
    {
        internal string CompositionText { get; private set; } = "";

        internal string Apply(HybridInputFrame frame)
        {
            StringBuilder? committedText = null;
            for (var i = 0; i < frame.Events.Count; ++i)
            {
                var inputEvent = frame.Events[i];
                switch (inputEvent.Kind)
                {
                    case HybridInputEventKind.CommittedText:
                        CompositionText = "";
                        committedText ??= new StringBuilder();
                        committedText.Append(inputEvent.Text);
                        break;
                    case HybridInputEventKind.Composition:
                        CompositionText = inputEvent.Text;
                        break;
                }
            }
            return committedText?.ToString() ?? "";
        }

        internal void Reset()
        {
            CompositionText = "";
        }
    }
}
