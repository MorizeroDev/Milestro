using System;
using System.Collections.Generic;
using Milestro.Configuration;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using SubmissionStatus = Milestro.Skia.UnitySkiaRenderTextureSurface.RenderSubmissionStatus;

namespace Milestro.Skia
{
    internal static class RenderTextureAtlas
    {
        private static readonly List<Page> Pages = new List<Page>();
        private static readonly List<Page> FlushPages = new List<Page>();
        private static bool registered;
        private static bool flushing;
        internal static long SubmissionCount { get; private set; }
        internal static int PageCount => Pages.Count;

        internal static bool TryAllocate(RenderSurfaceCandidate candidate,
            ColorSpace colorSpace,
            RenderSurfaceConfiguration configuration,
            ulong epoch,
            out Lease? lease)
        {
            lease = null;
            var size = configuration.AtlasPageSize;
            if (!configuration.EnableAtlas || size < 4 || size > SystemInfo.maxTextureSize ||
                size > configuration.ConservativeMaxTextureEdge ||
                (long)size * size > configuration.MaxPixelsPerSurface ||
                (long)size * size * 4 > configuration.MaxBytesPerSurface ||
                candidate.RasterWidth > size - RenderAtlasAllocator.Padding * 2 ||
                candidate.RasterHeight > size - RenderAtlasAllocator.Padding * 2)
            {
                return false;
            }
            var fingerprint = RenderSurfaceConfigurationFingerprint.Compute(configuration);
            foreach (var page in Pages)
            {
                if (page.Matches(colorSpace, epoch, fingerprint) && page.TryAllocate(candidate, out lease))
                {
                    return true;
                }
            }
            var pixels = (long)size * size;
            var pageCandidate = new RenderSurfaceCandidate(size, size, size, size, 1f, pixels, pixels * 4);
            if (!UnityAutoRenderTextureSurface.TryCreate(pageCandidate, colorSpace, configuration,
                    out var surface, out _))
            {
                return false;
            }
            var created = new Page(surface!, size, fingerprint);
            Pages.Add(created);
            Register();
            return created.TryAllocate(candidate, out lease);
        }

        internal static void Flush()
        {
            if (flushing || Pages.Count == 0)
            {
                return;
            }
            flushing = true;
            FlushPages.AddRange(Pages);
            try
            {
                foreach (var page in FlushPages)
                {
                    page.Flush();
                }
            }
            finally
            {
                FlushPages.Clear();
                flushing = false;
            }
        }

        private static void Register()
        {
            if (registered)
            {
                return;
            }
            registered = true;
            UnityEngine.Canvas.willRenderCanvases += Flush;
            Camera.onPreCull += BeforeCamera;
            UnityEngine.Rendering.RenderPipelineManager.beginContextRendering += BeforeRenderPipeline;
#if UNITY_EDITOR
            EditorApplication.update += EditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
            Application.quitting += Shutdown;
        }

        private static void BeforeCamera(Camera camera) => Flush();

        private static void BeforeRenderPipeline(UnityEngine.Rendering.ScriptableRenderContext context,
            List<Camera> cameras) => Flush();

#if UNITY_EDITOR
        private static void EditorUpdate()
        {
            if (!Application.isPlaying)
            {
                Flush();
            }
        }
#endif

        private static void Shutdown()
        {
            UnityEngine.Canvas.willRenderCanvases -= Flush;
            Camera.onPreCull -= BeforeCamera;
            UnityEngine.Rendering.RenderPipelineManager.beginContextRendering -= BeforeRenderPipeline;
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
            Application.quitting -= Shutdown;
            registered = false;
            foreach (var page in Pages.ToArray())
            {
                page.Shutdown();
            }
        }

        internal sealed class Lease : IDisposable
        {
            private readonly Page page;
            private readonly List<IDisposable> deferredResources = new List<IDisposable>();
            private bool disposed;
            internal bool Pending { get; private set; }
            internal RectInt Allocation { get; }
            internal RectInt Content { get; }
            internal float Scale { get; }
            internal Texture? Texture => page.Surface.Texture;
            internal ColorSpace ColorSpace => page.Surface.ColorSpace;
            internal UnitySkiaGraphicsBackend Backend => page.Surface.Backend;
            internal ulong Epoch => page.Epoch;
            internal bool IsLive => !disposed && !page.Disposed;
            internal event Action<SubmissionStatus>? Completed;

            internal Rect DisplayUvRect => new Rect(
                (float)Content.x / page.Size,
                (float)Content.yMax / page.Size,
                (float)Content.width / page.Size,
                -(float)Content.height / page.Size);

            internal Lease(Page page, RectInt allocation, RenderSurfaceCandidate candidate)
            {
                this.page = page;
                Allocation = allocation;
                Content = new RectInt(allocation.x + RenderAtlasAllocator.Padding,
                    allocation.y + RenderAtlasAllocator.Padding, candidate.RasterWidth, candidate.RasterHeight);
                Scale = candidate.EffectiveScale;
            }

            internal bool Matches(RenderSurfaceCandidate candidate, ColorSpace colorSpace,
                ulong epoch, RenderSurfaceConfiguration configuration)
            {
                return IsLive && Content.width == candidate.RasterWidth && Content.height == candidate.RasterHeight &&
                       Scale == candidate.EffectiveScale &&
                       page.Matches(colorSpace, epoch, RenderSurfaceConfigurationFingerprint.Compute(configuration));
            }

