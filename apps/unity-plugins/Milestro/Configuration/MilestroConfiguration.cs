namespace Milestro.Configuration
{
    public class MilestroConfiguration
    {
        public static MilestroConfiguration Configuration { get; set; } = new MilestroConfiguration();

        public InputBoxShortcutConfiguration InputBoxShortcut { get; set; } = new InputBoxShortcutConfiguration();

        public ScrollAxisLockConfiguration ScrollAxisLock { get; set; } = new ScrollAxisLockConfiguration();

        public ScrollTweenConfiguration ScrollTween { get; set; } = new ScrollTweenConfiguration();

        public TextInputConfiguration TextInput { get; set; } = new TextInputConfiguration();

        public WorldSpaceTextBoxConfiguration WorldSpaceTextBox { get; set; } = new WorldSpaceTextBoxConfiguration();

        public IcuConfiguration Icu { get; set; } = new IcuConfiguration();
    }
}
