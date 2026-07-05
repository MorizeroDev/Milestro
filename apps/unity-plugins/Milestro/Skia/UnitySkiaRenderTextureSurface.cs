using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Milestro.Binding;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Milestro.Skia
{
    public sealed class UnitySkiaRenderTextureSurface : IDisposable
    {
        private enum RenderTextureHandleKind
        {
            RenderBuffer = 1,
            NativeTexture = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RenderTargetPayload
        {
            public int GraphicsBackend;
            public int HandleKind;
            public IntPtr ColorRenderBufferHandle;
            public IntPtr NativeTextureHandle;
            public int Width;
            public int Height;
            public int ColorSpace;
            public int StorageSrgb;
            public int ClearBeforeDraw;
            public int MsaaSamples;
            public int ResolveStrategy;
            public int PreferredFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DrawCommandPayload
        {
            public int Kind;
            public IntPtr Resource;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public float ClipX;
            public float ClipY;
            public float ClipWidth;
            public float ClipHeight;
            public int ResourceOwnership;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RenderSubmissionPayload
        {
            public RenderTargetPayload Target;
            public IntPtr Commands;
            public int CommandCount;
            public int Completed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RenderDrainPayload
        {
            public int Magic;
            public int GraphicsBackend;
            public int Completed;
        }

        internal sealed class PendingRenderEvent
        {
            public long Serial;
            public int GraphicsBackend;
            public IntPtr SubmissionPtr;
            public IntPtr CommandsPtr;
            public Texture? Texture;
            public object[] Resources = Array.Empty<object>();
            public IDisposable[] OwnedResources = Array.Empty<IDisposable>();
            public bool Reusable;
            public bool InUse;

            public void KeepAlive()
            {
                if (Texture != null)
                {
                    GC.KeepAlive(Texture);
                }

                foreach (var resource in Resources)
                {
                    if (resource != null)
                    {
                        GC.KeepAlive(resource);
                    }
                }
            }

            public void DisposeOwnedResources()
            {
                DisposeResources(OwnedResources);
            }
        }

        private readonly struct NativeOwnedResource
        {
            public NativeOwnedResource(IntPtr ptr, UnitySkiaRenderCommandList.ResourceOwnership ownership)
            {
                Ptr = ptr;
                Ownership = ownership;
            }

            public readonly IntPtr Ptr;
            public readonly UnitySkiaRenderCommandList.ResourceOwnership Ownership;
        }

        private sealed unsafe class SlimTextRenderSlot : IDisposable
        {
            private readonly CommandBuffer commandBuffer;
            private bool disposed;

            public readonly ReusableTextDrawSnapshot Snapshot;
            public readonly IntPtr SubmissionPtr;
            public readonly IntPtr CommandsPtr;
            public readonly PendingRenderEvent PendingEvent;

            public SlimTextRenderSlot(ReusableTextDrawSnapshot snapshot, int slotIndex)
            {
                Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
                SubmissionPtr = Marshal.AllocHGlobal(sizeof(RenderSubmissionPayload));
                CommandsPtr = Marshal.AllocHGlobal(sizeof(DrawCommandPayload));
                PendingEvent = new PendingRenderEvent
                {
                    SubmissionPtr = SubmissionPtr,
                    CommandsPtr = CommandsPtr,
                    Resources = new object[] { Snapshot },
                    OwnedResources = Array.Empty<IDisposable>(),
                    Reusable = true
                };
                commandBuffer = new CommandBuffer
                {
                    name = "Milestro Slim Text Native Plugin Pass " + slotIndex
                };
            }

            public bool InUse => PendingEvent.InUse;

            public void CopyTextFrom(ReusableTextDrawSnapshot source)
            {
                ThrowIfDisposed();
                Snapshot.CopyTextFrom(source);
            }

            public void WritePayload(RenderTargetPayload target, Vector2 baseline, bool drawText)
            {
                ThrowIfDisposed();
                var command = (DrawCommandPayload*)CommandsPtr;
                command->Kind = (int)UnitySkiaRenderCommandList.CommandKind.SlimText;
                command->Resource = Snapshot.NativePtr;
                command->X = baseline.x;
                command->Y = baseline.y;
                command->Width = 0f;
                command->Height = 0f;
                command->ClipX = 0f;
                command->ClipY = 0f;
                command->ClipWidth = 0f;
                command->ClipHeight = 0f;

                var submission = (RenderSubmissionPayload*)SubmissionPtr;
                submission->Target = target;
                submission->Commands = drawText ? CommandsPtr : IntPtr.Zero;
                submission->CommandCount = drawText ? 1 : 0;
                submission->Completed = 0;
            }

            public void Submit(IntPtr renderEventFunc, int renderEventId)
            {
                ThrowIfDisposed();
                commandBuffer.Clear();
                commandBuffer.IssuePluginEventAndData(renderEventFunc, renderEventId, SubmissionPtr);
                Graphics.ExecuteCommandBuffer(commandBuffer);
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                commandBuffer.Release();
                Snapshot.Dispose();
                if (CommandsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(CommandsPtr);
                }

                if (SubmissionPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(SubmissionPtr);
                }
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(SlimTextRenderSlot));
                }
            }
        }

        internal sealed class SlimTextNoAllocSubmission : IDisposable
        {
            private readonly ReusableTextDrawSnapshot stagingSnapshot;
            private readonly SlimTextRenderSlot[] slots;
            private bool retired;
            private bool disposed;

            public SlimTextNoAllocSubmission(Font font, int capacity, Color32 color, int slotCount = 3)
            {
                if (font == null)
                {
                    throw new ArgumentNullException(nameof(font));
                }

                if (capacity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(capacity));
                }

                slotCount = Math.Max(2, slotCount);
                Capacity = capacity;
                stagingSnapshot = new ReusableTextDrawSnapshot(font, capacity, color);
                slots = new SlimTextRenderSlot[slotCount];
                for (var i = 0; i < slots.Length; ++i)
                {
                    slots[i] = new SlimTextRenderSlot(new ReusableTextDrawSnapshot(font, capacity, color), i);
                }

                EnsurePendingEventCapacity(slotCount);
            }

            public int Capacity { get; }

            internal ReusableTextDrawSnapshot StagingSnapshot => stagingSnapshot;

            public void UpdateText(byte[] buffer, int offset, int length)
            {
                ThrowIfDisposed();
                stagingSnapshot.UpdateText(buffer, offset, length);
            }

            internal void CopyTextFrom(SlimTextNoAllocSubmission source)
            {
                ThrowIfDisposed();
                if (source == null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                stagingSnapshot.CopyTextFrom(source.stagingSnapshot);
            }

            public Rect MeasureBounds()
            {
                ThrowIfDisposed();
                return stagingSnapshot.MeasureBounds();
            }

            internal bool TryBeginRetire()
            {
                if (retired || disposed)
                {
                    return false;
                }

                retired = true;
                return true;
            }

            private SlimTextRenderSlot? TryAcquireSlot()
            {
                for (var i = 0; i < slots.Length; ++i)
                {
                    var slot = slots[i];
                    if (!slot.InUse)
                    {
                        return slot;
                    }
                }

                return null;
            }

            private bool TryPrepareSlot(RenderTargetPayload target,
                Vector2 baseline,
                bool drawText,
                out PendingRenderEvent pendingEvent)
            {
                ThrowIfDisposed();
                var slot = TryAcquireSlot();
                if (slot == null)
                {
                    pendingEvent = null!;
                    return false;
                }

                slot.CopyTextFrom(stagingSnapshot);
                slot.WritePayload(target, baseline, drawText);
                pendingEvent = slot.PendingEvent;
                return true;
            }

            private void SubmitPrepared(PendingRenderEvent pendingEvent, IntPtr renderEventFunc, int renderEventId)
            {
                for (var i = 0; i < slots.Length; ++i)
                {
                    var slot = slots[i];
                    if (slot.PendingEvent == pendingEvent)
                    {
                        slot.Submit(renderEventFunc, renderEventId);
                        return;
                    }
                }

                throw new InvalidOperationException("Milestro slim text render slot is not owned by this submission.");
            }

            internal bool TryPrepareAndSubmit(RenderTargetPayload target,
                Vector2 baseline,
                bool drawText,
                IntPtr renderEventFunc,
                int renderEventId,
                Texture texture)
            {
                if (!TryPrepareSlot(target, baseline, drawText, out var pendingEvent))
                {
                    return false;
                }

                try
                {
                    AddReusablePendingEvent(pendingEvent, texture);
                    SubmitPrepared(pendingEvent, renderEventFunc, renderEventId);
                }
                catch
                {
                    CancelPendingEvent(pendingEvent);
                    throw;
                }

                return true;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                stagingSnapshot.Dispose();
                for (var i = 0; i < slots.Length; ++i)
                {
                    slots[i].Dispose();
                }
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(SlimTextNoAllocSubmission));
                }
            }
        }

        private sealed class DeferredRelease
        {
            public long WaitForSerial;
            public Action Release;
        }

        private sealed class PendingRenderDrain
        {
            public int GraphicsBackend;
            public IntPtr DrainPtr;
            public IntPtr RenderEventFunc;
            public int RenderEventId;
        }

        private const int RenderDrainMagic = 0x4D524451; // MRDQ
        private static readonly object PendingLock = new object();
        private static readonly List<PendingRenderEvent> PendingEvents = new List<PendingRenderEvent>();
        private static readonly List<DeferredRelease> DeferredReleases = new List<DeferredRelease>();
        private static readonly Dictionary<int, PendingRenderDrain> PendingDrains =
            new Dictionary<int, PendingRenderDrain>();
        private static readonly int CompletedOffset =
            (int)Marshal.OffsetOf<RenderSubmissionPayload>(nameof(RenderSubmissionPayload.Completed));
        private static readonly int DrainCompletedOffset =
            (int)Marshal.OffsetOf<RenderDrainPayload>(nameof(RenderDrainPayload.Completed));
        private static long nextSerial;
        private static MilestroRenderEventLifetimePump lifetimePump;
#if UNITY_EDITOR
        private static bool editorLifetimePumpRegistered;
#endif

        private UnitySkiaRenderTextureDescriptor descriptor;
        private IntPtr renderEventFunc;
        private int renderEventId;
        private IntPtr d3d12ExternalTexture;
        private bool warnedMissingNativeTarget;
        private bool disposed;

        public UnitySkiaGraphicsBackend Backend { get; }
        public UnityEngine.ColorSpace ColorSpace => descriptor.ColorSpace;
        public bool UseSrgbStorage => descriptor.UseSrgbStorage;

        public Rect DisplayUvRect => DisplayUvRectForBackend(Backend);

        /// <summary>
        /// Assigned by Resize before construction returns, and null after Dispose or while rebuilding the target.
        /// </summary>
        public Texture? Texture { get; private set; }

        /// <summary>
        /// Null on Direct3D12, which uses an external Texture2D; non-null on Metal, Vulkan, OpenGL, and OpenGLES.
        /// </summary>
        public RenderTexture? RenderTexture { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend backend, int width, int height)
            : this(backend, new UnitySkiaRenderTextureDescriptor(width, height))
        {
        }

        public UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend backend, int width, int height, bool srgb)
            : this(backend, new UnitySkiaRenderTextureDescriptor(width, height, srgb))
        {
        }

        public UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend backend,
            int width,
            int height,
            UnityEngine.ColorSpace colorSpace)
            : this(backend, new UnitySkiaRenderTextureDescriptor(width, height, colorSpace))
        {
        }

        public UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend backend,
            UnitySkiaRenderTextureDescriptor descriptor)
        {
            Backend = backend;
            EnsureBackendSupported(backend);
            this.descriptor = NormalizeDescriptor(descriptor);
            renderEventFunc = BindingC.UnityRenderGetRenderEventAndDataFunc();
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderGetRenderTextureEventId((int)Backend, out renderEventId));
            Resize(this.descriptor.Width, this.descriptor.Height);
        }

        public void Resize(int width, int height)
        {
            ThrowIfDisposed();
            CollectCompletedEvents();

            descriptor = NormalizeDescriptor(new UnitySkiaRenderTextureDescriptor(width, height, descriptor.ColorSpace)
            {
                UseSrgbStorage = descriptor.UseSrgbStorage,
                ClearBeforeDraw = descriptor.ClearBeforeDraw,
                MsaaSamples = descriptor.MsaaSamples,
                ResolveStrategy = descriptor.ResolveStrategy,
                PreferredFormat = descriptor.PreferredFormat
            });

            if (HasUsableTexture() && Width == descriptor.Width && Height == descriptor.Height)
            {
                return;
            }

            RetireCurrentTexture();
            Width = descriptor.Width;
            Height = descriptor.Height;

            if (Backend == UnitySkiaGraphicsBackend.Direct3D12)
            {
                CreateD3D12Texture();
                return;
            }

            var renderTextureDescriptor = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = descriptor.UseSrgbStorage
            };
            RenderTexture = new RenderTexture(renderTextureDescriptor)
            {
                name = "Milestro " + Backend + " RenderTexture PoC"
            };
            ConfigureDisplayTexture(RenderTexture);
            RenderTexture.Create();
            Texture = RenderTexture;
        }

        /// <summary>
        /// Disposes a resource only after all render events queued before this call have completed.
        /// </summary>
        public void DisposeResourceAfterPendingDraws(IDisposable resource)
        {
            if (resource == null)
            {
                return;
            }

            DeferReleaseAfterCurrentEvents(resource.Dispose);
        }

        public void Submit(UnitySkiaRenderCommandList commands, bool? clearBeforeDraw = null)
        {
            TrySubmit(commands, clearBeforeDraw);
        }

        public bool TrySubmit(UnitySkiaRenderCommandList commands, bool? clearBeforeDraw = null)
        {
            ThrowIfDisposed();
            CollectCompletedEvents();

            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (!HasUsableTexture())
            {
                Resize(Width, Height);
            }

            if (!TryGetNativeTargetHandles(out var handleKind, out var colorRenderBufferHandle, out var nativeTextureHandle))
            {
                return false;
            }
            warnedMissingNativeTarget = false;

            var target = new RenderTargetPayload
            {
                GraphicsBackend = (int)Backend,
                HandleKind = (int)handleKind,
                ColorRenderBufferHandle = colorRenderBufferHandle,
                NativeTextureHandle = nativeTextureHandle,
                Width = Width,
                Height = Height,
                ColorSpace = (int)descriptor.ColorSpace,
                StorageSrgb = descriptor.UseSrgbStorage ? 1 : 0,
                ClearBeforeDraw = (clearBeforeDraw ?? descriptor.ClearBeforeDraw) ? 1 : 0,
                MsaaSamples = descriptor.MsaaSamples,
                ResolveStrategy = (int)descriptor.ResolveStrategy,
                PreferredFormat = (int)descriptor.PreferredFormat
            };

            var submissionPtr = IntPtr.Zero;
            var commandsPtr = IntPtr.Zero;
            object[] resources = Array.Empty<object>();
            IDisposable[] ownedResources = Array.Empty<IDisposable>();
            NativeOwnedResource[] nativeOwnedResources = Array.Empty<NativeOwnedResource>();
            PendingRenderEvent? pendingEvent = null;
            var enqueued = false;
            try
            {
                commandsPtr = MarshalCommands(commands, out resources, out ownedResources, out nativeOwnedResources);
                var submission = new RenderSubmissionPayload
                {
                    Target = target,
                    Commands = commandsPtr,
                    CommandCount = commands.Count,
                    Completed = 0
                };

                submissionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RenderSubmissionPayload>());
                Marshal.StructureToPtr(submission, submissionPtr, false);

                // HasUsableTexture and TryGetNativeTargetHandles above guarantee a live target texture here.
                pendingEvent = AddPendingEvent((int)Backend, submissionPtr, commandsPtr, Texture!, resources, ownedResources);
                ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderEnqueueSubmission((int)Backend, submissionPtr));
                enqueued = true;
                ScheduleRenderDrain(Backend, renderEventFunc, renderEventId);
            }
            catch
            {
                if (pendingEvent != null && !enqueued)
                {
                    CancelPendingEvent(pendingEvent);
                    DisposeNativeOwnedResources(nativeOwnedResources);
                }
                else if (pendingEvent == null)
                {
                    FreeSubmission(submissionPtr, commandsPtr);
                    KeepAliveResources(resources);
                    DisposeResources(ownedResources);
                    DisposeNativeOwnedResources(nativeOwnedResources);
                }
                throw;
            }

            return true;
        }

        internal bool TrySubmitSlimTextNoAlloc(SlimTextNoAllocSubmission submission,
            Vector2 baseline,
            bool drawText,
            bool? clearBeforeDraw = null)
        {
            ThrowIfDisposed();
            CollectCompletedEvents();

            if (submission == null)
            {
                throw new ArgumentNullException(nameof(submission));
            }

            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (!HasUsableTexture())
            {
                Resize(Width, Height);
            }

            if (!TryGetNativeTargetHandles(out var handleKind, out var colorRenderBufferHandle, out var nativeTextureHandle))
            {
                return false;
            }
            warnedMissingNativeTarget = false;

            var target = new RenderTargetPayload
            {
                GraphicsBackend = (int)Backend,
                HandleKind = (int)handleKind,
                ColorRenderBufferHandle = colorRenderBufferHandle,
                NativeTextureHandle = nativeTextureHandle,
                Width = Width,
                Height = Height,
                ColorSpace = (int)descriptor.ColorSpace,
                StorageSrgb = descriptor.UseSrgbStorage ? 1 : 0,
                ClearBeforeDraw = (clearBeforeDraw ?? descriptor.ClearBeforeDraw) ? 1 : 0,
                MsaaSamples = descriptor.MsaaSamples,
                ResolveStrategy = (int)descriptor.ResolveStrategy,
                PreferredFormat = (int)descriptor.PreferredFormat
            };

            return submission.TryPrepareAndSubmit(target, baseline, drawText, renderEventFunc, renderEventId, Texture!);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            RetireCurrentTexture();
            CollectCompletedEvents();
        }

        private static RenderTextureHandleKind HandleKindForBackend(UnitySkiaGraphicsBackend backend)
        {
            switch (backend)
            {
                case UnitySkiaGraphicsBackend.Metal:
                    return RenderTextureHandleKind.RenderBuffer;
                case UnitySkiaGraphicsBackend.Direct3D12:
                case UnitySkiaGraphicsBackend.Vulkan:
                case UnitySkiaGraphicsBackend.OpenGL:
                case UnitySkiaGraphicsBackend.OpenGLES:
                    return RenderTextureHandleKind.NativeTexture;
                default:
                    throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown Milestro Unity Skia RenderTexture backend.");
            }
        }

        private static IntPtr MarshalCommands(UnitySkiaRenderCommandList commandList,
            out object[] keepAliveResources,
            out IDisposable[] ownedResources,
            out NativeOwnedResource[] nativeOwnedResources)
        {
            keepAliveResources = Array.Empty<object>();
            ownedResources = Array.Empty<IDisposable>();
            nativeOwnedResources = Array.Empty<NativeOwnedResource>();
            if (commandList.Count == 0)
            {
                return IntPtr.Zero;
            }

            var commandSize = Marshal.SizeOf<DrawCommandPayload>();
            var commandsPtr = Marshal.AllocHGlobal(commandSize * commandList.Count);
            var keepAliveList = new List<object>(commandList.Count);
            var ownedList = new List<IDisposable>();
            var nativeOwnedList = new List<NativeOwnedResource>();
            try
            {
                var commands = commandList.Commands;
                for (var i = 0; i < commands.Count; ++i)
                {
                    var command = commands[i];
                    var resource = command.Resource;
                    var keepAlive = command.KeepAlive;
                    var ownership = command.Ownership;
                    if (command.SnapshotParagraph)
                    {
                        if (command.ParagraphSnapshotFactory == null)
                        {
                            throw new InvalidOperationException("Milestro paragraph draw command is missing its snapshot factory.");
                        }

                        var snapshot = command.ParagraphSnapshotFactory();
                        if (snapshot == null)
                        {
                            throw new InvalidOperationException("Milestro paragraph draw command snapshot factory returned null.");
                        }

                        resource = snapshot.DetachNativePtr();
                        ownership = UnitySkiaRenderCommandList.ResourceOwnership.Paragraph;
                        keepAlive = null;
                        nativeOwnedList.Add(new NativeOwnedResource(resource, ownership));
                    }
                    else if (command.SnapshotInputBox)
                    {
                        if (!(command.KeepAlive is TextLayout.InputBox inputBox))
                        {
                            throw new InvalidOperationException("Milestro InputBox draw command is missing its editor model.");
                        }

                        var snapshot = inputBox.CreateDrawSnapshot();
                        resource = snapshot.DetachNativePtr();
                        ownership = UnitySkiaRenderCommandList.ResourceOwnership.InputBoxSnapshot;
                        keepAlive = null;
                        nativeOwnedList.Add(new NativeOwnedResource(resource, ownership));
                    }
                    else if (command.SnapshotSlimText)
                    {
                        if (!(command.KeepAlive is Font font))
                        {
                            throw new InvalidOperationException("Milestro slim text draw command is missing its font.");
                        }

                        var snapshot = new TextDrawSnapshot(font, command.Text, command.Color);
                        resource = snapshot.NativePtr;
                        keepAlive = snapshot;
                        ownedList.Add(snapshot);
                    }

                    var payload = new DrawCommandPayload
                    {
                        Kind = (int)command.Kind,
                        Resource = resource,
                        X = command.X,
                        Y = command.Y,
                        Width = command.Width,
                        Height = command.Height,
                        ClipX = command.ClipX,
                        ClipY = command.ClipY,
                        ClipWidth = command.ClipWidth,
                        ClipHeight = command.ClipHeight,
                        ResourceOwnership = (int)ownership
                    };
                    Marshal.StructureToPtr(payload, IntPtr.Add(commandsPtr, i * commandSize), false);
                    if (keepAlive != null)
                    {
                        keepAliveList.Add(keepAlive);
                    }
                }

                keepAliveResources = keepAliveList.ToArray();
                ownedResources = ownedList.ToArray();
                nativeOwnedResources = nativeOwnedList.ToArray();
            }
            catch
            {
                try
                {
                    DisposeResources(ownedList);
                    DisposeNativeOwnedResources(nativeOwnedList);
                }
                finally
                {
                    Marshal.FreeHGlobal(commandsPtr);
                }
                throw;
            }

            return commandsPtr;
        }

        private static void KeepAliveResources(object[] resources)
        {
            if (resources == null)
            {
                return;
            }

            foreach (var resource in resources)
            {
                if (resource != null)
                {
                    GC.KeepAlive(resource);
                }
            }
        }

        private static void DisposeResources(IDisposable[] resources)
        {
            if (resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Length; ++i)
            {
                var resource = resources[i];
                resource?.Dispose();
            }
        }

        private static void DisposeNativeOwnedResources(IEnumerable<NativeOwnedResource> resources)
        {
            if (resources == null)
            {
                return;
            }

            foreach (var resource in resources)
            {
                var ptr = resource.Ptr;
                if (ptr == IntPtr.Zero)
                {
                    continue;
                }

                switch (resource.Ownership)
                {
                    case UnitySkiaRenderCommandList.ResourceOwnership.Paragraph:
                        ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphDestroy(out ptr));
                        break;
                    case UnitySkiaRenderCommandList.ResourceOwnership.InputBoxSnapshot:
                        ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDrawSnapshotDestroy(ref ptr));
                        break;
                }
            }
        }

        private static void DisposeResources(List<IDisposable> resources)
        {
            if (resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Count; ++i)
            {
                resources[i]?.Dispose();
            }
        }

        private static void FreeSubmission(IntPtr submissionPtr, IntPtr commandsPtr)
        {
            if (commandsPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(commandsPtr);
            }

            if (submissionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(submissionPtr);
            }
        }

        private static UnitySkiaRenderTextureDescriptor NormalizeDescriptor(UnitySkiaRenderTextureDescriptor descriptor)
        {
            descriptor.Width = Math.Max(1, descriptor.Width);
            descriptor.Height = Math.Max(1, descriptor.Height);
            descriptor.MsaaSamples = Math.Max(1, descriptor.MsaaSamples);
            if (descriptor.MsaaSamples != 1)
            {
                throw new NotSupportedException("Milestro Unity RenderTexture surface does not support MSAA yet.");
            }
            return descriptor;
        }

        private static Rect DisplayUvRectForBackend(UnitySkiaGraphicsBackend backend)
        {
            return new Rect(0f, 1f, 1f, -1f);
        }

        private bool HasUsableTexture()
        {
            return Texture != null && (RenderTexture == null || RenderTexture.IsCreated());
        }

        private static void ConfigureDisplayTexture(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
        }

        private bool TryGetNativeTargetHandles(out RenderTextureHandleKind handleKind,
            out IntPtr colorRenderBufferHandle,
            out IntPtr nativeTextureHandle)
        {
            handleKind = HandleKindForBackend(Backend);
            colorRenderBufferHandle = IntPtr.Zero;
            nativeTextureHandle = IntPtr.Zero;

            if (handleKind == RenderTextureHandleKind.RenderBuffer)
            {
                if (RenderTexture != null)
                {
                    colorRenderBufferHandle = RenderTexture.colorBuffer.GetNativeRenderBufferPtr();
                    if (colorRenderBufferHandle != IntPtr.Zero)
                    {
                        return true;
                    }
                }

                WarnMissingNativeTarget();
                return false;
            }

            if (Backend == UnitySkiaGraphicsBackend.Direct3D12 && d3d12ExternalTexture != IntPtr.Zero)
            {
                nativeTextureHandle = d3d12ExternalTexture;
            }
            else if (Texture != null)
            {
                nativeTextureHandle = Texture.GetNativeTexturePtr();
            }
            else
            {
                nativeTextureHandle = IntPtr.Zero;
            }
            if (nativeTextureHandle != IntPtr.Zero)
            {
                return true;
            }

            WarnMissingNativeTarget();
            return false;
        }

        private void WarnMissingNativeTarget()
        {
            if (warnedMissingNativeTarget)
            {
                return;
            }

            warnedMissingNativeTarget = true;
            Debug.LogWarning(
                "Milestro skipped a RenderTexture draw because Unity did not expose a native render target handle for the current RenderTexture yet.");
        }

        private void CreateD3D12Texture()
        {
            d3d12ExternalTexture = CreateD3D12ExternalTextureHandle(Width,
                Height,
                descriptor.UseSrgbStorage ? 1 : 0,
                (int)descriptor.PreferredFormat);
            if (d3d12ExternalTexture == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro D3D12 external texture creation returned null.");
            }

            try
            {
                Texture = Texture2D.CreateExternalTexture(Width,
                    Height,
                    TextureFormatForDescriptor(descriptor),
                    false,
                    !descriptor.UseSrgbStorage,
                    d3d12ExternalTexture);
                ConfigureDisplayTexture(Texture);
            }
            catch
            {
                var textureToRelease = d3d12ExternalTexture;
                d3d12ExternalTexture = IntPtr.Zero;
                DestroyD3D12ExternalTextureHandle(textureToRelease);
                throw;
            }

            if (Texture == null)
            {
                var textureToRelease = d3d12ExternalTexture;
                d3d12ExternalTexture = IntPtr.Zero;
                DestroyD3D12ExternalTextureHandle(textureToRelease);
                throw new InvalidOperationException("Unity failed to create Milestro D3D12 external texture.");
            }

            Texture.name = "Milestro " + Backend + " ExternalTexture PoC";
        }

        private static IntPtr CreateD3D12ExternalTextureHandle(int width, int height, int storageSrgb, int preferredFormat)
        {
            IntPtr texture;
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderCreateD3D12ExternalTexture(width,
                height,
                storageSrgb,
                preferredFormat,
                out texture));
            return texture;
        }

        private static void DestroyD3D12ExternalTextureHandle(IntPtr texture)
        {
            if (texture == IntPtr.Zero)
            {
                return;
            }

            var textureToRelease = texture;
            BindingC.UnityRenderDestroyD3D12ExternalTexture(ref textureToRelease);
        }

        private static TextureFormat TextureFormatForDescriptor(UnitySkiaRenderTextureDescriptor descriptor)
        {
            switch (descriptor.PreferredFormat)
            {
                case UnitySkiaRenderTextureFormat.Rgba32:
                    return TextureFormat.RGBA32;
                case UnitySkiaRenderTextureFormat.Auto:
                case UnitySkiaRenderTextureFormat.Bgra32:
                    return TextureFormat.BGRA32;
                default:
                    throw new ArgumentOutOfRangeException(nameof(descriptor.PreferredFormat),
                        descriptor.PreferredFormat,
                        "Unknown Milestro Unity Skia RenderTexture format.");
            }
        }

        private static void EnsureBackendSupported(UnitySkiaGraphicsBackend backend)
        {
            switch (backend)
            {
                case UnitySkiaGraphicsBackend.Metal:
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal)
                    {
                        throw new NotSupportedException("Milestro Unity Skia RenderTexture Metal backend requires Unity Metal.");
                    }
                    return;
                case UnitySkiaGraphicsBackend.Direct3D12:
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                    {
                        throw new NotSupportedException("Milestro Unity Skia RenderTexture Direct3D12 backend requires Unity D3D12.");
                    }
                    return;
                case UnitySkiaGraphicsBackend.OpenGL:
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore)
                    {
                        throw new NotSupportedException("Milestro Unity Skia RenderTexture OpenGL backend requires Unity OpenGLCore.");
                    }
                    return;
                case UnitySkiaGraphicsBackend.OpenGLES:
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3)
                    {
                        throw new NotSupportedException("Milestro Unity Skia RenderTexture OpenGLES backend requires Unity OpenGLES3.");
                    }
                    return;
                case UnitySkiaGraphicsBackend.Vulkan:
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
                    {
                        throw new NotSupportedException("Milestro Unity Skia RenderTexture Vulkan backend requires Unity Vulkan.");
                    }
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown Milestro Unity Skia RenderTexture backend.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(UnitySkiaRenderTextureSurface));
            }
        }

        private void RetireCurrentTexture()
        {
            var texture = Texture;
            var renderTexture = RenderTexture;
            var d3d12Texture = d3d12ExternalTexture;
            Texture = null;
            RenderTexture = null;
            d3d12ExternalTexture = IntPtr.Zero;

            if (d3d12Texture != IntPtr.Zero)
            {
                DeferReleaseAfterCurrentEvents(() => ReleaseD3D12Texture(texture, d3d12Texture));
            }
            else if (renderTexture != null)
            {
                DeferReleaseAfterCurrentEvents(() => ReleaseRenderTexture(renderTexture));
            }
        }

        private static PendingRenderEvent AddPendingEvent(int graphicsBackend,
            IntPtr submissionPtr,
            IntPtr commandsPtr,
            Texture texture,
            object[] resources,
            IDisposable[] ownedResources)
        {
            EnsureLifetimePump();
            lock (PendingLock)
            {
                var pendingEvent = new PendingRenderEvent
                {
                    Serial = ++nextSerial,
                    GraphicsBackend = graphicsBackend,
                    SubmissionPtr = submissionPtr,
                    CommandsPtr = commandsPtr,
                    Texture = texture,
                    Resources = resources,
                    OwnedResources = ownedResources
                };
                PendingEvents.Add(pendingEvent);
                return pendingEvent;
            }
        }

        private static void ScheduleRenderDrain(UnitySkiaGraphicsBackend backend,
            IntPtr renderEventFunc,
            int renderEventId)
        {
            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (renderEventId < 0)
            {
                throw new InvalidOperationException("Milestro Unity render event id is unavailable.");
            }

            PendingRenderDrain? pendingDrain = null;
            var graphicsBackend = (int)backend;
            lock (PendingLock)
            {
                if (PendingDrains.ContainsKey(graphicsBackend))
                {
                    return;
                }

                EnsureLifetimePump();
                var drain = new RenderDrainPayload
                {
                    Magic = RenderDrainMagic,
                    GraphicsBackend = graphicsBackend,
                    Completed = 0
                };
                var drainPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RenderDrainPayload>());
                Marshal.StructureToPtr(drain, drainPtr, false);
                pendingDrain = new PendingRenderDrain
                {
                    GraphicsBackend = graphicsBackend,
                    DrainPtr = drainPtr,
                    RenderEventFunc = renderEventFunc,
                    RenderEventId = renderEventId
                };
                PendingDrains.Add(graphicsBackend, pendingDrain);
            }

            try
            {
                IssueRenderDrain(pendingDrain);
            }
            catch
            {
                lock (PendingLock)
                {
                    if (PendingDrains.TryGetValue(graphicsBackend, out var current) && current == pendingDrain)
                    {
                        PendingDrains.Remove(graphicsBackend);
                    }
                }

                Marshal.FreeHGlobal(pendingDrain.DrainPtr);
                throw;
            }
        }

        private static void IssueRenderDrain(PendingRenderDrain pendingDrain)
        {
            CommandBuffer? cmd = null;
            try
            {
                cmd = new CommandBuffer();
                cmd.name = "Milestro Queued Native Render Drain";
                cmd.IssuePluginEventAndData(pendingDrain.RenderEventFunc,
                    pendingDrain.RenderEventId,
                    pendingDrain.DrainPtr);
                Graphics.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd?.Release();
            }
        }

        private static void AddReusablePendingEvent(PendingRenderEvent pendingEvent, Texture texture)
        {
            EnsureLifetimePump();
            lock (PendingLock)
            {
                if (pendingEvent.InUse)
                {
                    throw new InvalidOperationException("Milestro reusable render event slot is already pending.");
                }

                pendingEvent.Serial = ++nextSerial;
                pendingEvent.Texture = texture;
                pendingEvent.InUse = true;
                PendingEvents.Add(pendingEvent);
            }
        }

        private static void CancelPendingEvent(PendingRenderEvent pendingEvent)
        {
            var removed = false;
            lock (PendingLock)
            {
                removed = PendingEvents.Remove(pendingEvent);
            }

            if (removed)
            {
                pendingEvent.InUse = false;
                if (!pendingEvent.Reusable)
                {
                    FreeSubmission(pendingEvent.SubmissionPtr, pendingEvent.CommandsPtr);
                }

                pendingEvent.KeepAlive();
                pendingEvent.Texture = null;
                pendingEvent.DisposeOwnedResources();
            }
        }

        private static void DeferReleaseAfterCurrentEvents(Action release)
        {
            CollectCompletedEvents();

            var runImmediately = false;
            lock (PendingLock)
            {
                var waitForSerial = nextSerial;
                if (!HasPendingEventAtOrBefore(waitForSerial))
                {
                    runImmediately = true;
                }
                else
                {
                    EnsureLifetimePump();
                    DeferredReleases.Add(new DeferredRelease
                    {
                        WaitForSerial = waitForSerial,
                        Release = release
                    });
                }
            }

            if (runImmediately)
            {
                release();
            }
        }

        private static void CollectCompletedEvents()
        {
            List<Action>? releases = null;
            List<PendingRenderDrain>? completedDrains = null;
            List<PendingRenderDrain>? drainsToReschedule = null;

            lock (PendingLock)
            {
                for (var i = PendingEvents.Count - 1; i >= 0; i--)
                {
                    var pendingEvent = PendingEvents[i];
                    if (Marshal.ReadInt32(pendingEvent.SubmissionPtr, CompletedOffset) == 0)
                    {
                        continue;
                    }

                    pendingEvent.InUse = false;
                    if (!pendingEvent.Reusable)
                    {
                        FreeSubmission(pendingEvent.SubmissionPtr, pendingEvent.CommandsPtr);
                    }

                    pendingEvent.KeepAlive();
                    pendingEvent.Texture = null;
                    pendingEvent.DisposeOwnedResources();
                    PendingEvents.RemoveAt(i);
                }

                foreach (var pendingDrain in PendingDrains.Values)
                {
                    if (Marshal.ReadInt32(pendingDrain.DrainPtr, DrainCompletedOffset) == 0)
                    {
                        continue;
                    }

                    Marshal.FreeHGlobal(pendingDrain.DrainPtr);
                    if (completedDrains == null)
                    {
                        completedDrains = new List<PendingRenderDrain>();
                    }
                    completedDrains.Add(pendingDrain);
                    if (HasPendingEventForBackend(pendingDrain.GraphicsBackend))
                    {
                        if (drainsToReschedule == null)
                        {
                            drainsToReschedule = new List<PendingRenderDrain>();
                        }
                        drainsToReschedule.Add(pendingDrain);
                    }
                }

                if (completedDrains != null)
                {
                    foreach (var pendingDrain in completedDrains)
                    {
                        PendingDrains.Remove(pendingDrain.GraphicsBackend);
                    }
                }

                for (var i = DeferredReleases.Count - 1; i >= 0; i--)
                {
                    var deferredRelease = DeferredReleases[i];
                    if (HasPendingEventAtOrBefore(deferredRelease.WaitForSerial))
                    {
                        continue;
                    }

                    if (releases == null)
                    {
                        releases = new List<Action>();
                    }
                    releases.Add(deferredRelease.Release);
                    DeferredReleases.RemoveAt(i);
                }
            }

            if (drainsToReschedule != null)
            {
                foreach (var pendingDrain in drainsToReschedule)
                {
                    ScheduleRenderDrain((UnitySkiaGraphicsBackend)pendingDrain.GraphicsBackend,
                        pendingDrain.RenderEventFunc,
                        pendingDrain.RenderEventId);
                }
            }

            if (releases == null)
            {
                return;
            }

            foreach (var release in releases)
            {
                release();
            }
        }

        internal static void CollectCompletedEventsFromPump()
        {
            CollectCompletedEvents();
        }

