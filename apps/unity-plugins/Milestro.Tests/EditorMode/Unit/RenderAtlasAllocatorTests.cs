using System;
using System.Collections.Generic;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class RenderAtlasAllocatorTests
    {
        [Test]
        public void AllocationsIncludePaddingAndNeverOverlap()
        {
            var allocator = new RenderAtlasAllocator(64);
            var allocations = new List<RectInt>();
            while (allocator.TryAllocate(6, 6, out var allocation))
            {
                Assert.That(allocation.size, Is.EqualTo(new Vector2Int(8, 8)));
                foreach (var previous in allocations)
                {
                    Assert.That(previous.Overlaps(allocation), Is.False);
                }
                allocations.Add(allocation);
            }
            Assert.That(allocations.Count, Is.EqualTo(64));
        }

        [Test]
        public void ReleasingAllRegionsCoalescesWholePage()
        {
            var allocator = new RenderAtlasAllocator(64);
            var allocations = new List<RectInt>();
            while (allocator.TryAllocate(6, 6, out var allocation))
            {
                allocations.Add(allocation);
            }
            var random = new System.Random(1827);
            while (allocations.Count > 0)
            {
                var index = random.Next(allocations.Count);
                allocator.Release(allocations[index]);
                allocations.RemoveAt(index);
            }
            Assert.That(allocator.TryAllocate(62, 62, out var entirePage), Is.True);
            Assert.That(entirePage, Is.EqualTo(new RectInt(0, 0, 64, 64)));
        }

        [Test]
        public void ReusesFreedAllocationWithoutMovingLiveRegion()
        {
            var allocator = new RenderAtlasAllocator(32);
            Assert.That(allocator.TryAllocate(14, 14, out var first), Is.True);
            Assert.That(allocator.TryAllocate(14, 14, out var second), Is.True);
            allocator.Release(first);
            Assert.That(allocator.TryAllocate(14, 14, out var reused), Is.True);
            Assert.That(reused, Is.EqualTo(first));
            Assert.That(second.Overlaps(reused), Is.False);
            allocator.Release(second);
            Assert.Throws<InvalidOperationException>(() => allocator.Release(second));
        }

        [TestCase(0, 1)]
        [TestCase(1, -1)]
        [TestCase(63, 63)]
        [TestCase(int.MaxValue, 1)]
        public void InvalidOrOversizedRequestsDoNotConsumeSpace(int width, int height)
        {
            var allocator = new RenderAtlasAllocator(64);
            Assert.That(allocator.TryAllocate(width, height, out _), Is.False);
            Assert.That(allocator.TryAllocate(62, 62, out _), Is.True);
        }
    }
}
