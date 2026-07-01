using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Milestro.Native
{
    public unsafe class UnmanagedStringArray : IDisposable
    {
        private NativeArray<IntPtr> stringPointers;
        private NativeArray<byte> utf8BytesArray;
        private bool disposed = false;

        public void** Ptr { get; private set; }
        public uint Length { get; private set; }

        public UnmanagedStringArray(List<string> strings)
        {
            Length = (uint)strings.Count;
            stringPointers = new NativeArray<IntPtr>(strings.Count, Allocator.Persistent);

            int totalBytes = 0;
            List<int> byteCounts = new List<int>(strings.Count);
            foreach (var str in strings)
            {
                int byteCount = System.Text.Encoding.UTF8.GetByteCount(str) + 1; // +1 for null terminator
                byteCounts.Add(byteCount);
                totalBytes += byteCount;
            }

            utf8BytesArray = new NativeArray<byte>(totalBytes, Allocator.Persistent);

            int offset = 0;
            for (int i = 0; i < strings.Count; i++)
            {
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(strings[i]);
                NativeArray<byte>.Copy(utf8Bytes, 0, utf8BytesArray, offset, utf8Bytes.Length);
                utf8BytesArray[offset + utf8Bytes.Length] = 0;
                stringPointers[i] = (IntPtr)utf8BytesArray.GetUnsafePtr() + offset;
                offset += byteCounts[i];
            }

            Ptr = (void**)stringPointers.GetUnsafePtr();
        }

        public void Dispose()
        {
            if (disposed) return;

            if (stringPointers.IsCreated)
            {
                stringPointers.Dispose();
            }

            if (utf8BytesArray.IsCreated)
            {
                utf8BytesArray.Dispose();
            }

            disposed = true;
        }
    }
}
