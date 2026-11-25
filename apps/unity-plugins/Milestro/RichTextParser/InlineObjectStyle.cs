using System;
using System.Globalization;
using Milestro.ResourceManagement;
using Milestro.RichTextParser.Css;
using UnityEngine;

namespace Milestro.RichTextParser
{
    internal readonly struct InlineLength
    {
        internal InlineLength(float value, bool em = false) { Value = value; Em = em; }
        internal float Value { get; }
        internal bool Em { get; }
        internal float Resolve(float fontSize) => Value * (Em ? fontSize : 1f);

        internal static InlineLength Parse(string text, bool positive)
        {
            text = text.Trim().ToLowerInvariant();
            var em = text.EndsWith("em", StringComparison.Ordinal);
            if (em || text.EndsWith("px", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 2);
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                !float.IsFinite(value) || (positive ? value <= 0f : value < 0f))
                throw new ArgumentException("Inline lengths must be finite positive sizes or nonnegative margins in px or em.");
            return new InlineLength(value, em);
        }
    }

    internal sealed class InlineObjectStyle
    {
        internal InlineLength? Width;
        internal InlineLength? Height;
        internal InlineLength Top;
        internal InlineLength Right;
        internal InlineLength Bottom;
        internal InlineLength Left;
        internal InlineObjectAlignment Alignment = InlineObjectAlignment.Middle;
        internal InlineObjectFit Fit = InlineObjectFit.Contain;
        internal Vector2 Position = new Vector2(0.5f, 0.5f);
        internal float Opacity = 1f;
        internal bool Visible = true;
        internal InlineImageBinding? Binding;
        internal Rect ContentRect;
        internal Vector2 OuterSize;

        internal void Resolve(float fontSize)
        {
            var natural = Binding?.IntrinsicSize ?? Vector2.zero;
            var ratio = natural.x > 0 && natural.y > 0 ? natural.x / natural.y : 1f;
            var height = Height?.Resolve(fontSize) ?? (Width.HasValue ? Width.Value.Resolve(fontSize) / ratio : fontSize);
            var width = Width?.Resolve(fontSize) ?? height * ratio;
            var left = Left.Resolve(fontSize);
            var top = Top.Resolve(fontSize);
            ContentRect = new Rect(left, top, width, height);
            OuterSize = new Vector2(left + width + Right.Resolve(fontSize), top + height + Bottom.Resolve(fontSize));
            if (!float.IsFinite(OuterSize.x) || !float.IsFinite(OuterSize.y))
                throw new ArgumentException("Inline dimensions overflowed.");
        }

        internal void Apply(string declarations)
        {
            var parser = new CssDeclarationParser(declarations);
            while (parser.TryRead(out var declaration))
            {
                if (!declaration.IsValid) continue;
                var value = declaration.Value.ToString().Trim().ToLowerInvariant();
                switch (declaration.Property.ToString().ToLowerInvariant())
                {
                    case "width": Width = value == "auto" ? null : InlineLength.Parse(value, true); break;
                    case "height": Height = value == "auto" ? null : InlineLength.Parse(value, true); break;
                    case "margin-top": Top = InlineLength.Parse(value, false); break;
                    case "margin-right": Right = InlineLength.Parse(value, false); break;
                    case "margin-bottom": Bottom = InlineLength.Parse(value, false); break;
                    case "margin-left": Left = InlineLength.Parse(value, false); break;
                    case "margin":
                        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 1 || parts.Length > 4) throw new ArgumentException("Invalid inline margin.");
                        Top = InlineLength.Parse(parts[0], false);
                        Right = parts.Length > 1 ? InlineLength.Parse(parts[1], false) : Top;
                        Bottom = parts.Length > 2 ? InlineLength.Parse(parts[2], false) : Top;
                        Left = parts.Length > 3 ? InlineLength.Parse(parts[3], false) : Right;
                        break;
                    case "vertical-align":
                        Alignment = value switch
                        {
                            "baseline" => InlineObjectAlignment.Baseline,
                            "top" => InlineObjectAlignment.Top,
                            "bottom" => InlineObjectAlignment.Bottom,
                            "middle" => InlineObjectAlignment.Middle,
                            _ => throw new ArgumentException("Inline vertical-align supports baseline, top, bottom, middle.")
                        };
                        break;
                    case "object-fit":
                        Fit = value switch
                        {
                            "contain" => InlineObjectFit.Contain,
                            "cover" => InlineObjectFit.Cover,
                            "fill" => InlineObjectFit.Fill,
                            "none" => InlineObjectFit.None,
                            "scale-down" => InlineObjectFit.ScaleDown,
                            _ => throw new ArgumentException("Invalid object-fit.")
                        };
                        break;
                    case "object-position": Position = ParsePosition(value); break;
                    case "opacity":
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity) || !float.IsFinite(opacity))
                            throw new ArgumentException("Invalid inline opacity.");
                        Opacity = Mathf.Clamp01(opacity);
                        break;
                    case "visibility":
                        Visible = value switch { "visible" => true, "hidden" => false, _ => throw new ArgumentException("Invalid inline visibility.") };
                        break;
                }
            }
        }

        private static Vector2 ParsePosition(string value)
        {
            var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0] == "top" || parts[0] == "bottom"
                    ? new Vector2(0.5f, PositionPart(parts[0], false))
                    : new Vector2(PositionPart(parts[0], true), 0.5f);
            if (parts.Length != 2) throw new ArgumentException("object-position requires one or two keywords or percentages.");
            if (parts[0] == "top" || parts[0] == "bottom" || parts[1] == "left" || parts[1] == "right")
                return new Vector2(PositionPart(parts[1], true), PositionPart(parts[0], false));
            return new Vector2(PositionPart(parts[0], true), PositionPart(parts[1], false));
        }

        private static float PositionPart(string value, bool horizontal)
        {
            if (value == "center") return 0.5f;
            if (value == (horizontal ? "left" : "top")) return 0f;
            if (value == (horizontal ? "right" : "bottom")) return 1f;
            if (value.EndsWith("%", StringComparison.Ordinal) &&
                float.TryParse(value.Substring(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) && float.IsFinite(amount)) return amount / 100f;
            throw new ArgumentException("Invalid object-position.");
        }
    }
}
