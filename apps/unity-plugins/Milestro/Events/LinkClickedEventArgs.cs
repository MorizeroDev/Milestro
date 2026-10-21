using System;
using UnityEngine.Scripting;

namespace Milestro.Events
{
    [Serializable]
    [Preserve]
    public sealed class LinkClickedEventArgs
    {
        public LinkClickedEventArgs(string href, string id)
        {
            Href = href ?? throw new ArgumentNullException(nameof(href));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public string Href { get; }
        public string Id { get; }
    }
}
