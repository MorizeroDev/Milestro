using System;
using System.Collections.Generic;
using UnityEngine;

namespace Milestro.Skia
{
    internal sealed class RenderAtlasAllocator
    {
        internal const int Padding = 1;
        private readonly List<RectInt> free = new List<RectInt>();
        private readonly HashSet<RectInt> allocated = new HashSet<RectInt>();

        internal RenderAtlasAllocator(int size)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }
            free.Add(new RectInt(0, 0, size, size));
        }

        internal bool TryAllocate(int width, int height, out RectInt allocation)
        {
            allocation = default;
            if (width <= 0 || height <= 0 || width > int.MaxValue - Padding * 2 ||
                height > int.MaxValue - Padding * 2)
            {
                return false;
            }
            width += Padding * 2;
            height += Padding * 2;
            var bestIndex = -1;
            var bestArea = long.MaxValue;
            for (var index = 0; index < free.Count; ++index)
            {
                var candidate = free[index];
                var area = (long)candidate.width * candidate.height;
                if (candidate.width >= width && candidate.height >= height && area < bestArea)
                {
                    bestIndex = index;
                    bestArea = area;
                }
            }
            if (bestIndex < 0)
            {
                return false;
            }
            var available = free[bestIndex];
            free.RemoveAt(bestIndex);
            allocation = new RectInt(available.x, available.y, width, height);
            AddFree(new RectInt(available.x + width, available.y, available.width - width, height));
            AddFree(new RectInt(available.x, available.y + height, available.width, available.height - height));
            allocated.Add(allocation);
            return true;
        }

        internal void Release(RectInt allocation)
        {
            if (!allocated.Remove(allocation))
            {
                throw new InvalidOperationException("Atlas allocation is not live.");
            }
            for (var index = 0; index < free.Count; ++index)
            {
                var adjacent = free[index];
                if (allocation.y == adjacent.y && allocation.height == adjacent.height &&
                    (allocation.xMax == adjacent.x || adjacent.xMax == allocation.x))
                {
                    allocation = new RectInt(Math.Min(allocation.x, adjacent.x), allocation.y,
                        allocation.width + adjacent.width, allocation.height);
                }
                else if (allocation.x == adjacent.x && allocation.width == adjacent.width &&
                         (allocation.yMax == adjacent.y || adjacent.yMax == allocation.y))
                {
                    allocation = new RectInt(allocation.x, Math.Min(allocation.y, adjacent.y),
                        allocation.width, allocation.height + adjacent.height);
                }
                else
                {
                    continue;
                }
                free.RemoveAt(index);
                index = -1;
            }
            free.Add(allocation);
        }

        private void AddFree(RectInt rectangle)
        {
            if (rectangle.width > 0 && rectangle.height > 0)
            {
                free.Add(rectangle);
            }
        }
    }
}
