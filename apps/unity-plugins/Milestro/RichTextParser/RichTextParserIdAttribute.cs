using UnityEngine;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// Draws a string field as a rich text parser id: a free-form text field for custom
    /// ids plus a dropdown listing the parsers registered in <see cref="RichTextParserService"/>.
    /// </summary>
    public sealed class RichTextParserIdAttribute : PropertyAttribute
    {
    }
}
