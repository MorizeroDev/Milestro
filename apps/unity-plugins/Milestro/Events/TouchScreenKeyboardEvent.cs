using System;
using UnityEngine;
using UnityEngine.Events;

namespace Milestro.Events
{
    [Serializable]
    public class TouchScreenKeyboardEvent : UnityEvent<TouchScreenKeyboard.Status>
    {
    }
}
