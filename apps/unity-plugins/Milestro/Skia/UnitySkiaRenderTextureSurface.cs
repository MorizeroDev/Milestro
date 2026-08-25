using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using Milestro.Binding;
using Milestro.Configuration;
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

        internal enum RenderSubmissionStatus
        {
            Failed = -1,
            Pending = 0,
            Drawn = 1,
            Skipped = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RenderTargetPayload
        {
            public uint AbiVersion;
            public uint StructSize;
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
            public float EffectiveScale;
            public ulong DeviceEpoch;
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
            public float VisualOffsetX;
            public float VisualOffsetY;
            public int ResourceOwnership;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RenderSubmissionPayload
        {
            public uint AbiVersion;
            public uint StructSize;
            public RenderTargetPayload Target;
            public IntPtr Commands;
            public int CommandCount;
            public int Completed;
        }

        internal readonly struct RenderPayloadAbiInfo
        {
            internal RenderPayloadAbiInfo(uint abiVersion,
                ulong layoutFingerprint,
                uint targetSize,
                uint submissionSize,
                uint targetEffectiveScaleOffset,
                uint targetDeviceEpochOffset,
                uint submissionTargetOffset,
                uint submissionCompletedOffset)
            {
                AbiVersion = abiVersion;
                LayoutFingerprint = layoutFingerprint;
                TargetSize = targetSize;
                SubmissionSize = submissionSize;
                TargetEffectiveScaleOffset = targetEffectiveScaleOffset;
                TargetDeviceEpochOffset = targetDeviceEpochOffset;
                SubmissionTargetOffset = submissionTargetOffset;
                SubmissionCompletedOffset = submissionCompletedOffset;
            }

            internal uint AbiVersion { get; }
            internal ulong LayoutFingerprint { get; }
            internal uint TargetSize { get; }
            internal uint SubmissionSize { get; }
            internal uint TargetEffectiveScaleOffset { get; }
            internal uint TargetDeviceEpochOffset { get; }
            internal uint SubmissionTargetOffset { get; }
            internal uint SubmissionCompletedOffset { get; }
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
            public UnitySkiaRenderTextureSurface? Owner;
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

        private sealed class UnityTextureTarget
        {
            private int released;

            internal UnityTextureTarget(UnitySkiaRenderTextureDescriptor descriptor,
                Texture texture,
                RenderTexture? renderTexture,
                IntPtr d3d12ExternalTexture,
                RenderTextureHandleKind handleKind,
                IntPtr colorRenderBufferHandle,
                IntPtr nativeTextureHandle,
                float effectiveScale,
                ulong deviceEpoch)
            {
                Descriptor = descriptor;
                Texture = texture;
                RenderTexture = renderTexture;
                D3D12ExternalTexture = d3d12ExternalTexture;
                HandleKind = handleKind;
                ColorRenderBufferHandle = colorRenderBufferHandle;
                NativeTextureHandle = nativeTextureHandle;
                EffectiveScale = effectiveScale;
                DeviceEpoch = deviceEpoch;
            }

            internal UnitySkiaRenderTextureDescriptor Descriptor { get; }
            internal Texture Texture { get; }
            internal RenderTexture? RenderTexture { get; }
            internal IntPtr D3D12ExternalTexture { get; }
            internal RenderTextureHandleKind HandleKind { get; }
            internal IntPtr ColorRenderBufferHandle { get; }
            internal IntPtr NativeTextureHandle { get; }
            internal float EffectiveScale { get; set; }
            internal ulong DeviceEpoch { get; }

            internal bool IsUsable => Texture != null &&
                                      (RenderTexture == null || RenderTexture.IsCreated()) &&
                                      (ColorRenderBufferHandle != IntPtr.Zero || NativeTextureHandle != IntPtr.Zero);

            internal void Release()
            {
                if (Interlocked.Exchange(ref released, 1) != 0)
                {
                    return;
                }

                if (D3D12ExternalTexture != IntPtr.Zero)
                {
                    ReleaseD3D12Texture(Texture, D3D12ExternalTexture);
                }
                else if (RenderTexture != null)
                {
                    ReleaseRenderTexture(RenderTexture);
                }
            }
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
                command->VisualOffsetX = 0f;
                command->VisualOffsetY = 0f;

                var submission = (RenderSubmissionPayload*)SubmissionPtr;
                submission->AbiVersion = RenderPayloadAbiVersion;
                submission->StructSize = RenderSubmissionPayloadSize;
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

            internal FontTextMeasurement MeasureText()
            {
                ThrowIfDisposed();
                return stagingSnapshot.MeasureTextBoundsAndAdvance();
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
                Texture texture,
                UnitySkiaRenderTextureSurface owner)
            {
                if (!TryPrepareSlot(target, baseline, drawText, out var pendingEvent))
                {
                    return false;
                }

                try
                {
                    AddReusablePendingEvent(pendingEvent, texture, owner);
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

        private readonly struct CompletedRenderEventNotification
        {
            public CompletedRenderEventNotification(UnitySkiaRenderTextureSurface owner, RenderSubmissionStatus status)
            {
                Owner = owner;
                Status = status;
            }

            public readonly UnitySkiaRenderTextureSurface Owner;
            public readonly RenderSubmissionStatus Status;
        }

        private const int RenderDrainMagic = 0x4D524451; // MRDQ
        private const uint RenderPayloadAbiVersion = 1;
        private static readonly object PendingLock = new object();
        private static readonly List<PendingRenderEvent> PendingEvents = new List<PendingRenderEvent>();
        private static readonly List<DeferredRelease> DeferredReleases = new List<DeferredRelease>();
        private static readonly Dictionary<int, PendingRenderDrain> PendingDrains =
            new Dictionary<int, PendingRenderDrain>();
        private static readonly int CompletedOffset =
            (int)Marshal.OffsetOf<RenderSubmissionPayload>(nameof(RenderSubmissionPayload.Completed));
        private static readonly uint RenderTargetPayloadSize = checked((uint)Marshal.SizeOf<RenderTargetPayload>());
        private static readonly uint RenderSubmissionPayloadSize =
            checked((uint)Marshal.SizeOf<RenderSubmissionPayload>());
        private static readonly int DrainCompletedOffset =
            (int)Marshal.OffsetOf<RenderDrainPayload>(nameof(RenderDrainPayload.Completed));
        private static readonly RenderSurfaceBudgetLedger SharedBudgetLedger = new RenderSurfaceBudgetLedger();
        private static readonly RenderSurfaceConfiguration LegacyUnboundedConfiguration =
            new RenderSurfaceConfiguration
            {
                MaxScreenSpaceRasterScale = 1f,
                MinimumFallbackScale = 1f,
                ScaleQuantum = 1f,
                ScaleHysteresis = 0f,
                ConservativeMaxTextureEdge = int.MaxValue,
                MaxPixelsPerSurface = long.MaxValue,
                MaxBytesPerSurface = long.MaxValue,
                MaxGlobalBytes = long.MaxValue,
                MaxTransitionBytes = long.MaxValue,
                MaxAttemptsPerRequestAndEpoch = 1
            };
        private static long nextSerial;
        private static MilestroRenderEventLifetimePump lifetimePump;
#if UNITY_EDITOR
        private static bool editorLifetimePumpRegistered;
#endif

        private readonly RenderSurfaceReplacement<UnityTextureTarget> replacement;
        private readonly RenderSurfaceCounters counters = new RenderSurfaceCounters();
        private UnitySkiaRenderTextureDescriptor requestedDescriptor;
        private RenderSurfaceConfiguration requestedConfiguration = LegacyUnboundedConfiguration;
        private float requestedEffectiveScale = 1f;
        private IntPtr renderEventFunc;
        private int renderEventId;
        private ulong deviceEpoch;
#if MILESTRO_RENDER_DEBUG_LOG
        private bool warnedMissingNativeTarget;
#endif
        private bool disposed;

        internal event Action<RenderSubmissionStatus>? RenderEventCompleted;
        internal static RenderSurfaceBudgetLedger BudgetLedger => SharedBudgetLedger;
        internal static RenderPayloadAbiInfo ManagedPayloadAbiInfo => CreateManagedPayloadAbiInfo();
        internal RenderSurfaceCounterSnapshot CounterSnapshot => counters.Snapshot();
        internal RenderSurfaceDiagnosticsSnapshot DiagnosticsSnapshot => new RenderSurfaceDiagnosticsSnapshot(
            ReadNativeDiagnostics(),
            counters.Snapshot(),
            SharedBudgetLedger.Snapshot(),
            requestedEffectiveScale,
            EffectiveRasterScale,
            deviceEpoch);
        internal ulong DeviceEpoch => deviceEpoch;
        internal float EffectiveRasterScale => replacement.CurrentTarget?.EffectiveScale ?? requestedEffectiveScale;

        public UnitySkiaGraphicsBackend Backend { get; }
        public UnityEngine.ColorSpace ColorSpace => requestedDescriptor.ColorSpace;
        public bool UseSrgbStorage => requestedDescriptor.UseSrgbStorage;

        public Rect DisplayUvRect => DisplayUvRectForBackend(Backend);

        /// <summary>
        /// Assigned by Resize before construction returns and null after Dispose. A failed resize keeps the prior target.
        /// </summary>
        public Texture? Texture => replacement.CurrentTarget?.Texture;

        /// <summary>
        /// Null on Direct3D12, which uses an external Texture2D; non-null on Metal, Vulkan, OpenGL, and OpenGLES.
        /// </summary>
        public RenderTexture? RenderTexture => replacement.CurrentTarget?.RenderTexture;

        public int Width => replacement.CurrentTarget?.Descriptor.Width ?? requestedDescriptor.Width;
        public int Height => replacement.CurrentTarget?.Descriptor.Height ?? requestedDescriptor.Height;

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
            : this(backend, descriptor, true)
        {
        }

        private UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend backend,
            UnitySkiaRenderTextureDescriptor descriptor,
            bool createImmediately)
        {
            Backend = backend;
            EnsureBackendSupported(backend);
            ValidateNativePayloadAbi();
            deviceEpoch = ReadDeviceEpoch();
            replacement = new RenderSurfaceReplacement<UnityTextureTarget>(SharedBudgetLedger);
            requestedDescriptor = NormalizeDescriptor(descriptor);
            renderEventFunc = BindingC.UnityRenderGetRenderEventAndDataFunc();
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderGetRenderTextureEventId((int)Backend, out renderEventId));
            if (createImmediately)
            {
                Resize(requestedDescriptor.Width, requestedDescriptor.Height);
            }
        }

        internal static bool TryCreate(UnitySkiaGraphicsBackend backend,
            RenderSurfaceCandidate candidate,
            UnityEngine.ColorSpace colorSpace,
            RenderSurfaceConfiguration configuration,
            out UnitySkiaRenderTextureSurface? surface,
            out RenderSurfaceFailureReason failureReason)
        {
            surface = null;
            UnitySkiaRenderTextureSurface? candidateSurface = null;
            try
            {
                candidateSurface = new UnitySkiaRenderTextureSurface(backend,
                    new UnitySkiaRenderTextureDescriptor(candidate.RasterWidth,
                        candidate.RasterHeight,
                        colorSpace),
                    false);
                if (!candidateSurface.TryResize(candidate, colorSpace, configuration, out failureReason))
                {
                    candidateSurface.Dispose();
                    return false;
                }

                surface = candidateSurface;
                return true;
            }
            catch
            {
                candidateSurface?.Dispose();
                failureReason = RenderSurfaceFailureReason.Allocation;
                return false;
            }
        }

        internal static ulong ReadCurrentDeviceEpoch()
        {
            return ReadDeviceEpoch();
        }

        internal static NativeRenderDiagnosticsSnapshot ReadNativeDiagnostics()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderGetDiagnosticsSnapshot(out var abiVersion,
                out var structSize,
                out var acceptedSubmissionCount,
                out var rejectedSubmissionCount,
                out var hasLastAcceptedSubmission,
                out var lastAcceptedGraphicsBackend,
                out var lastAcceptedRasterWidth,
                out var lastAcceptedRasterHeight,
                out var lastAcceptedEffectiveScale,
                out var lastAcceptedDeviceEpoch,
                out var currentDeviceEpoch));

            var snapshot = new NativeRenderDiagnosticsSnapshot(abiVersion,
                structSize,
                acceptedSubmissionCount,
                rejectedSubmissionCount,
                hasLastAcceptedSubmission,
                lastAcceptedGraphicsBackend,
                lastAcceptedRasterWidth,
                lastAcceptedRasterHeight,
                lastAcceptedEffectiveScale,
                lastAcceptedDeviceEpoch,
                currentDeviceEpoch);
            if (!snapshot.HasExpectedAbi)
            {
                throw new InvalidOperationException(
                    "Milestro Unity render diagnostics ABI does not match the loaded native plugin.");
            }
            return snapshot;
        }

        public void Resize(int width, int height)
        {
            ThrowIfDisposed();
            RefreshDeviceEpoch();
            CollectCompletedEvents();

            var nextDescriptor = NormalizeDescriptor(new UnitySkiaRenderTextureDescriptor(width,
                height,
                requestedDescriptor.ColorSpace)
            {
                UseSrgbStorage = requestedDescriptor.UseSrgbStorage,
                ClearBeforeDraw = requestedDescriptor.ClearBeforeDraw,
                MsaaSamples = requestedDescriptor.MsaaSamples,
                ResolveStrategy = requestedDescriptor.ResolveStrategy,
                PreferredFormat = requestedDescriptor.PreferredFormat
            });
            requestedDescriptor = nextDescriptor;

            if (!TryComputeByteCount(nextDescriptor, out var byteCount))
            {
                throw new InvalidOperationException("Milestro RenderTexture dimensions overflow checked byte accounting.");
            }

            requestedConfiguration = LegacyUnboundedConfiguration;
            requestedEffectiveScale = 1f;
            if (TryReplaceTarget(nextDescriptor,
                    byteCount,
                    requestedEffectiveScale,
                    requestedConfiguration,
                    out var failureReason))
            {
                return;
            }

            if (replacement.CurrentTarget == null)
            {
                throw new InvalidOperationException("Milestro failed to create its initial RenderTexture target: " +
                                                    failureReason + ".");
            }
        }

        internal bool TryResize(RenderSurfaceCandidate candidate,
            RenderSurfaceConfiguration configuration,
            out RenderSurfaceFailureReason failureReason)
        {
            return TryResize(candidate,
                requestedDescriptor.ColorSpace,
                configuration,
                out failureReason);
        }

        internal bool TryResize(RenderSurfaceCandidate candidate,
            UnityEngine.ColorSpace colorSpace,
            RenderSurfaceConfiguration configuration,
            out RenderSurfaceFailureReason failureReason)
        {
            ThrowIfDisposed();
            RefreshDeviceEpoch();
            CollectCompletedEvents();

            if (candidate.RasterWidth <= 0 ||
                candidate.RasterHeight <= 0 ||
                float.IsNaN(candidate.EffectiveScale) ||
                float.IsInfinity(candidate.EffectiveScale) ||
                candidate.EffectiveScale <= 0f)
            {
                failureReason = RenderSurfaceFailureReason.InvalidRequest;
                return false;
            }

            if (configuration == null)
            {
                failureReason = RenderSurfaceFailureReason.InvalidConfiguration;
                return false;
            }

            var nextDescriptor = NormalizeDescriptor(new UnitySkiaRenderTextureDescriptor(candidate.RasterWidth,
                candidate.RasterHeight,
                colorSpace)
            {
                UseSrgbStorage = requestedDescriptor.UseSrgbStorage,
                ClearBeforeDraw = requestedDescriptor.ClearBeforeDraw,
                MsaaSamples = requestedDescriptor.MsaaSamples,
                ResolveStrategy = requestedDescriptor.ResolveStrategy,
                PreferredFormat = requestedDescriptor.PreferredFormat
            });
            if (!TryComputeByteCount(nextDescriptor, out var checkedByteCount) ||
                checkedByteCount != candidate.ByteCount)
            {
                failureReason = RenderSurfaceFailureReason.InvalidRequest;
                return false;
            }

            requestedDescriptor = nextDescriptor;
            requestedConfiguration = CopyConfiguration(configuration);
            requestedEffectiveScale = candidate.EffectiveScale;
            return TryReplaceTarget(nextDescriptor,
                checkedByteCount,
                requestedEffectiveScale,
                requestedConfiguration,
                out failureReason);
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
            RefreshDeviceEpoch();
            CollectCompletedEvents();

            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (!TryGetUsableTarget(out var textureTarget))
            {
                if (!TryRestoreRequestedTarget() || !TryGetUsableTarget(out textureTarget))
                {
                    return false;
                }
            }

            if (!TryGetNativeTargetHandles(textureTarget,
                    out var handleKind,
                    out var colorRenderBufferHandle,
                    out var nativeTextureHandle))
            {
                return false;
            }
#if MILESTRO_RENDER_DEBUG_LOG
            warnedMissingNativeTarget = false;
#endif

            var target = CreateRenderTargetPayload(textureTarget,
                handleKind,
                colorRenderBufferHandle,
                nativeTextureHandle,
                clearBeforeDraw);

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
                    AbiVersion = RenderPayloadAbiVersion,
                    StructSize = RenderSubmissionPayloadSize,
                    Target = target,
                    Commands = commandsPtr,
                    CommandCount = commands.Count,
                    Completed = 0
                };

                submissionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RenderSubmissionPayload>());
                Marshal.StructureToPtr(submission, submissionPtr, false);

                // The target snapshot and checked handles above guarantee a live texture for this event.
                pendingEvent = AddPendingEvent((int)Backend,
                    submissionPtr,
                    commandsPtr,
                    textureTarget.Texture,
                    resources,
                    ownedResources,
                    this);
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
            RefreshDeviceEpoch();
            CollectCompletedEvents();

            if (submission == null)
            {
                throw new ArgumentNullException(nameof(submission));
            }

            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (!TryGetUsableTarget(out var textureTarget))
            {
                if (!TryRestoreRequestedTarget() || !TryGetUsableTarget(out textureTarget))
                {
                    return false;
                }
            }

            if (!TryGetNativeTargetHandles(textureTarget,
                    out var handleKind,
                    out var colorRenderBufferHandle,
                    out var nativeTextureHandle))
            {
                return false;
            }
#if MILESTRO_RENDER_DEBUG_LOG
            warnedMissingNativeTarget = false;
#endif

            var target = CreateRenderTargetPayload(textureTarget,
                handleKind,
                colorRenderBufferHandle,
                nativeTextureHandle,
                clearBeforeDraw);

            var queued = submission.TryPrepareAndSubmit(target,
                baseline,
                drawText,
                renderEventFunc,
                renderEventId,
                textureTarget.Texture,
                this);
            return queued;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            replacement.Clear(RetireTarget);
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
                        VisualOffsetX = command.VisualOffsetX,
                        VisualOffsetY = command.VisualOffsetY,
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

        private static RenderSurfaceConfiguration CopyConfiguration(RenderSurfaceConfiguration configuration)
        {
            return new RenderSurfaceConfiguration
            {
                MaxScreenSpaceRasterScale = configuration.MaxScreenSpaceRasterScale,
                MinimumFallbackScale = configuration.MinimumFallbackScale,
                ScaleQuantum = configuration.ScaleQuantum,
                ScaleHysteresis = configuration.ScaleHysteresis,
                ConservativeMaxTextureEdge = configuration.ConservativeMaxTextureEdge,
                MaxPixelsPerSurface = configuration.MaxPixelsPerSurface,
                MaxBytesPerSurface = configuration.MaxBytesPerSurface,
                MaxGlobalBytes = configuration.MaxGlobalBytes,
                MaxTransitionBytes = configuration.MaxTransitionBytes,
                MaxAttemptsPerRequestAndEpoch = configuration.MaxAttemptsPerRequestAndEpoch
            };
        }

        private static Rect DisplayUvRectForBackend(UnitySkiaGraphicsBackend backend)
        {
            return new Rect(0f, 1f, 1f, -1f);
        }

        private bool TryGetUsableTarget([NotNullWhen(true)] out UnityTextureTarget? target)
        {
            target = replacement.CurrentTarget;
            return target != null && target.IsUsable && target.DeviceEpoch == deviceEpoch;
        }

        private bool TryRestoreRequestedTarget()
        {
            if (!TryComputeByteCount(requestedDescriptor, out var byteCount))
            {
                return false;
            }

            return TryReplaceTarget(requestedDescriptor,
                byteCount,
                requestedEffectiveScale,
                requestedConfiguration,
                out _);
        }

        private static void ConfigureDisplayTexture(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
        }

        private bool TryGetNativeTargetHandles(UnityTextureTarget target,
            out RenderTextureHandleKind handleKind,
            out IntPtr colorRenderBufferHandle,
            out IntPtr nativeTextureHandle)
        {
            handleKind = target.HandleKind;
            colorRenderBufferHandle = target.ColorRenderBufferHandle;
            nativeTextureHandle = target.NativeTextureHandle;
            if (target.IsUsable)
            {
                return true;
            }

            WarnMissingNativeTarget();
            return false;
        }

        private RenderTargetPayload CreateRenderTargetPayload(UnityTextureTarget textureTarget,
            RenderTextureHandleKind handleKind,
            IntPtr colorRenderBufferHandle,
            IntPtr nativeTextureHandle,
            bool? clearBeforeDraw)
        {
            return new RenderTargetPayload
            {
                AbiVersion = RenderPayloadAbiVersion,
                StructSize = RenderTargetPayloadSize,
                GraphicsBackend = (int)Backend,
                HandleKind = (int)handleKind,
                ColorRenderBufferHandle = colorRenderBufferHandle,
                NativeTextureHandle = nativeTextureHandle,
                Width = textureTarget.Descriptor.Width,
                Height = textureTarget.Descriptor.Height,
                ColorSpace = (int)textureTarget.Descriptor.ColorSpace,
                StorageSrgb = textureTarget.Descriptor.UseSrgbStorage ? 1 : 0,
                ClearBeforeDraw = (clearBeforeDraw ?? textureTarget.Descriptor.ClearBeforeDraw) ? 1 : 0,
                MsaaSamples = textureTarget.Descriptor.MsaaSamples,
                ResolveStrategy = (int)textureTarget.Descriptor.ResolveStrategy,
                PreferredFormat = (int)textureTarget.Descriptor.PreferredFormat,
                EffectiveScale = textureTarget.EffectiveScale,
                DeviceEpoch = textureTarget.DeviceEpoch
            };
        }

        private static void ValidateNativePayloadAbi()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderGetPayloadAbiInfo(out var abiVersion,
                out var layoutFingerprint,
                out var targetSize,
                out var submissionSize,
                out var targetEffectiveScaleOffset,
                out var targetDeviceEpochOffset,
                out var submissionTargetOffset,
                out var submissionCompletedOffset));

            var nativeAbi = new RenderPayloadAbiInfo(abiVersion,
                layoutFingerprint,
                targetSize,
                submissionSize,
                targetEffectiveScaleOffset,
                targetDeviceEpochOffset,
                submissionTargetOffset,
                submissionCompletedOffset);
            if (!PayloadAbiMatches(nativeAbi))
            {
                throw new InvalidOperationException(
                    "Milestro Unity render payload ABI does not match the loaded native plugin.");
            }
        }

        internal static bool PayloadAbiMatches(RenderPayloadAbiInfo nativeAbi)
        {
            var managedAbi = CreateManagedPayloadAbiInfo();
            return nativeAbi.AbiVersion == managedAbi.AbiVersion &&
                   nativeAbi.LayoutFingerprint == managedAbi.LayoutFingerprint &&
                   nativeAbi.TargetSize == managedAbi.TargetSize &&
                   nativeAbi.SubmissionSize == managedAbi.SubmissionSize &&
                   nativeAbi.TargetEffectiveScaleOffset == managedAbi.TargetEffectiveScaleOffset &&
                   nativeAbi.TargetDeviceEpochOffset == managedAbi.TargetDeviceEpochOffset &&
                   nativeAbi.SubmissionTargetOffset == managedAbi.SubmissionTargetOffset &&
                   nativeAbi.SubmissionCompletedOffset == managedAbi.SubmissionCompletedOffset;
        }

        private static RenderPayloadAbiInfo CreateManagedPayloadAbiInfo()
        {
            var targetEffectiveScaleOffset = LayoutOffset<RenderTargetPayload>(
                nameof(RenderTargetPayload.EffectiveScale));
            var targetDeviceEpochOffset = LayoutOffset<RenderTargetPayload>(nameof(RenderTargetPayload.DeviceEpoch));
            var submissionTargetOffset = LayoutOffset<RenderSubmissionPayload>(nameof(RenderSubmissionPayload.Target));
            var submissionCompletedOffset = LayoutOffset<RenderSubmissionPayload>(
                nameof(RenderSubmissionPayload.Completed));
            return new RenderPayloadAbiInfo(RenderPayloadAbiVersion,
                ComputeManagedPayloadLayoutFingerprint(),
                RenderTargetPayloadSize,
                RenderSubmissionPayloadSize,
                targetEffectiveScaleOffset,
                targetDeviceEpochOffset,
                submissionTargetOffset,
                submissionCompletedOffset);
        }

        private static ulong ComputeManagedPayloadLayoutFingerprint()
        {
            const ulong offsetBasis = 14695981039346656037UL;
            var hash = offsetBasis;
            hash = MixLayoutValue(hash, RenderTargetPayloadSize);
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.AbiVersion));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.StructSize));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.GraphicsBackend));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.HandleKind));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.ColorRenderBufferHandle));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.NativeTextureHandle));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.Width));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.Height));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.ColorSpace));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.StorageSrgb));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.ClearBeforeDraw));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.MsaaSamples));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.ResolveStrategy));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.PreferredFormat));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.EffectiveScale));
            hash = MixLayoutMember<RenderTargetPayload>(hash, nameof(RenderTargetPayload.DeviceEpoch));
            hash = MixLayoutValue(hash, RenderSubmissionPayloadSize);
            hash = MixLayoutMember<RenderSubmissionPayload>(hash, nameof(RenderSubmissionPayload.AbiVersion));
            hash = MixLayoutMember<RenderSubmissionPayload>(hash, nameof(RenderSubmissionPayload.StructSize));
            hash = MixLayoutMember<RenderSubmissionPayload>(hash, nameof(RenderSubmissionPayload.Target));
            hash = MixLayoutMember<RenderSubmissionPayload>(hash, nameof(RenderSubmissionPayload.Commands));
            hash = MixLayoutMember<RenderSubmissionPayload>(hash, nameof(RenderSubmissionPayload.CommandCount));
            hash = MixLayoutMember<RenderSubmissionPayload>(hash, nameof(RenderSubmissionPayload.Completed));
            return hash;
        }

        private static ulong MixLayoutMember<T>(ulong hash, string memberName) where T : struct
        {
            return MixLayoutValue(hash, LayoutOffset<T>(memberName));
        }

        private static ulong MixLayoutValue(ulong hash, ulong value)
        {
            unchecked
            {
                return (hash ^ value) * 1099511628211UL;
            }
        }

        private static uint LayoutOffset<T>(string memberName) where T : struct
        {
            return checked((uint)Marshal.OffsetOf<T>(memberName).ToInt64());
        }

        private static ulong ReadDeviceEpoch()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderGetDeviceEpoch(out var epoch));
            if (epoch == 0)
            {
                throw new InvalidOperationException("Milestro Unity render device epoch must be nonzero.");
            }

            return epoch;
        }

        private void RefreshDeviceEpoch()
        {
            var nextEpoch = ReadDeviceEpoch();
            if (nextEpoch == deviceEpoch)
            {
                return;
            }

            deviceEpoch = nextEpoch;
            AbandonPendingLifetimeWorkForDeviceEpochChange();
        }

        private void WarnMissingNativeTarget()
        {
#if MILESTRO_RENDER_DEBUG_LOG
            if (warnedMissingNativeTarget)
            {
                return;
            }

            warnedMissingNativeTarget = true;
            Debug.LogWarning(
                "Milestro skipped a RenderTexture draw because Unity did not expose a native render target handle for the current RenderTexture yet.");
#endif
        }

        private bool TryReplaceTarget(UnitySkiaRenderTextureDescriptor nextDescriptor,
            long byteCount,
            float effectiveScale,
            RenderSurfaceConfiguration configuration,
            out RenderSurfaceFailureReason failureReason)
        {
            if (float.IsNaN(effectiveScale) || float.IsInfinity(effectiveScale) || effectiveScale <= 0f)
            {
                failureReason = RenderSurfaceFailureReason.InvalidRequest;
                return false;
            }

            var current = replacement.CurrentTarget;
            if (current != null &&
                current.IsUsable &&
                current.DeviceEpoch == deviceEpoch &&
                DescriptorsEqual(current.Descriptor, nextDescriptor))
            {
                current.EffectiveScale = effectiveScale;
                failureReason = RenderSurfaceFailureReason.None;
                return true;
            }

            var replaced = replacement.TryReplace(byteCount,
                configuration,
                () =>
                {
                    counters.RecordAllocationAttempt();
                    var allocation = AllocateTarget(nextDescriptor, effectiveScale, deviceEpoch);
                    if (allocation.Success)
                    {
                        counters.RecordAllocationSuccess();
                    }
                    else if (allocation.FailureReason == RenderSurfaceFailureReason.TextureValidation ||
                             allocation.FailureReason == RenderSurfaceFailureReason.NativeHandle)
                    {
                        counters.RecordValidationFailure();
                    }
                    else
                    {
                        counters.RecordAllocationFailure();
                    }

                    return allocation;
                },
                target => target.Release(),
                RetireTarget,
                out failureReason);
            if (replaced)
            {
                counters.RecordAtomicSwap();
            }

            return replaced;
        }

        private RenderSurfaceAllocationResult<UnityTextureTarget> AllocateTarget(
            UnitySkiaRenderTextureDescriptor nextDescriptor,
            float effectiveScale,
            ulong targetDeviceEpoch)
        {
            if (Backend == UnitySkiaGraphicsBackend.Direct3D12)
            {
                return AllocateD3D12Target(nextDescriptor, effectiveScale, targetDeviceEpoch);
            }

            RenderTexture? renderTexture = null;
            try
            {
                var unityDescriptor = new RenderTextureDescriptor(nextDescriptor.Width,
                    nextDescriptor.Height,
                    RenderTextureFormat.ARGB32,
                    0)
                {
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    sRGB = nextDescriptor.UseSrgbStorage
                };
                renderTexture = new RenderTexture(unityDescriptor)
                {
                    name = "Milestro " + Backend + " RenderTexture PoC"
                };
                ConfigureDisplayTexture(renderTexture);
                if (!renderTexture.Create() || !renderTexture.IsCreated())
                {
                    return RenderSurfaceAllocationResult<UnityTextureTarget>.Failed(
                        RenderSurfaceFailureReason.TextureCreation,
                        new UnityTextureTarget(nextDescriptor,
                            renderTexture,
                            renderTexture,
                            IntPtr.Zero,
                            HandleKindForBackend(Backend),
                            IntPtr.Zero,
                            IntPtr.Zero,
                            effectiveScale,
                            targetDeviceEpoch));
                }

                var handleKind = HandleKindForBackend(Backend);
                var colorRenderBufferHandle = IntPtr.Zero;
                var nativeTextureHandle = IntPtr.Zero;
                if (handleKind == RenderTextureHandleKind.RenderBuffer)
                {
                    colorRenderBufferHandle = renderTexture.colorBuffer.GetNativeRenderBufferPtr();
                }
                else
                {
                    nativeTextureHandle = renderTexture.GetNativeTexturePtr();
                }

                var target = new UnityTextureTarget(nextDescriptor,
                    renderTexture,
                    renderTexture,
                    IntPtr.Zero,
                    handleKind,
                    colorRenderBufferHandle,
                    nativeTextureHandle,
                    effectiveScale,
                    targetDeviceEpoch);
                return target.IsUsable
                    ? RenderSurfaceAllocationResult<UnityTextureTarget>.Succeeded(target)
                    : RenderSurfaceAllocationResult<UnityTextureTarget>.Failed(
                        RenderSurfaceFailureReason.NativeHandle,
                        target);
            }
            catch
            {
                if (renderTexture != null)
                {
                    ReleaseRenderTexture(renderTexture);
                }

                return RenderSurfaceAllocationResult<UnityTextureTarget>.Failed(
                    RenderSurfaceFailureReason.Allocation);
            }
        }

        private RenderSurfaceAllocationResult<UnityTextureTarget> AllocateD3D12Target(
            UnitySkiaRenderTextureDescriptor nextDescriptor,
            float effectiveScale,
            ulong targetDeviceEpoch)
        {
            var nativeTexture = IntPtr.Zero;
            Texture? texture = null;
            try
            {
                nativeTexture = CreateD3D12ExternalTextureHandle(nextDescriptor.Width,
                    nextDescriptor.Height,
                    nextDescriptor.UseSrgbStorage ? 1 : 0,
                    (int)nextDescriptor.PreferredFormat);
                if (nativeTexture == IntPtr.Zero)
                {
                    return RenderSurfaceAllocationResult<UnityTextureTarget>.Failed(
                        RenderSurfaceFailureReason.TextureCreation);
                }

                texture = Texture2D.CreateExternalTexture(nextDescriptor.Width,
                    nextDescriptor.Height,
                    TextureFormatForDescriptor(nextDescriptor),
                    false,
                    !nextDescriptor.UseSrgbStorage,
                    nativeTexture);
                if (texture == null)
                {
                    DestroyD3D12ExternalTextureHandle(nativeTexture);
                    return RenderSurfaceAllocationResult<UnityTextureTarget>.Failed(
                        RenderSurfaceFailureReason.TextureCreation);
                }

                texture.name = "Milestro " + Backend + " ExternalTexture PoC";
                ConfigureDisplayTexture(texture);
                var target = new UnityTextureTarget(nextDescriptor,
                    texture,
                    null,
                    nativeTexture,
                    RenderTextureHandleKind.NativeTexture,
                    IntPtr.Zero,
                    nativeTexture,
                    effectiveScale,
                    targetDeviceEpoch);
                return target.IsUsable
                    ? RenderSurfaceAllocationResult<UnityTextureTarget>.Succeeded(target)
                    : RenderSurfaceAllocationResult<UnityTextureTarget>.Failed(
                        RenderSurfaceFailureReason.NativeHandle,
                        target);
            }
            catch
            {
                if (nativeTexture != IntPtr.Zero)
                {
                    ReleaseD3D12Texture(texture, nativeTexture);
                }

                return RenderSurfaceAllocationResult<UnityTextureTarget>.Failed(
                    RenderSurfaceFailureReason.Allocation);
            }
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

        private bool TryComputeByteCount(UnitySkiaRenderTextureDescriptor textureDescriptor, out long byteCount)
        {
            byteCount = 0;
            var surfaceDescriptor = new RenderSurfaceDescriptor(Backend, textureDescriptor, 1);
            if (!RenderSurfaceDescriptorAccounting.TryResolveBytesPerPixel(surfaceDescriptor,
                    out var bytesPerPixel) ||
                textureDescriptor.Width <= 0 ||
                textureDescriptor.Height <= 0 ||
                textureDescriptor.Width > long.MaxValue / textureDescriptor.Height)
            {
                return false;
            }

            var pixels = (long)textureDescriptor.Width * textureDescriptor.Height;
            if (pixels > long.MaxValue / bytesPerPixel)
            {
                return false;
            }

            byteCount = pixels * bytesPerPixel;
            return byteCount > 0;
        }

        private static bool DescriptorsEqual(UnitySkiaRenderTextureDescriptor left,
            UnitySkiaRenderTextureDescriptor right)
        {
            return left.Width == right.Width &&
                   left.Height == right.Height &&
                   left.ColorSpace == right.ColorSpace &&
                   left.UseSrgbStorage == right.UseSrgbStorage &&
                   left.ClearBeforeDraw == right.ClearBeforeDraw &&
                   left.MsaaSamples == right.MsaaSamples &&
                   left.ResolveStrategy == right.ResolveStrategy &&
                   left.PreferredFormat == right.PreferredFormat;
        }

        private void RetireTarget(UnityTextureTarget target,
            RenderSurfaceBudgetLedger.RenderSurfaceBudgetLease lease)
        {
            counters.RecordRetirement();
            DeferReleaseAfterCurrentEvents(() =>
            {
                try
                {
                    target.Release();
                }
                finally
                {
                    lease.Dispose();
                }
            });
        }

        private static PendingRenderEvent AddPendingEvent(int graphicsBackend,
            IntPtr submissionPtr,
            IntPtr commandsPtr,
            Texture texture,
            object[] resources,
            IDisposable[] ownedResources,
            UnitySkiaRenderTextureSurface owner)
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
                    OwnedResources = ownedResources,
                    Owner = owner
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

        private static void AddReusablePendingEvent(PendingRenderEvent pendingEvent,
            Texture texture,
            UnitySkiaRenderTextureSurface owner)
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
                pendingEvent.Owner = owner;
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
                pendingEvent.Owner = null;
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
            List<CompletedRenderEventNotification>? notifications = null;

            lock (PendingLock)
            {
                for (var i = PendingEvents.Count - 1; i >= 0; i--)
                {
                    var pendingEvent = PendingEvents[i];
                    if (Marshal.ReadInt32(pendingEvent.SubmissionPtr, CompletedOffset) == 0)
                    {
                        continue;
                    }

                    var completedStatus = CompletedStatus(pendingEvent.SubmissionPtr);
                    var owner = pendingEvent.Owner;
                    pendingEvent.InUse = false;
                    if (!pendingEvent.Reusable)
                    {
                        FreeSubmission(pendingEvent.SubmissionPtr, pendingEvent.CommandsPtr);
                    }

                    pendingEvent.KeepAlive();
                    pendingEvent.Texture = null;
                    pendingEvent.DisposeOwnedResources();
                    pendingEvent.Owner = null;
                    PendingEvents.RemoveAt(i);
                    if (owner != null)
                    {
                        if (notifications == null)
                        {
                            notifications = new List<CompletedRenderEventNotification>();
                        }
                        notifications.Add(new CompletedRenderEventNotification(owner, completedStatus));
                    }
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

            if (notifications != null)
            {
                foreach (var notification in notifications)
                {
                    notification.Owner.NotifyRenderEventCompleted(notification.Status);
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

        private static void AbandonPendingLifetimeWorkForDeviceEpochChange()
        {
            PendingRenderEvent[] pendingEvents;
            PendingRenderDrain[] pendingDrains;
            Action[] deferredReleases;
            lock (PendingLock)
            {
                pendingEvents = PendingEvents.ToArray();
                PendingEvents.Clear();
                pendingDrains = new PendingRenderDrain[PendingDrains.Count];
                PendingDrains.Values.CopyTo(pendingDrains, 0);
                PendingDrains.Clear();
                deferredReleases = new Action[DeferredReleases.Count];
                for (var i = 0; i < DeferredReleases.Count; ++i)
                {
                    deferredReleases[i] = DeferredReleases[i].Release;
                }
                DeferredReleases.Clear();
            }

            var notifications = new List<CompletedRenderEventNotification>(pendingEvents.Length);
            foreach (var pendingEvent in pendingEvents)
            {
                var owner = pendingEvent.Owner;
                pendingEvent.InUse = false;
                if (!pendingEvent.Reusable)
                {
                    FreeSubmission(pendingEvent.SubmissionPtr, pendingEvent.CommandsPtr);
                }

                pendingEvent.KeepAlive();
                pendingEvent.Texture = null;
                pendingEvent.DisposeOwnedResources();
                pendingEvent.Owner = null;
                if (owner != null)
                {
                    notifications.Add(new CompletedRenderEventNotification(owner,
                        RenderSubmissionStatus.Failed));
                }
            }

            foreach (var pendingDrain in pendingDrains)
            {
                if (pendingDrain.DrainPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pendingDrain.DrainPtr);
                }
            }

            Exception? firstException = null;
            foreach (var release in deferredReleases)
            {
                try
                {
                    release();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                    {
                        firstException = exception;
                    }
                }
            }

            foreach (var notification in notifications)
            {
                try
                {
                    notification.Owner.NotifyRenderEventCompleted(notification.Status);
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                    {
                        firstException = exception;
                    }
                }
            }

            if (firstException != null)
            {
                ExceptionDispatchInfo.Capture(firstException).Throw();
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

        private static RenderSubmissionStatus CompletedStatus(IntPtr submissionPtr)
        {
            var rawStatus = Marshal.ReadInt32(submissionPtr, CompletedOffset);
            if (rawStatus == (int)RenderSubmissionStatus.Pending)
            {
                return RenderSubmissionStatus.Drawn;
            }

            if (rawStatus == (int)RenderSubmissionStatus.Drawn)
            {
                return RenderSubmissionStatus.Drawn;
            }

            if (rawStatus == (int)RenderSubmissionStatus.Skipped)
            {
                return RenderSubmissionStatus.Skipped;
            }

            return RenderSubmissionStatus.Failed;
        }

        private void NotifyRenderEventCompleted(RenderSubmissionStatus status)
        {
            if (disposed)
            {
                return;
            }

            RenderEventCompleted?.Invoke(status);
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
