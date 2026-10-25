using System;
using Milestro.Binding;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class ExitCodeUtilTests
    {
        private const ulong Magic = 0x5AUL;
        private const ulong NativeDomain = 1;
        private const ulong EnqueueOperation = 2;
        private const ulong QueueFullDetail = 2;
        private const ulong DeferredFlag = 1;
        private const ulong RetryableFlag = 2;
        private const ulong FailureSeverity = 2;

        [Test]
        public void RetryableQueueFullStatusPreservesRetrySignal()
        {
            var status = StructuredStatus(FailureSeverity, DeferredFlag | RetryableFlag);

            var exception = Assert.Throws<RetryableNativeException>(() => ExitCodeUtil.ThrowIfFailed(status));

            Assert.That(exception.Status, Is.EqualTo(status));
        }

        [Test]
        public void QueueFullWithoutRetryFlagRemainsAnOrdinaryFailure()
        {
            var status = StructuredStatus(FailureSeverity, DeferredFlag);

            var exception = Assert.Throws<Exception>(() => ExitCodeUtil.ThrowIfFailed(status));

            Assert.That(exception, Is.Not.InstanceOf<RetryableNativeException>());
        }

        private static long StructuredStatus(ulong severity, ulong flags)
        {
            var raw = (severity << 62) |
                      (flags << 59) |
                      (Magic << 51) |
                      (NativeDomain << 43) |
                      (EnqueueOperation << 32) |
                      QueueFullDetail;
            return unchecked((long)raw);
        }
    }
}
