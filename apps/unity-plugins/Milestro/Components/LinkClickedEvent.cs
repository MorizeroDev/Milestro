using System;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace Milestro.Components
{
    [Serializable]
    [Preserve]
    public sealed class LinkClickedEvent : UnityEvent<LinkClickedEventArgs>
    {
    }
}
