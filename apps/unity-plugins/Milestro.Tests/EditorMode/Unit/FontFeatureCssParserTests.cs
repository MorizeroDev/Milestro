using System.Collections.Generic;
using System.Linq;
using Milestro.RichTextParser;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class FontFeatureCssParserTests
    {
        [TestCase("font")]
        [TestCase("span")]
        [TestCase("p")]
        public void ParsesFontFeaturesAndKerningOnSupportedElements(string tag)
        {
            var operations = ParseSingle($"<{tag} style=\"font-kerning: none; " +
                                         "font-feature-settings: 'liga' 0, &quot;tnum&quot; on\">text</" +
                                         tag + ">").FontFeatureOperations;

            AssertOperation(operations, 0, FontFeatureOperationType.Set, "kern", 0);
            AssertOperation(operations, 1, FontFeatureOperationType.Set, "liga", 0);
            AssertOperation(operations, 2, FontFeatureOperationType.Set, "tnum", 1);
            Assert.That(operations, Has.Count.EqualTo(3));
        }

        [Test]
        public void FeatureSettingsTakePrecedenceOverKerningWithinOneDeclarationBlock()
        {
            var operations = ParseSingle("<span style=\"font-feature-settings: 'kern' 1; " +
                                         "font-kerning: none\">text</span>").FontFeatureOperations;

            AssertOperation(operations, 0, FontFeatureOperationType.Set, "kern", 0);
            AssertOperation(operations, 1, FontFeatureOperationType.Set, "kern", 1);
        }

        [Test]
        public void NestedStylesPreserveResetAndRemovalOperations()
        {
            var payload = Parse("<span style=\"font-feature-settings: 'liga' 0; font-kerning: none\">one" +
                                "<span style=\"font-feature-settings: normal\">two" +
                                "<span style=\"font-kerning: auto\">three</span></span></span>");

            var first = payload.Body[0].TextStyle.FontFeatureOperations;
            var second = payload.Body[1].TextStyle.FontFeatureOperations;
            var third = payload.Body[2].TextStyle.FontFeatureOperations;
            Assert.That(second.Take(2).ToArray(), Is.EqualTo(first.ToArray()));
            AssertOperation(second, 2, FontFeatureOperationType.ResetAll);
            Assert.That(third.Take(3).ToArray(), Is.EqualTo(second.ToArray()));
            AssertOperation(third, 3, FontFeatureOperationType.Remove, "kern");
        }

        [TestCase("font-feature-settings: normal, 'liga' 0")]
        [TestCase("font-feature-settings: liga 0")]
        [TestCase("font-feature-settings: 'liga' -1")]
        [TestCase("font-feature-settings: 'toolong' 1")]
        [TestCase("font-feature-settings: 'liga' 0,")]
        [TestCase("font-feature-settings: 'liga 0")]
        [TestCase("font-kerning: invalid")]
        public void InvalidDeclarationsAreIgnored(string declarations)
        {
            var state = ParseSingle($"<span style=\"{declarations}\">text</span>");

            Assert.That(state.FontFeatureOperations, Is.Empty);
        }

        [Test]
        public void UnsupportedPropertiesAndLaterInvalidValuesDoNotClearValidOverrides()
        {
            var state = ParseSingle("<span style=\"color: red; font-kerning: none; " +
                                    "font-kerning: invalid\">text</span>");

            Assert.That(state.FontFeatureOperations, Has.Count.EqualTo(1));
            AssertOperation(state.FontFeatureOperations, 0, FontFeatureOperationType.Set, "kern", 0);
        }

        private static TextStyleState ParseSingle(string markup)
        {
            var payload = Parse(markup);
            Assert.That(payload.Body, Has.Count.EqualTo(1));
            return payload.Body[0].TextStyle;
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }

        private static void AssertOperation(IReadOnlyList<FontFeatureOperation> operations,
                                            int index,
                                            FontFeatureOperationType type,
                                            string? feature = null,
                                            int value = 0)
        {
            Assert.That(operations[index].Type, Is.EqualTo(type));
            Assert.That(operations[index].Feature, Is.EqualTo(feature));
            Assert.That(operations[index].Value, Is.EqualTo(value));
        }
    }
}
