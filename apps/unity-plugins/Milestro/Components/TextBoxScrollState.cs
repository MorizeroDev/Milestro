namespace Milestro.Components
{
    public readonly struct TextBoxScrollState
    {
        public readonly float ScrollX;
        public readonly float ScrollY;
        public readonly float ViewportWidth;
        public readonly float ViewportHeight;
        public readonly float ContentWidth;
        public readonly float ContentHeight;

        public TextBoxScrollState(float scrollX,
            float scrollY,
            float viewportWidth,
            float viewportHeight,
            float contentWidth,
            float contentHeight)
        {
            ScrollX = scrollX;
            ScrollY = scrollY;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
        }
    }
}
