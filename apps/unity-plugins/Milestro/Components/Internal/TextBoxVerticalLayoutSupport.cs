using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Binding;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using Milestro.Util;

namespace Milestro.Components.Internal
{
    internal sealed class TextBoxRenderPreparation
    {
        internal TextBoxRenderPreparation(TextBoxRenderTargetSettings settings,
            ParagraphPayload payload,
            NotSupportedException? diagnostic,
            bool usesHorizontalFallback)
        {
            Settings = settings;
            Payload = payload;
            Diagnostic = diagnostic;
            UsesHorizontalFallback = usesHorizontalFallback;
        }

        internal TextBoxRenderTargetSettings Settings { get; }
        internal ParagraphPayload Payload { get; }
        internal NotSupportedException? Diagnostic { get; }
        internal bool UsesHorizontalFallback { get; }
    }

    /// <summary>
    /// First-pass guardrails for vertical-rl Canvas TextBoxes. These checks turn combinations
    /// that the native vertical layout rejects, or that other text consumers do not support,
    /// into explicit managed failures before any old successful output can be retained.
    /// </summary>
    internal static class TextBoxVerticalLayoutSupport
    {
        internal static bool IsVertical(ParagraphStyleSettings style)
        {
            return style != null && style.writingMode == WritingMode.VerticalRl;
        }

        internal static bool IsVertical(TextBoxRenderTargetSettings settings)
        {
            return settings.ParagraphStyle != null && IsVertical(settings.ParagraphStyle);
        }

        internal static TextBoxRenderPreparation PrepareRenderConfiguration(
            TextBoxRenderTargetSettings requested)
        {
            var issues = new List<string>();
            var payload = requested.ParseContent(false, issues);
            var writingMode = requested.ParagraphStyle?.writingMode ?? WritingMode.HorizontalTb;
            if (writingMode == WritingMode.HorizontalTb)
            {
                return new TextBoxRenderPreparation(requested, payload,
                    CreateDiagnostic(issues), false);
            }

            var requestedVertical = writingMode == WritingMode.VerticalRl;
            if (!requestedVertical)
            {
                issues.Add($"writingMode={(int)writingMode} defaulted to HorizontalTb");
            }

            var structuralFallback = requestedVertical && RequiresHorizontalFallback(payload, issues);
            if (!requestedVertical || structuralFallback)
            {
                return PrepareHorizontalFallback(requested, payload, issues);
            }

            var verticalIssues = new List<string>();
            var verticalPayload = requested.ParseContent(true, verticalIssues);
            foreach (var issue in verticalIssues)
            {
                if (!issues.Contains(issue)) issues.Add(issue);
            }

            return PrepareVerticalFallback(requested, verticalPayload, issues);
        }

        internal static TextBoxRenderTargetSettings WithNaturalLineHeight(
            TextBoxRenderTargetSettings settings)
        {
            var textStyle = CloneTextStyle(settings.TextStyle);
            textStyle.lineHeight = 0f;
            return new TextBoxRenderTargetSettings(settings.Content,
                CloneMargin(settings.Margin),
                CloneParagraphStyle(settings.ParagraphStyle),
                textStyle,
                settings.ParserId,
                settings.WrapMode,
                settings.TextOverflow,
                settings.AutoFontSize,
                settings.MinSize,
                settings.MaxSize,
                settings.Sizing,
                settings.MarginReferenceSize,
                settings.VerticalHeightMode,
                settings.VerticalMarginContext,
                settings.SizingUsesVerticalLayout);
        }

