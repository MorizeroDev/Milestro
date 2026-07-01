using UnityEngine;

namespace Milestro.Skia
{
    public sealed class MilestroRenderEventLifetimePump : MonoBehaviour
    {
        private void Update()
        {
            UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
        }

        private void LateUpdate()
        {
            UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
        }
    }
}
