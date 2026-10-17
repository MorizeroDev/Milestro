using UnityEngine;

namespace Milestro.Skia
{
    [DefaultExecutionOrder(-10000)]
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
