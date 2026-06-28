using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Milestro.Binding;
using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.Rendering;

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
        private struct RenderPayload
        {
            public int GraphicsBackend;
            public int HandleKind;
            public IntPtr ColorRenderBufferHandle;
            public IntPtr NativeTextureHandle;
            public int Width;
            public int Height;
            public int Srgb;
            public int ClearBeforeDraw;
            public int MsaaSamples;
            public int ResolveStrategy;
            public int PreferredFormat;

            public IntPtr Paragraph;
            public float ParagraphX;
            public float ParagraphY;

            public IntPtr Image;
            public float ImageX;
            public float ImageY;
            public float ImageWidth;
            public float ImageHeight;

            public int Completed;
        }

        private sealed class PendingRenderEvent
        {
            public long Serial;
            public IntPtr PayloadPtr;
            public Texture Texture;
            public Paragraph Paragraph;
            public MilestroImage Image;

            public void KeepAlive()
            {
                GC.KeepAlive(Texture);
                GC.KeepAlive(Paragraph);
                GC.KeepAlive(Image);
            }
        }

        private sealed class DeferredRelease
        {
            public long WaitForSerial;
            public Action Release;
        }

        private static readonly object PendingLock = new object();
        private static readonly List<PendingRenderEvent> PendingEvents = new List<PendingRenderEvent>();
        private static readonly List<DeferredRelease> DeferredReleases = new List<DeferredRelease>();
        private static readonly int CompletedOffset = (int)Marshal.OffsetOf<RenderPayload>(nameof(RenderPayload.Completed));
        private static long nextSerial;
        private static MilestroRenderEventLifetimePump lifetimePump;

        private UnitySkiaRenderTextureDescriptor descriptor;
        private IntPtr renderEventFunc;
        private int renderEventId;
        private IntPtr d3d12ExternalTexture;
        private bool disposed;

        public UnitySkiaGraphicsBackend Backend { get; }
        public bool Srgb => descriptor.Srgb;
        public Texture Texture { get; private set; }
        public RenderTexture RenderTexture { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend backend,
            int width,
            int height,
            bool srgb = true)
            : this(backend, new UnitySkiaRenderTextureDescriptor(width, height, srgb))
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

            descriptor = NormalizeDescriptor(new UnitySkiaRenderTextureDescriptor(width, height, descriptor.Srgb)
            {
                ClearBeforeDraw = descriptor.ClearBeforeDraw,
                MsaaSamples = descriptor.MsaaSamples,
                ResolveStrategy = descriptor.ResolveStrategy,
                PreferredFormat = descriptor.PreferredFormat
            });

            if (Texture != null && Width == descriptor.Width && Height == descriptor.Height)
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
                sRGB = descriptor.Srgb
            };
            RenderTexture = new RenderTexture(renderTextureDescriptor)
            {
                name = "Milestro " + Backend + " RenderTexture PoC"
            };
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

        public void Draw(Paragraph paragraph,
            MilestroImage image,
            Vector2 paragraphPosition,
            Rect imageRect,
            bool? clearBeforeDraw = null)
        {
            ThrowIfDisposed();
            CollectCompletedEvents();

            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (Texture == null || (RenderTexture != null && !RenderTexture.IsCreated()))
            {
                Resize(Width, Height);
            }

            var payload = new RenderPayload
            {
                GraphicsBackend = (int)Backend,
                HandleKind = Backend == UnitySkiaGraphicsBackend.Direct3D12
                    ? (int)RenderTextureHandleKind.NativeTexture
                    : (int)RenderTextureHandleKind.RenderBuffer,
                ColorRenderBufferHandle = RenderTexture != null
                    ? RenderTexture.colorBuffer.GetNativeRenderBufferPtr()
                    : IntPtr.Zero,
                NativeTextureHandle = Backend == UnitySkiaGraphicsBackend.Direct3D12 && d3d12ExternalTexture != IntPtr.Zero
                    ? d3d12ExternalTexture
                    : Texture.GetNativeTexturePtr(),
                Width = Width,
                Height = Height,
                Srgb = descriptor.Srgb ? 1 : 0,
                ClearBeforeDraw = (clearBeforeDraw ?? descriptor.ClearBeforeDraw) ? 1 : 0,
                MsaaSamples = descriptor.MsaaSamples,
                ResolveStrategy = (int)descriptor.ResolveStrategy,
                PreferredFormat = (int)descriptor.PreferredFormat,
                Paragraph = paragraph?.Ptr ?? IntPtr.Zero,
                ParagraphX = paragraphPosition.x,
                ParagraphY = paragraphPosition.y,
                Image = image?.Ptr ?? IntPtr.Zero,
                ImageX = imageRect.x,
                ImageY = imageRect.y,
                ImageWidth = imageRect.width,
                ImageHeight = imageRect.height,
                Completed = 0
            };

            var payloadPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RenderPayload>());
            PendingRenderEvent pendingEvent = null;
            try
            {
                Marshal.StructureToPtr(payload, payloadPtr, false);
                pendingEvent = AddPendingEvent(payloadPtr, Texture, paragraph, image);

                CommandBuffer cmd = null;
                try
                {
                    cmd = new CommandBuffer();
                    cmd.name = "Milestro " + Backend + " Native Plugin Pass";
                    cmd.IssuePluginEventAndData(renderEventFunc, renderEventId, payloadPtr);
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                finally
                {
                    cmd?.Release();
                }
            }
            catch
            {
                if (pendingEvent != null)
                {
                    CancelPendingEvent(pendingEvent);
                }
                else
                {
                    Marshal.FreeHGlobal(payloadPtr);
                }
                throw;
            }
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

        private void CreateD3D12Texture()
        {
            d3d12ExternalTexture = CreateD3D12ExternalTextureHandle(Width,
                Height,
                descriptor.Srgb ? 1 : 0,
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
                    !descriptor.Srgb,
                    d3d12ExternalTexture);
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

        private static IntPtr CreateD3D12ExternalTextureHandle(int width, int height, int srgb, int preferredFormat)
        {
            IntPtr texture;
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderCreateD3D12ExternalTexture(width,
                height,
                srgb,
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
                case UnitySkiaGraphicsBackend.Vulkan:
                case UnitySkiaGraphicsBackend.OpenGL:
                case UnitySkiaGraphicsBackend.OpenGLES:
                    throw new NotSupportedException("Milestro Unity Skia RenderTexture backend is reserved but not implemented: " + backend);
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

        private static PendingRenderEvent AddPendingEvent(IntPtr payloadPtr,
            Texture texture,
            Paragraph paragraph,
            MilestroImage image)
        {
            EnsureLifetimePump();
            lock (PendingLock)
            {
                var pendingEvent = new PendingRenderEvent
                {
                    Serial = ++nextSerial,
                    PayloadPtr = payloadPtr,
                    Texture = texture,
                    Paragraph = paragraph,
                    Image = image
                };
                PendingEvents.Add(pendingEvent);
                return pendingEvent;
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
                Marshal.FreeHGlobal(pendingEvent.PayloadPtr);
                pendingEvent.KeepAlive();
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
            List<Action> releases = null;

            lock (PendingLock)
            {
                for (var i = PendingEvents.Count - 1; i >= 0; i--)
                {
                    var pendingEvent = PendingEvents[i];
                    if (Marshal.ReadInt32(pendingEvent.PayloadPtr, CompletedOffset) == 0)
                    {
                        continue;
                    }

                    Marshal.FreeHGlobal(pendingEvent.PayloadPtr);
                    pendingEvent.KeepAlive();
                    PendingEvents.RemoveAt(i);
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

        private static void EnsureLifetimePump()
        {
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

        private static void ReleaseD3D12Texture(Texture texture, IntPtr nativeTexture)
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
