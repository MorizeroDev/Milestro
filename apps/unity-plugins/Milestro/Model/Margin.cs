using System;

namespace Milestro.Model
{
    [Serializable]
    public sealed class Margin
    {
        public MarginValue Left;
        public MarginValue Top;
        public MarginValue Right;
        public MarginValue Bottom;

        public ResolvedMargin Resolve(MarginResolveContext context)
        {
            return new ResolvedMargin(Left.Resolve(context, MarginAxis.Horizontal),
                Top.Resolve(context, MarginAxis.Vertical),
                Right.Resolve(context, MarginAxis.Horizontal),
                Bottom.Resolve(context, MarginAxis.Vertical));
        }

        public void Normalize()
        {
            Left.Normalize();
            Top.Normalize();
            Right.Normalize();
            Bottom.Normalize();
        }

        public static Margin FixedZero()
        {
            return new Margin();
        }
    }
}
