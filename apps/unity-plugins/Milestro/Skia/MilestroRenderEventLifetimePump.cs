using UnityEngine;

namespace Milestro.Skia
{
    public sealed class MilestroRenderEventLifetimePump : MonoBehaviour
    {
        private void Update()
        {
            UnityMetalRenderTextureSurface.CollectCompletedEventsFromPump();
        }

        private void LateUpdate()
        {
            UnityMetalRenderTextureSurface.CollectCompletedEventsFromPump();
        }
    }
}
