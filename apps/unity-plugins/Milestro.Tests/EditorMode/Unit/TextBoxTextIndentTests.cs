using System.Reflection;
using Milestro.Components.Internal;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextBoxTextIndentTests
    {
        [TestCase(TextOverflow.Clip, TextDirection.Ltr, false)]
        [TestCase(TextOverflow.Ellipsis, TextDirection.Ltr, false)]
        [TestCase(TextOverflow.Clip, TextDirection.Rtl, false)]
        [TestCase(TextOverflow.Ellipsis, TextDirection.Rtl, false)]
        [TestCase(TextOverflow.Clip, TextDirection.Ltr, true)]
        [TestCase(TextOverflow.Clip, TextDirection.Rtl, true)]
        public void NegativeIndentCanPaintIntoHorizontalMargins(TextOverflow overflow,
            TextDirection direction,
            bool flowSlice)
        {
            var margin = new Margin();
            margin.Left = new MarginValue { Value = 20f };
            margin.Right = new MarginValue { Value = 20f };
            margin.Top = new MarginValue { Value = 20f };
            margin.Bottom = new MarginValue { Value = 20f };
            var settings = new TextBoxRenderTargetSettings(
                "normal<p style=\"text-indent: -20px\">test</p>normal",
                margin,
                new ParagraphStyleSettings { textDirection = direction },
                new TextStyleSettings { size = 20f },
                TextBoxWrapMode.Wrap,
                overflow);
            var size = new Vector2Int(200, 200);
            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(size, default),
                ColorSpace.Gamma, settings, true, null), Is.True);
            var viewport = flowSlice
                ? TextBoxRenderViewport.FlowSlice(size, size, size, default, 0f)
                : TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            var arguments = new object[] { settings, viewport, size, size, null, Vector2.zero, default(Rect) };
            var commands = (UnitySkiaRenderCommandList)typeof(TextBoxRenderTarget)
                .GetMethod("BuildRenderCommands", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, arguments);

            Assert.That(commands.Count, Is.EqualTo(3));
            var command = commands.Commands[1];
            Assert.That(command.X, Is.EqualTo(20f));
            Assert.That(command.ClipX, Is.EqualTo(0f), "The negative first line must reach the left margin.");
            Assert.That(command.ClipX + command.ClipWidth, Is.EqualTo(200f),
                "RTL indentation must reach the right margin.");
            Assert.That(command.ClipY, Is.EqualTo(flowSlice ? 0f : 20f));
            Assert.That(target.ViewportSize.x, Is.EqualTo(160f));
        }
    }
}
