using System;

namespace Milestro.Binding
{
    public static class ExitCodeUtil
    {
        internal static void ThrowIfFailed(long status)
        {
            if (status < 0)
            {
                throw new Exception();
            }
        }
    }
}
