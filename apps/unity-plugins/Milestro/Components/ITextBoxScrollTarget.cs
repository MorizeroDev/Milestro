using UnityEngine;

namespace Milestro.Components
{
    public interface ITextBoxScrollTarget
    {
        Vector2 ScrollPercent { get; set; }
        float ScrollPercentX { get; set; }
        float ScrollPercentY { get; set; }

        bool TryGetScrollState(out TextBoxScrollState state);
    }
}
