using UnityEngine;

namespace Milestro.Components
{
    public interface ITextBoxScrollTarget
    {
        Vector2 GetScrollPercent();
        float GetScrollPercentX();
        float GetScrollPercentY();
        void ScrollToPercent(Vector2 percent, bool animated = false);
        void ScrollToPercentX(float percent, bool animated = false);
        void ScrollToPercentY(float percent, bool animated = false);

        bool TryGetScrollState(out TextBoxScrollState state);
    }
}
