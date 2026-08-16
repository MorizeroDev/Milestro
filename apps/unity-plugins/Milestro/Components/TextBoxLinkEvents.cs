using System;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace Milestro.Components
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

    [Serializable]
    [Preserve]
    public sealed class LinkClickedEvent : UnityEvent<LinkClickedEventArgs>
    {
    }
}