        private static TextBoxRenderPreparation PrepareVerticalFallback(
            TextBoxRenderTargetSettings requested,
            ParagraphPayload payload,
            List<string> issues)
        {
            var paragraphStyle = CloneParagraphStyle(requested.ParagraphStyle);
            var textStyle = CloneTextStyle(requested.TextStyle);
            var margin = CloneMargin(requested.Margin);

            paragraphStyle.writingMode = WritingMode.VerticalRl;
            if (paragraphStyle.textDirection != TextDirection.Ltr)
            {
                issues.Add($"textDirection={paragraphStyle.textDirection} defaulted to Ltr");
                paragraphStyle.textDirection = TextDirection.Ltr;
            }

            if (!IsSupportedVerticalAlignment(paragraphStyle.textAlign))
            {
                issues.Add($"textAlign={paragraphStyle.textAlign} defaulted to Start");
                paragraphStyle.textAlign = TextAlign.Start;
            }

            if (!FloatUtil.IsFinite(requested.ParagraphStyle.paragraphSpacing) ||
                requested.ParagraphStyle.paragraphSpacing < 0f)
            {
                issues.Add($"paragraphSpacing={requested.ParagraphStyle.paragraphSpacing} defaulted to 0");
                paragraphStyle.paragraphSpacing = 0f;
            }

            SanitizeTextStyle(textStyle, issues);
            var textOverflow = SanitizeTextOverflow(requested.TextOverflow, issues);
            SanitizePayload(payload, true, issues, textStyle.size);
            foreach (var ruby in payload.RubyAnnotations)
            {
                if (ruby.Style.Position == RubyPosition.Below)
                {
                    issues.Add("ruby-position under defaulted to Above for vertical-rl");
                    ruby.Style.Position = RubyPosition.Above;
                }
            }
            var autoFontSize = requested.AutoFontSize &&
                               requested.VerticalHeightMode != TextHeightMode.Auto &&
                               textOverflow != TextOverflow.Ellipsis;
            if (requested.AutoFontSize &&
                (requested.VerticalHeightMode == TextHeightMode.Auto || textOverflow == TextOverflow.Ellipsis))
            {
                issues.Add("auto font sizing defaulted to the fixed base font size");
            }
            DiagnoseVerticalMarginContext(requested, issues);
            var sizing = requested.Sizing;
            if (requested.VerticalHeightMode == TextHeightMode.Auto)
            {
                issues.Add("heightMode=Auto has no fixed vertical content height; " +
                           "content width sizing is disabled for this render");
                sizing = null;
            }
            var effective = new TextBoxRenderTargetSettings(requested.Content,
                margin,
                paragraphStyle,
                textStyle,
                requested.ParserId,
                requested.WrapMode,
                textOverflow,
                autoFontSize: autoFontSize,
                minSize: requested.MinSize,
                maxSize: requested.MaxSize,
                sizing: sizing,
                marginReferenceSize: requested.MarginReferenceSize,
                verticalHeightMode: requested.VerticalHeightMode,
                verticalMarginContext: requested.VerticalMarginContext,
                sizingUsesVerticalLayout: sizing.HasValue &&
                                          requested.SizingUsesVerticalLayout);
            return new TextBoxRenderPreparation(effective, payload,
                CreateDiagnostic(issues), false);
        }

        private static TextBoxRenderPreparation PrepareHorizontalFallback(
            TextBoxRenderTargetSettings requested,
            ParagraphPayload payload,
            List<string> issues)
        {
            var paragraphStyle = CloneParagraphStyle(requested.ParagraphStyle);
            var textStyle = CloneTextStyle(requested.TextStyle);
            var margin = CloneMargin(requested.Margin);
            paragraphStyle.writingMode = WritingMode.HorizontalTb;
            SanitizeHorizontalParagraphStyle(paragraphStyle, issues);
            SanitizeTextStyle(textStyle, issues);
            var textOverflow = SanitizeTextOverflow(requested.TextOverflow, issues);
            SanitizePayload(payload, false, issues, 0f);
            var sizing = requested.SizingUsesVerticalLayout ? null : requested.Sizing;
            var effective = new TextBoxRenderTargetSettings(requested.Content,
                margin,
                paragraphStyle,
                textStyle,
                requested.ParserId,
                requested.WrapMode,
                textOverflow,
                requested.AutoFontSize,
                requested.MinSize,
                requested.MaxSize,
                sizing,
                requested.MarginReferenceSize,
                requested.VerticalHeightMode,
                requested.VerticalMarginContext,
                false);
            return new TextBoxRenderPreparation(effective, payload,
                CreateDiagnostic(issues), true);
        }

        private static void DiagnoseVerticalMarginContext(TextBoxRenderTargetSettings settings,
            List<string> issues)
        {
            if (!settings.VerticalMarginContext.HasValue)
            {
                return;
            }

            var context = settings.VerticalMarginContext.Value;
            DiagnoseVerticalMargin("left", settings.Margin.Left, context, issues);
            DiagnoseVerticalMargin("top", settings.Margin.Top, context, issues);
            DiagnoseVerticalMargin("right", settings.Margin.Right, context, issues);
            DiagnoseVerticalMargin("bottom", settings.Margin.Bottom, context, issues);
        }

