using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Milestro.Binding;
using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milestro.Skia
{
    public sealed class UnityMetalRenderTextureSurface : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RenderPayload
        {
            public IntPtr ColorRenderBufferHandle;
            public int Width;
            public int Height;
            public int Srgb;
            public int ClearBeforeDraw;

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
            public RenderTexture RenderTexture;
            public Paragraph Paragraph;
            public MilestroImage Image;

            public void KeepAlive()
            {
                GC.KeepAlive(RenderTexture);
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

        private readonly bool srgb;
        private IntPtr renderEventFunc;
        private int renderEventId;
        private bool disposed;

        public RenderTexture RenderTexture { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public UnityMetalRenderTextureSurface(int width, int height, bool srgb = true)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal)
            {
                throw new NotSupportedException("Milestro UnityMetalRenderTextureSurface requires Unity Metal backend.");
            }

            this.srgb = srgb;
            renderEventFunc = BindingC.UnityRenderGetRenderEventAndDataFunc();
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderGetMetalRenderEventId(out renderEventId));
            Resize(width, height);
        }

        public void Resize(int width, int height)
        {
            ThrowIfDisposed();
            CollectCompletedEvents();

            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (RenderTexture != null && Width == width && Height == height && RenderTexture.IsCreated())
            {
                return;
            }

            RetireCurrentRenderTexture();
            Width = width;
            Height = height;

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = srgb
            };
            RenderTexture = new RenderTexture(descriptor)
            {
                name = "Milestro Metal RenderTexture PoC"
            };
            RenderTexture.Create();
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
            bool clearBeforeDraw = true)
        {
            ThrowIfDisposed();
            CollectCompletedEvents();

            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (RenderTexture == null || !RenderTexture.IsCreated())
            {
                Resize(Width, Height);
            }

            var payload = new RenderPayload
            {
                ColorRenderBufferHandle = RenderTexture.colorBuffer.GetNativeRenderBufferPtr(),
                Width = Width,
                Height = Height,
                Srgb = srgb ? 1 : 0,
                ClearBeforeDraw = clearBeforeDraw ? 1 : 0,
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
                pendingEvent = AddPendingEvent(payloadPtr, RenderTexture, paragraph, image);

                CommandBuffer cmd = new CommandBuffer();
                cmd.name = "Native Plugin Pass";
                cmd.IssuePluginEventAndData(renderEventFunc, renderEventId, payloadPtr);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Release();
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
            RetireCurrentRenderTexture();
            CollectCompletedEvents();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(UnityMetalRenderTextureSurface));
            }
        }

        private void RetireCurrentRenderTexture()
        {
            var renderTexture = RenderTexture;
            RenderTexture = null;
            if (renderTexture != null)
            {
                DeferReleaseAfterCurrentEvents(() => ReleaseRenderTexture(renderTexture));
            }
        }

        private static PendingRenderEvent AddPendingEvent(IntPtr payloadPtr,
            RenderTexture renderTexture,
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
                    RenderTexture = renderTexture,
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
    }
}
