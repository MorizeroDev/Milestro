using System;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace Milestro.Events
{
    [Serializable]
    [Preserve]
    public sealed class MilestroDropdownChangedEvent : UnityEvent<int>
    {
    }
}