        private static void DiagnoseVerticalMargin(string edge,
            MarginValue margin,
            MarginResolveContext context,
            List<string> issues)
        {
            if (margin.Auto || !FloatUtil.IsFinite(margin.Value) || margin.Value == 0f)
            {
                return;
            }

            if (margin.Unit == MarginUnit.ContainerPercent && !context.HasContainerWidth)
            {
                issues.Add($"margin.{edge} ContainerPercent={margin.Value} has no definite " +
                           "containing block width; defaulted to 0");
            }
            else if ((margin.Unit == MarginUnit.Vw || margin.Unit == MarginUnit.Vh) &&
                     !context.HasViewport)
            {
                issues.Add($"margin.{edge} {margin.Unit}={margin.Value} has no root Canvas " +
                           "viewport; defaulted to 0");
            }
        }

        private static void SanitizeHorizontalParagraphStyle(ParagraphStyleSettings style,
            List<string> issues)
        {
            if (!Enum.IsDefined(typeof(TextDirection), style.textDirection))
            {
                issues.Add($"textDirection={(int)style.textDirection} defaulted to Ltr");
                style.textDirection = TextDirection.Ltr;
            }

            if (!Enum.IsDefined(typeof(TextAlign), style.textAlign))
            {
                issues.Add($"textAlign={(int)style.textAlign} defaulted to Start");
                style.textAlign = TextAlign.Start;
            }
        }

        private static bool RequiresHorizontalFallback(ParagraphPayload payload,
            List<string> issues)
        {
            foreach (var block in payload.Blocks)
            {
                foreach (var segment in block.Segments)
                {
                    if (!ContainsStrongRtl(segment.Content ?? ""))
                    {
                        continue;
                    }

                    issues.Add("strong RTL content requires horizontal fallback");
                    return true;
                }
            }

            return false;
        }

        private static void SanitizeTextStyle(TextStyleSettings style, List<string> issues)
        {
            if (!FloatUtil.IsFinite(style.size) || style.size <= 0f)
            {
                var fallback = new TextStyleSettings().size;
                issues.Add($"fontSize={style.size} defaulted to {fallback}");
                style.size = fallback;
            }

            if (!FloatUtil.IsFinite(style.lineHeight))
            {
                issues.Add($"lineHeight={style.lineHeight} defaulted to 0 (natural metrics)");
                style.lineHeight = 0f;
            }

            if (!FloatUtil.IsFinite(style.letterSpacing) || style.letterSpacing < 0f)
            {
                issues.Add($"letterSpacing={style.letterSpacing} defaulted to 0");
                style.letterSpacing = 0f;
            }

            if (!FloatUtil.IsFinite(style.wordSpacing) || style.wordSpacing < 0f)
            {
                issues.Add($"wordSpacing={style.wordSpacing} defaulted to 0");
                style.wordSpacing = 0f;
            }
        }

        private static TextOverflow SanitizeTextOverflow(TextOverflow overflow,
            List<string> issues)
        {
            if (!Enum.IsDefined(typeof(TextOverflow), overflow))
            {
                issues.Add($"textOverflow={(int)overflow} defaulted to Clip");
                return TextOverflow.Clip;
            }
            return overflow;
        }

        private static bool IsResolvedEmIndentRepresentable(ParagraphStyleState style,
            float baseFontSize)
        {
            if (!style.TextIndentEm.HasValue)
            {
                return true;
            }

            var fontSize = style.TextIndentFontSize ?? baseFontSize;
            var resolved = (double)style.TextIndentEm.Value * fontSize *
                           style.TextIndentFontSizeScale;
            return double.IsFinite(resolved) &&
                   resolved <= float.MaxValue &&
                   resolved >= -float.MaxValue;
        }

