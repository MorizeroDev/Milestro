using UnityEngine;

namespace Milestro.Util
{
    internal readonly struct ScrollDeltaConsumption
    {
        public ScrollDeltaConsumption(float nextOffset, float consumedDelta, float unusedDelta)
        {
            NextOffset = nextOffset;
            ConsumedDelta = consumedDelta;
            UnusedDelta = unusedDelta;
        }

        public float NextOffset { get; }

        public float ConsumedDelta { get; }

        public float UnusedDelta { get; }

        public bool Consumed => !Mathf.Approximately(ConsumedDelta, 0f);
    }
}
