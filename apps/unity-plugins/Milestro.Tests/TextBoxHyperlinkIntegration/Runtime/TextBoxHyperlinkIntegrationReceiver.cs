using Milestro.Components;
using UnityEngine;
using UnityEngine.Scripting;

namespace Milestro.Tests.TextBoxHyperlinkIntegration
{
    [Preserve]
    public sealed class TextBoxHyperlinkIntegrationReceiver : MonoBehaviour
    {
        public int Count { get; private set; }
        public string LastHref { get; private set; } = string.Empty;
        public string LastId { get; private set; } = string.Empty;

        [Preserve]
        public void OnLinkClicked(LinkClickedEventArgs value)
        {
            ++Count;
            LastHref = value.Href;
            LastId = value.Id;
        }

        public void ResetRecords()
        {
            Count = 0;
            LastHref = string.Empty;
            LastId = string.Empty;
        }
    }
}