        /// <summary>
        /// Reports whether the effective first-column indent of every payload block can be
        /// represented for the inline extent used by the current vertical layout. A positive
        /// percentage resolves against a finite content height; an infinite extent resolves
        /// percentage indents to zero, matching the native layout.
        /// </summary>
        internal static bool HasUnrepresentableVerticalIndent(ParagraphPayload payload,
            float inlineExtent,
            float baseFontSize)
        {
            foreach (var style in EffectiveIndentStyles(payload))
            {
                if (!TryResolveVerticalIndent(style, inlineExtent, baseFontSize, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Temporarily replaces a vertical indent that cannot be represented for the current
        /// inline extent with zero pixels. The original payload values are restored when the
        /// returned scope is disposed, so a later layout with a representable height recovers.
        /// </summary>
        internal static IDisposable? UseRepresentableVerticalIndent(ParagraphPayload payload,
            float inlineExtent,
            float baseFontSize,
            out bool degraded)
        {
            List<(ParagraphStyleState Style, TextIndent? Indent, float? Em)>? changes = null;
            foreach (var style in EffectiveIndentStyles(payload))
            {
                if (TryResolveVerticalIndent(style, inlineExtent, baseFontSize, out _))
                {
                    continue;
                }

                changes ??= new List<(ParagraphStyleState, TextIndent?, float?)>();
                changes.Add((style, style.TextIndent, style.TextIndentEm));
                style.TextIndent = TextIndent.Pixels(0f);
                style.TextIndentEm = null;
            }

            degraded = changes != null;
            return changes == null ? null : new VerticalIndentRestoreScope(changes);
        }

        private static IEnumerable<ParagraphStyleState> EffectiveIndentStyles(
            ParagraphPayload payload)
        {
            if (payload.Blocks.Count == 0)
            {
                yield return payload.ParagraphStyle;
                yield break;
            }

            foreach (var block in payload.Blocks)
            {
                var style = block.Style;
                if (style != null && (style.TextIndent.HasValue || style.TextIndentEm.HasValue))
                {
                    yield return style;
                }
                else
                {
                    yield return payload.ParagraphStyle;
                }
            }
        }

        private static bool TryResolveVerticalIndent(ParagraphStyleState style,
            float inlineExtent,
            float baseFontSize,
            out float resolved)
        {
            resolved = 0f;
            if (style == null)
            {
                return true;
            }

            double value;
            if (style.TextIndent.HasValue)
            {
                var indent = style.TextIndent.Value;
                if (!indent.IsPercentage)
                {
                    value = indent.Value;
                }
                else if (!FloatUtil.IsFinite(inlineExtent))
                {
                    return true;
                }
                else
                {
                    value = (double)indent.Value * inlineExtent / 100.0;
                }
            }
            else if (style.TextIndentEm.HasValue)
            {
                var fontSize = style.TextIndentFontSize ?? baseFontSize;
                value = (double)style.TextIndentEm.Value * fontSize *
                        style.TextIndentFontSizeScale;
            }
            else
            {
                return true;
            }

            if (!double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
            {
                return false;
            }

            if (FloatUtil.IsFinite(inlineExtent) && inlineExtent - value > float.MaxValue)
            {
                return false;
            }

            resolved = (float)value;
            return true;
        }

        private sealed class VerticalIndentRestoreScope : IDisposable
        {
            private readonly List<(ParagraphStyleState Style, TextIndent? Indent, float? Em)>
                changes;

            internal VerticalIndentRestoreScope(
                List<(ParagraphStyleState Style, TextIndent? Indent, float? Em)> changes)
            {
                this.changes = changes;
            }

            public void Dispose()
            {
                foreach (var change in changes)
                {
                    change.Style.TextIndent = change.Indent;
                    change.Style.TextIndentEm = change.Em;
                }
                changes.Clear();
            }
        }

        private static void SanitizePayload(ParagraphPayload payload,
            bool vertical,
            List<string> issues,
            float baseFontSize)
        {
            SanitizeParagraphState(payload.ParagraphStyle, "payload", vertical, issues,
                baseFontSize);
            foreach (var block in payload.Blocks)
            {
                SanitizeParagraphState(block.Style, "block", vertical, issues, baseFontSize);
                foreach (var segment in block.Segments)
                {
                    SanitizeTextState(segment.TextStyle, vertical, issues);
                }
            }
        }

        private static void SanitizeParagraphState(ParagraphStyleState style,
            string owner,
            bool vertical,
            List<string> issues,
            float baseFontSize)
        {
            if (style == null)
            {
                return;
            }

            if (vertical)
            {
                if (style.TextDirection.HasValue && style.TextDirection.Value != TextDirection.Ltr)
                {
                    issues.Add($"{owner}.textDirection={style.TextDirection.Value} defaulted to Ltr");
                    style.TextDirection = TextDirection.Ltr;
                }

                if (style.TextAlign.HasValue && !IsSupportedVerticalAlignment(style.TextAlign.Value))
                {
                    issues.Add($"{owner}.textAlign={style.TextAlign.Value} defaulted to Start");
                    style.TextAlign = TextAlign.Start;
                }

                if (style.TextIndent.HasValue && !FloatUtil.IsFinite(style.TextIndent.Value.Value))
                {
                    issues.Add($"{owner}.textIndent defaulted to 0");
                    style.TextIndent = TextIndent.Pixels(0f);
                }

                if (style.TextIndentEm.HasValue && !FloatUtil.IsFinite(style.TextIndentEm.Value))
                {
                    issues.Add($"{owner}.textIndentEm defaulted to 0");
                    style.TextIndentEm = 0f;
                }
                else if (style.TextIndentEm.HasValue &&
                         !IsResolvedEmIndentRepresentable(style, baseFontSize))
                {
                    issues.Add($"{owner}.textIndentEm={style.TextIndentEm.Value} resolved to an " +
                               "unrepresentable pixel indent; defaulted to 0");
                    style.TextIndentEm = 0f;
                }

                if (!TextBoxVerticalBlockLayout.IsValidMargin(style.MarginTop))
                {
                    issues.Add($"{owner}.marginTop defaulted to 0");
                    style.MarginTop = new MarginValue();
                }

                if (!TextBoxVerticalBlockLayout.IsValidMargin(style.MarginRight))
                {
                    issues.Add($"{owner}.marginRight defaulted to 0");
                    style.MarginRight = new MarginValue();
                }

                if (!TextBoxVerticalBlockLayout.IsValidMargin(style.MarginBottom))
                {
                    issues.Add($"{owner}.marginBottom defaulted to 0");
                    style.MarginBottom = new MarginValue();
                }

                if (!TextBoxVerticalBlockLayout.IsValidMargin(style.MarginLeft))
                {
                    issues.Add($"{owner}.marginLeft defaulted to 0");
                    style.MarginLeft = new MarginValue();
                }

                return;
            }

            if (style.TextDirection.HasValue &&
                !Enum.IsDefined(typeof(TextDirection), style.TextDirection.Value))
            {
                issues.Add($"{owner}.textDirection={style.TextDirection.Value} defaulted to inherited value");
                style.TextDirection = null;
            }

            if (style.TextAlign.HasValue &&
                !Enum.IsDefined(typeof(TextAlign), style.TextAlign.Value))
            {
                issues.Add($"{owner}.textAlign={style.TextAlign.Value} defaulted to inherited value");
                style.TextAlign = null;
            }

            if (style.TextIndent.HasValue && !FloatUtil.IsFinite(style.TextIndent.Value.Value))
            {
                issues.Add($"{owner}.textIndent defaulted to 0");
                style.TextIndent = TextIndent.Pixels(0f);
            }

            if (style.TextIndentEm.HasValue && !FloatUtil.IsFinite(style.TextIndentEm.Value))
            {
                issues.Add($"{owner}.textIndentEm defaulted to 0");
                style.TextIndentEm = 0f;
            }
        }

        private static void SanitizeTextState(TextStyleState style, bool vertical,
            List<string> issues)
        {
            if (style == null)
            {
                return;
            }

            if (style.FontSize.HasValue &&
                (!FloatUtil.IsFinite(style.FontSize.Value) || style.FontSize.Value <= 0f))
            {
                issues.Add($"inline.fontSize={style.FontSize.Value} defaulted to inherited value");
                style.FontSize = null;
            }

            if (style.FontSizeScale.HasValue &&
                (!FloatUtil.IsFinite(style.FontSizeScale.Value) || style.FontSizeScale.Value <= 0f))
            {
                issues.Add($"inline.fontSizeScale={style.FontSizeScale.Value} defaulted to 1");
                style.FontSizeScale = 1f;
            }

            if (style.LetterSpacing.HasValue &&
                (!FloatUtil.IsFinite(style.LetterSpacing.Value) ||
                 (vertical && style.LetterSpacing.Value < 0f)))
            {
                issues.Add($"inline.letterSpacing={style.LetterSpacing.Value} defaulted to 0");
                style.LetterSpacing = 0f;
            }

            if (vertical &&
                ((style.BaselineShift.HasValue && !FloatUtil.IsFinite(style.BaselineShift.Value)) ||
                 (style.BaselineShiftEm.HasValue && !FloatUtil.IsFinite(style.BaselineShiftEm.Value))))
            {
                issues.Add("inline baseline shift defaulted to 0");
                style.BaselineShift = 0f;
                style.BaselineShiftEm = 0f;
            }
            else
            {
                if (style.BaselineShift.HasValue && !FloatUtil.IsFinite(style.BaselineShift.Value))
                {
                    issues.Add($"inline.baselineShift={style.BaselineShift.Value} defaulted to 0");
                    style.BaselineShift = 0f;
                }

                if (style.BaselineShiftEm.HasValue && !FloatUtil.IsFinite(style.BaselineShiftEm.Value))
                {
                    issues.Add($"inline.baselineShiftEm={style.BaselineShiftEm.Value} defaulted to 0");
                    style.BaselineShiftEm = 0f;
                }
            }
        }

        private static ParagraphStyleSettings CloneParagraphStyle(ParagraphStyleSettings source)
        {
            return new ParagraphStyleSettings
            {
                textAlign = source.textAlign,
                textDirection = source.textDirection,
                writingMode = source.writingMode,
                singleLine = source.singleLine,
                ellipsisString = source.ellipsisString,
                paragraphSpacing = source.paragraphSpacing,
            };
        }

        private static TextStyleSettings CloneTextStyle(TextStyleSettings source)
        {
            return new TextStyleSettings
            {
                fontFamilies = new List<string>(source.fontFamilies),
                size = source.size,
                weight = source.weight,
                width = source.width,
                slant = source.slant,
                textColor = source.textColor,
                letterSpacing = source.letterSpacing,
                wordSpacing = source.wordSpacing,
                lineHeight = source.lineHeight,
                locale = source.locale,
            };
        }

        private static Margin CloneMargin(Margin source)
        {
            var clone = new Margin();
            clone.Left = source.Left;
            clone.Top = source.Top;
            clone.Right = source.Right;
            clone.Bottom = source.Bottom;
            return clone;
        }

        private static bool IsSupportedVerticalAlignment(TextAlign alignment)
        {
            return alignment == TextAlign.Start ||
                   alignment == TextAlign.End ||
                   alignment == TextAlign.Center ||
                   alignment == TextAlign.Justify;
        }

        private static NotSupportedException? CreateDiagnostic(List<string> issues)
        {
            return issues.Count == 0
                ? null
                : new NotSupportedException(
                    "Canvas TextBox configuration was defaulted for rendering: " +
                    string.Join("; ", issues) + ".");
        }

        internal static void ValidateProducer(TextBoxRenderTextureProducer producer)
        {
            if (!IsVertical(producer.paragraphStyle))
            {
                return;
            }

            if (producer.GetComponent<WorldSpaceTextBox>() != null)
            {
                throw new NotSupportedException(
                    "WorldSpaceTextBox does not support vertical-rl in the first vertical TextBox release.");
            }

        }

        internal static void ValidateSettings(TextBoxRenderTargetSettings settings)
        {
            if (!IsVertical(settings))
            {
                return;
            }

            var paragraphStyle = settings.ParagraphStyle;
            if (!FloatUtil.IsFinite(paragraphStyle.paragraphSpacing) ||
                paragraphStyle.paragraphSpacing < 0f)
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox requires finite nonnegative paragraph spacing.");
            }
            if (paragraphStyle.textDirection != TextDirection.Ltr)
            {
                throw new NotSupportedException("Vertical-rl Canvas TextBox requires LTR text direction.");
            }

            ValidateAlignment(paragraphStyle.textAlign, "component");
            ValidateTextStyle(settings.TextStyle);
        }

        internal static void ValidatePayload(ParagraphPayload payload, TextBoxRenderTargetSettings settings)
        {
            if (!IsVertical(settings))
            {
                return;
            }

            ValidateParagraphState(payload.ParagraphStyle, "payload");
            foreach (var block in payload.Blocks)
            {
                ValidateParagraphState(block.Style, "block");
                if (!TextBoxVerticalBlockLayout.IsValidMargin(block.Style.MarginTop) ||
                    !TextBoxVerticalBlockLayout.IsValidMargin(block.Style.MarginRight) ||
                    !TextBoxVerticalBlockLayout.IsValidMargin(block.Style.MarginBottom) ||
                    !TextBoxVerticalBlockLayout.IsValidMargin(block.Style.MarginLeft))
                {
                    throw new NotSupportedException(
                        "Vertical-rl Canvas TextBox requires finite nonnegative block margins.");
                }

                foreach (var segment in block.Segments)
                {
                    ValidateSegment(segment);
                }
            }
        }

        private static void ValidateParagraphState(ParagraphStyleState style, string owner)
        {
            if (style == null)
            {
                return;
            }

            if (style.TextDirection.HasValue && style.TextDirection.Value != TextDirection.Ltr)
            {
                throw new NotSupportedException(
                    $"Vertical-rl Canvas TextBox does not support RTL {owner} direction.");
            }

            if (style.TextAlign.HasValue)
            {
                ValidateAlignment(style.TextAlign.Value, owner);
            }

            if (style.TextIndent.HasValue && !FloatUtil.IsFinite(style.TextIndent.Value.Value))
            {
                throw new NotSupportedException(
                    $"Vertical-rl Canvas TextBox requires a finite {owner} text indent.");
            }

            if (style.TextIndentEm.HasValue && !FloatUtil.IsFinite(style.TextIndentEm.Value))
            {
                throw new NotSupportedException(
                    $"Vertical-rl Canvas TextBox requires a finite {owner} em text indent.");
            }
        }

        private static void ValidateAlignment(TextAlign alignment, string owner)
        {
            if (alignment != TextAlign.Start &&
                alignment != TextAlign.End &&
                alignment != TextAlign.Center &&
                alignment != TextAlign.Justify)
            {
                throw new NotSupportedException(
                    $"Vertical-rl Canvas TextBox supports only Start, End, Center, and Justify alignment ({owner} uses {alignment}).");
            }
        }

        private static void ValidateTextStyle(TextStyleSettings style)
        {
            if (style == null)
            {
                return;
            }

            if (!FloatUtil.IsFinite(style.size) || style.size <= 0f)
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox requires a finite positive font size.");
            }

            if (!FloatUtil.IsFinite(style.lineHeight))
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox requires a finite line height. " +
                    "Values <= 0 use natural metrics; positive values enable the column-height multiplier.");
            }

            if (!FloatUtil.IsFinite(style.letterSpacing) || style.letterSpacing < 0f)
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox requires finite non-negative letter spacing.");
            }

            if (!FloatUtil.IsFinite(style.wordSpacing) || style.wordSpacing < 0f)
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox requires finite non-negative word spacing.");
            }
        }