            internal bool TrySubmit(UnitySkiaRenderCommandList commands)
            {
                if (Pending)
                {
                    UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                }
                if (!IsLive || Pending || Epoch != UnityAutoRenderTextureSurface.ReadCurrentDeviceEpoch())
                {
                    return false;
                }
                var captured = commands.Capture();
                Pending = true;
                page.Enqueue(this, captured);
                return true;
            }

            internal void DisposeResourceAfterPendingDraws(IDisposable resource)
            {
                if (Pending)
                {
                    deferredResources.Add(resource);
                }
                else
                {
                    page.Surface.DisposeResourceAfterPendingDraws(resource);
                }
            }

            internal void Notify(SubmissionStatus status)
            {
                Pending = false;
                foreach (var resource in deferredResources)
                {
                    resource.Dispose();
                }
                deferredResources.Clear();
                if (disposed)
                {
                    page.Release(this);
                }
                else
                {
                    Completed?.Invoke(status);
                }
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                if (!Pending)
                {
                    page.Release(this);
                }
            }
        }

        internal sealed class Page
        {
            private readonly RenderAtlasAllocator allocator;
            private readonly ulong fingerprint;
            private readonly List<Update> pending = new List<Update>();
            private List<Update> inFlight = new List<Update>();
            private int leases;
            private bool initialized;
            internal UnityAutoRenderTextureSurface Surface { get; }
            internal int Size { get; }
            internal ulong Epoch { get; }
            internal bool Disposed { get; private set; }

            internal Page(UnityAutoRenderTextureSurface surface, int size, ulong fingerprint)
            {
                Surface = surface;
                Size = size;
                Epoch = surface.DeviceEpoch;
                this.fingerprint = fingerprint;
                allocator = new RenderAtlasAllocator(size);
                surface.RenderEventCompleted += Complete;
            }

            internal bool Matches(ColorSpace colorSpace, ulong epoch, ulong configurationFingerprint)
            {
                return !Disposed && Surface.ColorSpace == colorSpace && Epoch == epoch &&
                       fingerprint == configurationFingerprint;
            }

            internal bool TryAllocate(RenderSurfaceCandidate candidate, out Lease? lease)
            {
                lease = null;
                if (!allocator.TryAllocate(candidate.RasterWidth, candidate.RasterHeight, out var allocation))
                {
                    return false;
                }
                ++leases;
                lease = new Lease(this, allocation, candidate);
                return true;
            }

            internal void Enqueue(Lease lease, UnitySkiaRenderCommandList.CapturedCommands captured)
            {
                pending.Add(new Update(lease, captured));
            }

            internal void Flush()
            {
                if (Disposed || pending.Count == 0 || inFlight.Count != 0)
                {
                    return;
                }
                inFlight.AddRange(pending);
                pending.Clear();
                var commands = new UnitySkiaRenderCommandList();
                foreach (var update in inFlight)
                {
                    commands.AppendRegion(update.Commands, update.Lease.Allocation,
                        update.Lease.Content, update.Lease.Scale);
                }
                try
                {
                    if (Epoch != UnityAutoRenderTextureSurface.ReadCurrentDeviceEpoch() ||
                        !Surface.TrySubmit(commands, !initialized))
                    {
                        Complete(SubmissionStatus.Failed);
                        return;
                    }
                    ++SubmissionCount;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    Surface.DisposeResourceAfterPendingDraws(new PendingFailure(this, inFlight));
                }
            }

            private void Complete(SubmissionStatus status)
            {
                if (inFlight.Count == 0)
                {
                    return;
                }
                if (status == SubmissionStatus.Drawn)
                {
                    initialized = true;
                }
                var completed = inFlight;
                inFlight = new List<Update>();
                FinishUpdates(completed, status);
            }

            private static void FinishUpdates(List<Update> completed, SubmissionStatus status)
            {
                foreach (var update in completed)
                {
                    try
                    {
                        update.Commands.Dispose();
                        update.Lease.Notify(status);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }

            internal void Release(Lease lease)
            {
                allocator.Release(lease.Allocation);
                if (--leases == 0)
                {
                    Disposed = true;
                    Pages.Remove(this);
                    Surface.RenderEventCompleted -= Complete;
                    Surface.Dispose();
                }
            }

            internal void Shutdown()
            {
                Disposed = true;
                Pages.Remove(this);
                Surface.RenderEventCompleted -= Complete;
                FinishUpdates(pending, SubmissionStatus.Failed);
                pending.Clear();
                Surface.DisposeResourceAfterPendingDraws(new PendingFailure(this, inFlight));
                Surface.Dispose();
            }

            private sealed class PendingFailure : IDisposable
            {
                private readonly Page page;
                private readonly List<Update> updates;

                internal PendingFailure(Page page, List<Update> updates)
                {
                    this.page = page;
                    this.updates = updates;
                }

                public void Dispose()
                {
                    if (ReferenceEquals(page.inFlight, updates))
                    {
                        page.Complete(SubmissionStatus.Failed);
                    }
                }
            }

            private sealed class Update
            {
                internal readonly Lease Lease;
                internal readonly UnitySkiaRenderCommandList.CapturedCommands Commands;

                internal Update(Lease lease, UnitySkiaRenderCommandList.CapturedCommands commands)
                {
                    Lease = lease;
                    Commands = commands;
                }
            }
        }
    }
}
