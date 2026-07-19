using System;
using Milestro.Util;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxHorizontalScrollRequest
    {
        private TextBoxHorizontalScrollRequest(float value)
        {
            HasValue = true;
            Value = value;
        }

        internal bool HasValue { get; }
        internal float Value { get; }

        internal static TextBoxHorizontalScrollRequest None => default;

        internal static TextBoxHorizontalScrollRequest FromValue(float value)
        {
            return new TextBoxHorizontalScrollRequest(value);
        }
    }

    internal readonly struct TextBoxHorizontalScrollState
    {
        private TextBoxHorizontalScrollState(float scrollX,
            float defaultAnchor,
            bool hasLayout,
            bool followsDefaultAnchor)
        {
            ScrollX = scrollX;
            DefaultAnchor = defaultAnchor;
            HasLayout = hasLayout;
            FollowsDefaultAnchor = followsDefaultAnchor;
        }

        internal float ScrollX { get; }
        internal float DefaultAnchor { get; }
        internal bool HasLayout { get; }
        internal bool FollowsDefaultAnchor { get; }

        internal TextBoxHorizontalScrollState WithUserRequest(float requestedScrollX)
        {
            requestedScrollX = SanitizeOffset(requestedScrollX);
            return new TextBoxHorizontalScrollState(requestedScrollX,
                DefaultAnchor,
                HasLayout,
                HasLayout && Approximately(requestedScrollX, DefaultAnchor));
        }

        internal TextBoxHorizontalScrollState Resolve(TextBoxNoWrapHorizontalLayout layout,
            TextBoxHorizontalScrollRequest request)
        {
            var requestedScrollX = request.HasValue ? SanitizeOffset(request.Value) : ScrollX;
            var followsDefault = !HasLayout || FollowsDefaultAnchor;
            if (request.HasValue && HasLayout && !Approximately(requestedScrollX, ScrollX))
            {
                followsDefault = Approximately(requestedScrollX, DefaultAnchor);
            }

            var nextScrollX = followsDefault ? layout.InitialScrollX : requestedScrollX;
            nextScrollX = Math.Min(layout.MaxScrollX, SanitizeOffset(nextScrollX));
            return new TextBoxHorizontalScrollState(nextScrollX,
                layout.InitialScrollX,
                true,
                followsDefault);
        }

        private static float SanitizeOffset(float value)
        {
            return FloatUtil.IsFinite(value) ? Math.Max(0f, value) : 0f;
        }

        private static bool Approximately(float a, float b)
        {
            return Math.Abs(a - b) <= 0.0001f;
        }
    }
}