#if UNITY_EDITOR
        private static void CollectCompletedEventsFromEditorPump()
        {
            CollectCompletedEvents();
            if (HasPendingLifetimeWork())
            {
                return;
            }

            EditorApplication.update -= CollectCompletedEventsFromEditorPump;
            editorLifetimePumpRegistered = false;
        }
#endif

        private static bool HasPendingEventAtOrBefore(long serial)
        {
            foreach (var pendingEvent in PendingEvents)
            {
                if (pendingEvent.Serial <= serial)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPendingLifetimeWork()
        {
            lock (PendingLock)
            {
                return PendingEvents.Count != 0 || PendingDrains.Count != 0 || DeferredReleases.Count != 0;
            }
        }

        private static bool HasPendingEventForBackend(int graphicsBackend)
        {
            foreach (var pendingEvent in PendingEvents)
            {
                if (pendingEvent.GraphicsBackend == graphicsBackend)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsurePendingEventCapacity(int additionalSlots)
        {
            lock (PendingLock)
            {
                var targetCapacity = PendingEvents.Capacity + Math.Max(0, additionalSlots);
                if (PendingEvents.Capacity < targetCapacity)
                {
                    PendingEvents.Capacity = targetCapacity;
                }
            }
        }

        private static void EnsureLifetimePump()
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (!editorLifetimePumpRegistered)
                {
                    EditorApplication.update += CollectCompletedEventsFromEditorPump;
                    editorLifetimePumpRegistered = true;
                }
                return;
            }
#endif

            if (lifetimePump != null || !Application.isPlaying)
            {
                return;
            }

            var gameObject = new GameObject("Milestro Render Event Lifetime Pump")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            lifetimePump = gameObject.AddComponent<MilestroRenderEventLifetimePump>();
        }

        private static void ReleaseRenderTexture(RenderTexture renderTexture)
        {
            renderTexture.Release();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(renderTexture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void ReleaseD3D12Texture(Texture? texture, IntPtr nativeTexture)
        {
            DestroyD3D12ExternalTextureHandle(nativeTexture);

            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