        private static void ValidateSegment(TextSegment segment)
        {
            var style = segment.TextStyle;
            if (style == null)
            {
                return;
            }

            if ((style.BaselineShift.HasValue && !FloatUtil.IsFinite(style.BaselineShift.Value)) ||
                (style.BaselineShiftEm.HasValue && !FloatUtil.IsFinite(style.BaselineShiftEm.Value)))
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox requires finite baseline shift.");
            }

            if (style.LetterSpacing.HasValue &&
                (!FloatUtil.IsFinite(style.LetterSpacing.Value) || style.LetterSpacing.Value < 0f))
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox requires finite non-negative letter spacing.");
            }

            var content = segment.Content ?? "";
            if (ContainsStrongRtl(content))
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox does not support strong RTL text content.");
            }
        }

        private static unsafe bool ContainsStrongRtl(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content ?? "");
            fixed (byte* text = bytes)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutContainsStrongRtl(text,
                    (ulong)bytes.Length,
                    out var containsStrongRtl));
                return containsStrongRtl != 0;
            }
        }

        internal static bool MarginsEqual(Margin? left, Margin? right)
        {
            return MarginValuesEqual(left?.Left ?? default, right?.Left ?? default) &&
                   MarginValuesEqual(left?.Top ?? default, right?.Top ?? default) &&
                   MarginValuesEqual(left?.Right ?? default, right?.Right ?? default) &&
                   MarginValuesEqual(left?.Bottom ?? default, right?.Bottom ?? default);
        }

        private static bool MarginValuesEqual(MarginValue left, MarginValue right)
        {
            return BitConverter.SingleToInt32Bits(left.Value) ==
                   BitConverter.SingleToInt32Bits(right.Value) &&
                   left.Unit == right.Unit &&
                   left.Auto == right.Auto;
        }
    }
}
