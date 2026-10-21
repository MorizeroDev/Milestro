using System;
using UnityEngine.Scripting;

namespace Milestro.Events
{
    [Serializable]
    [Preserve]
    public sealed class LinkHoverEventArgs
    {
        public LinkHoverEventArgs(string href, string id, bool hovered)
        {
            Href = href ?? throw new ArgumentNullException(nameof(href));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Hovered = hovered;
        }

        public string Href { get; }
        public string Id { get; }
        public bool Hovered { get; }
    }
}
