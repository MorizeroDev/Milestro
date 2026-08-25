using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace Milestro.Tests.ScreenSpaceHiDpiIntegration
{
    using Canvas = UnityEngine.Canvas;
    using Input = UnityEngine.Input;
    using Path = System.IO.Path;

    [Preserve]
    public sealed class ScreenSpaceHiDpiScenarioRunner : MonoBehaviour
    {
        private const string ExpectedLinkHref = "milestro://integration/screen-space-hidpi";
        private const string ExpectedLinkId = "screen-space-hidpi-link";
        private const string ExpectedImeCommit = "高清";
        internal const string IcuResourcePath = "Milestro/icudtl.dat";
        private const float ManualTimeoutSeconds = 180f;
        private const int StableFrameCount = 120;

        [SerializeField] private Canvas? targetCanvas;
        [SerializeField] private Camera? targetCamera;
        [SerializeField] private TextBox? textBox;
        [SerializeField] private TextInput? textInput;
        [SerializeField] private SlimTextRenderTextureProducer? slimText;
        [SerializeField] private WorldSpaceTextBox? worldSpaceTextBox;
        [SerializeField] private WorldSpaceSlimText? worldSpaceSlimText;
        [SerializeField] private ScreenSpaceHiDpiPhysicalPointerAudit? pointerAudit;
        [SerializeField] private TextAsset? packageManifest;
        [SerializeField] private string exactHead = string.Empty;
        [SerializeField] private string exactTree = string.Empty;
        [SerializeField] private string packageContentSha256 = string.Empty;
        [SerializeField] private string expectedBackend = string.Empty;

        private readonly List<string> incompleteReasons = new List<string>();
        private ScreenSpaceHiDpiRecorder? recorder;
        private RenderSurfaceConfiguration? originalRenderSurfaceConfiguration;
        private Vector2 textBoxOriginalSize;
        private int originalTargetDisplay;
        private bool baselinePhysicalLink;
        private bool highDpiPhysicalLink;
        private bool imeCompositionObserved;
        private bool humanVisualConfirmed;
        private bool crossDisplayConfirmed;
        private bool automationPassed;
        private bool lastKnownGoodPreserved;
        private bool recoverySucceeded;
        private bool worldSpaceExact;
        private bool logicalStateStable;
        private ulong initialDeviceEpoch;
        private ulong finalDeviceEpoch;
        private string currentPhase = "initializing";
        private string instructions = string.Empty;
        private bool highDpiInteractionPhase;
        private bool runtimeStateCaptured;
        private bool linkListenerAttached;
        private string validatedIcuResourceSha256 = string.Empty;

        public void Configure(Canvas canvas,
            Camera worldCamera,
            TextBox linkTextBox,
            TextInput input,
            SlimTextRenderTextureProducer slimTextProducer,
            WorldSpaceTextBox worldTextBox,
            WorldSpaceSlimText worldSlimText,
            ScreenSpaceHiDpiPhysicalPointerAudit physicalPointerAudit,
            TextAsset manifest,
            string sourceHead,
            string sourceTree,
            string expectedManifestContentSha256,
            string backend)
        {
            targetCanvas = canvas;
            targetCamera = worldCamera;
            textBox = linkTextBox;
            textInput = input;
            slimText = slimTextProducer;
            worldSpaceTextBox = worldTextBox;
            worldSpaceSlimText = worldSlimText;
            pointerAudit = physicalPointerAudit;
            packageManifest = manifest;
            exactHead = sourceHead;
            exactTree = sourceTree;
            packageContentSha256 = expectedManifestContentSha256;
            expectedBackend = backend;
        }

        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(RunScenario());
            while (stack.Count > 0)
            {
                object? current;
                try
                {
                    var active = stack.Peek();
                    if (!active.MoveNext())
                    {
                        (active as IDisposable)?.Dispose();
                        stack.Pop();
                        continue;
                    }
                    current = active.Current;
                }
                catch (Exception exception)
                {
                    while (stack.Count > 0)
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                    }
                    CompleteFailure(exception);
                    yield break;
                }

                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return current;
            }
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(Input.compositionString))
            {
                imeCompositionObserved = true;
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                humanVisualConfirmed = true;
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                crossDisplayConfirmed = true;
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(12f, 12f, 760f, 142f), "Milestro Screen Space HiDPI gate");
            GUI.Label(new Rect(28f, 38f, 730f, 24f), "Phase: " + currentPhase);
            GUI.Label(new Rect(28f, 62f, 730f, 68f), instructions);
            GUI.Label(new Rect(28f, 126f, 730f, 20f), "Log: " + (recorder?.LogPath ?? "not opened"));
        }

        private void OnDestroy()
        {
            RestoreRuntimeState();
            if (recorder == null)
            {
                return;
            }
            try
            {
                recorder.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError("SCREEN_SPACE_HIDPI_HIDPI_RECORDER_DISPOSE_FAILED " + exception);
            }
        }

        private IEnumerator RunScenario()
        {
            RequireConfigured();
            recorder = new ScreenSpaceHiDpiRecorder(exactHead,
                exactTree,
                NormalizeBackend(expectedBackend),
                packageContentSha256,
                packageManifest!);
            originalRenderSurfaceConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            originalTargetDisplay = targetCanvas!.targetDisplay;
            textBoxOriginalSize = textBox!.rectTransform.sizeDelta;
            runtimeStateCaptured = true;
            textBox.onLinkClicked.AddListener(HandleLinkClicked);
            linkListenerAttached = true;
            try
            {
                validatedIcuResourceSha256 = ScreenSpaceHiDpiIcuPayloadValidator.Validate(
                    Resources.Load<TextAsset>(IcuResourcePath),
                    Resources.LoadAll<TextAsset>(IcuResourcePath),
                    ScreenSpaceHiDpiPackageManifest.ExpectedIcuSha256);
                ValidateBackend();
                WriteEnvironment("start",
                    false,
                    "Clean build identity, package manifest and runtime ICU payload validated.");
                yield return WaitForOutputs(300);

                currentPhase = "baseline-1080";
                instructions = "The player is setting a real 1920x1080 framebuffer. Do not resize it during the sample.";
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                yield return WaitForResolution(1920, 1080, 300, "1080p baseline");
                yield return WaitForOutputs(180);
                var baselineInput = CaptureInputState();
                var baselineTextBox = CaptureAndWriteSurface("baseline", "TextBox", SurfaceForTextBox());
                var baselineTextInput = CaptureAndWriteSurface("baseline", "TextInput", SurfaceForTextInput());
                var baselineSlimText = CaptureAndWriteSurface("baseline", "SlimText", SurfaceForSlimText());
                initialDeviceEpoch = baselineTextBox.DeviceEpoch;
                Require(initialDeviceEpoch != 0 && baselineTextInput.DeviceEpoch == initialDeviceEpoch &&
                        baselineSlimText.DeviceEpoch == initialDeviceEpoch,
                    "Screen-space surfaces did not share a nonzero device epoch.");
                yield return CaptureScreenshot("baseline");

                currentPhase = "baseline-real-pointer";
                instructions = "Use the real mouse/touch to click the visible Screen Space HiDPI link once. Synthetic dispatch is not accepted.";
                highDpiInteractionPhase = false;
                yield return WaitUntil(() => baselinePhysicalLink,
                    ManualTimeoutSeconds,
                    "BASELINE_PHYSICAL_LINK_PENDING");

                currentPhase = "stable-120-frames";
                instructions = "Hands off: measuring allocation/resize churn for 120 stable frames.";
                var stableBefore = CaptureSurfaceSet();
                var stableTextBoxBefore = SurfaceForTextBox().Sample();
                var stableTextInputBefore = SurfaceForTextInput().Sample();
                var stableSlimTextBefore = SurfaceForSlimText().Sample();
                for (var frame = 0; frame < StableFrameCount; ++frame)
                {
                    yield return null;
                }
                var stableAfter = CaptureSurfaceSet();
                AssertStable(stableBefore, stableAfter);
                CaptureAndWriteSurface("stable-120",
                    "TextBox",
                    SurfaceForTextBox(),
                    stableTextBoxBefore);
                CaptureAndWriteSurface("stable-120",
                    "TextInput",
                    SurfaceForTextInput(),
                    stableTextInputBefore);
                CaptureAndWriteSurface("stable-120",
                    "SlimText",
                    SurfaceForSlimText(),
                    stableSlimTextBefore);

                yield return RunContinuousResizeCheck();

                currentPhase = "high-dpi-framebuffer";
                var highWidth = 3840;
                var highHeight = 2160;
                if (string.Equals(NormalizeBackend(expectedBackend), "metal", StringComparison.Ordinal))
                {
                    highWidth = Math.Max(highWidth, Display.main.systemWidth);
                    highHeight = Math.Max(highHeight, Display.main.systemHeight);
                }
                instructions = "Switching to the real high-resolution framebuffer; the next sample must show larger raster textures with unchanged logical layout.";
                Screen.SetResolution(highWidth, highHeight, FullScreenMode.FullScreenWindow);
                yield return WaitForResolution(highWidth, highHeight, 360, "4K/Retina framebuffer");
                yield return WaitForOutputs(180);
                var highInput = CaptureInputState();
                var highTextBox = CaptureAndWriteSurface("high-dpi", "TextBox", SurfaceForTextBox());
                var highTextInput = CaptureAndWriteSurface("high-dpi", "TextInput", SurfaceForTextInput());
                var highSlimText = CaptureAndWriteSurface("high-dpi", "SlimText", SurfaceForSlimText());
                logicalStateStable = baselineInput.Equivalent(highInput) &&
                                     baselineTextBox.LogicalWidth == highTextBox.LogicalWidth &&
                                     baselineTextBox.LogicalHeight == highTextBox.LogicalHeight &&
                                     baselineTextInput.LogicalWidth == highTextInput.LogicalWidth &&
                                     baselineTextInput.LogicalHeight == highTextInput.LogicalHeight &&
                                     baselineSlimText.LogicalWidth == highSlimText.LogicalWidth &&
                                     baselineSlimText.LogicalHeight == highSlimText.LogicalHeight;
                Require(logicalStateStable, "Logical layout/caret/selection/scroll state changed with framebuffer density.");
                if (!highTextBox.IsHiDpi || !highTextInput.IsHiDpi || !highSlimText.IsHiDpi)
                {
                    AddIncomplete("HIGH_DPI_SCALE_NOT_OBSERVED");
                }
                yield return CaptureScreenshot("high-dpi");

                yield return RunBudgetFailureRecovery();
                yield return RunWorldSpaceCheck();
                yield return RunCrossDisplayCheck();

                currentPhase = "real-link-ime-visual";
                instructions = "On the high-DPI output: click the link with real input, focus TextInput and enter 高清 through a real OS IME composition, inspect TextBox/TextInput/SlimText and WorldSpace clarity, then press F9.";
                highDpiInteractionPhase = true;
                yield return WaitUntil(() => highDpiPhysicalLink &&
                                             imeCompositionObserved &&
                                             textInput!.Text.Contains(ExpectedImeCommit) &&
                                             humanVisualConfirmed,
                    ManualTimeoutSeconds,
                    "REAL_POINTER_IME_OR_VISUAL_PENDING");

                finalDeviceEpoch = CaptureSurfaceSet().TextBox.DeviceEpoch;
                if (finalDeviceEpoch == initialDeviceEpoch)
                {
                    AddIncomplete("DEVICE_EPOCH_NOT_EXERCISED");
                }
                else
                {
                    yield return WaitForOutputs(180);
                    Require(CaptureSurfaceSet().AllUsable, "Surfaces did not recover after a real device epoch change.");
                }

                automationPassed = true;
            }
            finally
            {
                RestoreRuntimeState();
            }

            CompleteTerminal();
        }

        private IEnumerator RunContinuousResizeCheck()
        {
            currentPhase = "continuous-resize";
            instructions = "Cycling real player framebuffer sizes and checking that every surface remains usable.";
            var sizes = new[]
            {
                new Vector2Int(1280, 720),
                new Vector2Int(1600, 900),
                new Vector2Int(1920, 1080),
                new Vector2Int(1366, 768),
                new Vector2Int(1920, 1080)
            };
            for (var index = 0; index < sizes.Length; ++index)
            {
                var size = sizes[index];
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return WaitFrames(30);
                Require(CaptureSurfaceSet().AllUsable,
                    "A screen-space surface became unusable during continuous resize.");
                var phase = "resize-" + index;
                CaptureAndWriteSurface(phase, "TextBox", SurfaceForTextBox());
                CaptureAndWriteSurface(phase, "TextInput", SurfaceForTextInput());
                CaptureAndWriteSurface(phase, "SlimText", SurfaceForSlimText());
            }
        }

        private IEnumerator RunBudgetFailureRecovery()
        {
            currentPhase = "budget-failure-last-known-good";
            instructions = "Injecting a checked global/transition budget failure; the current texture must remain visible.";
            var before = SurfaceForTextBox();
            var originalTexture = before.Texture;
            var originalWidth = before.Width;
            var originalHeight = before.Height;
            var originalCounters = before.Counters;
            var originalSample = before.Sample();
            var constrained = CloneConfiguration(originalRenderSurfaceConfiguration!);
            constrained.MaxGlobalBytes = 1;
            constrained.MaxTransitionBytes = 1;
            MilestroConfiguration.Configuration.RenderSurface = constrained;
            textBox!.rectTransform.sizeDelta = textBoxOriginalSize + new Vector2(17f, 9f);
            yield return WaitFrames(30);
            var failed = SurfaceForTextBox();
            lastKnownGoodPreserved = failed.Texture == originalTexture &&
                                     failed.Width == originalWidth &&
                                     failed.Height == originalHeight &&
                                     failed.Counters.AtomicSwaps == originalCounters.AtomicSwaps &&
                                     failed.Counters.AllocationAttempts == originalCounters.AllocationAttempts;
            Require(lastKnownGoodPreserved, "Budget failure did not preserve the last-known-good texture without allocation churn.");
            CaptureAndWriteSurface("budget-failure", "TextBox", failed, originalSample);

            currentPhase = "budget-recovery";
            MilestroConfiguration.Configuration.RenderSurface = originalRenderSurfaceConfiguration!;
            yield return WaitUntil(() =>
                {
                    var probe = SurfaceForTextBox();
                    return probe.Texture != null && probe.Texture != originalTexture &&
                           probe.Counters.AtomicSwaps > originalCounters.AtomicSwaps;
                }, 10f, "BUDGET_RECOVERY_TIMEOUT");
            var recovered = SurfaceForTextBox();
            recoverySucceeded = recovered.Texture != null && recovered.Texture != originalTexture &&
                                recovered.Counters.AtomicSwaps > originalCounters.AtomicSwaps;
            Require(recoverySucceeded, "Surface did not recover after restoring the configuration fingerprint.");
            textBox.rectTransform.sizeDelta = textBoxOriginalSize;
            yield return WaitForOutputs(120);
            CaptureAndWriteSurface("budget-recovery", "TextBox", SurfaceForTextBox());
        }

        private IEnumerator RunWorldSpaceCheck()
        {
            currentPhase = "world-space-exact";
            instructions = "Verifying WorldSpaceTextBox and WorldSpaceSlimText retain exact textureSizePixels semantics.";
            yield return WaitFrames(10);
            var textProducer = worldSpaceTextBox!.GetComponent<TextBoxRenderTextureProducer>();
            var slimProducer = worldSpaceSlimText!.GetComponent<SlimTextRenderTextureProducer>();
            worldSpaceExact = textProducer != null && slimProducer != null &&
                              textProducer.OutputWidth == worldSpaceTextBox.textureSizePixels.x &&
                              textProducer.OutputHeight == worldSpaceTextBox.textureSizePixels.y &&
                              slimProducer.OutputWidth == worldSpaceSlimText.textureSizePixels.x &&
                              slimProducer.OutputHeight == worldSpaceSlimText.textureSizePixels.y;
            Require(worldSpaceExact, "WorldSpace exact-size rendering changed under screen-space HiDPI.");
            WriteWorldSpaceRecord("WorldSpaceTextBox",
                textProducer!.OutputWidth,
                textProducer.OutputHeight,
                worldSpaceTextBox.textureSizePixels);
            WriteWorldSpaceRecord("WorldSpaceSlimText",
                slimProducer!.OutputWidth,
                slimProducer.OutputHeight,
                worldSpaceSlimText.textureSizePixels);
        }

        private IEnumerator RunCrossDisplayCheck()
        {
            currentPhase = "cross-display";
            if (Display.displays.Length < 2)
            {
                AddIncomplete("SECOND_DISPLAY_NOT_AVAILABLE");
                yield break;
            }

            Display.displays[1].Activate();
            targetCanvas!.targetDisplay = 1;
            if (targetCamera != null)
            {
                targetCamera.targetDisplay = 1;
            }
            instructions = "The canvas is on display 2. Move/inspect the real output and press F10 only after it is visible and sharp.";
            yield return WaitFrames(90);
            var measurementOk = ScreenSpaceRasterMetrics.TryMeasure(textBox!.rectTransform,
                new Vector3[4], out var measurement) && measurement.TargetDisplay == 1;
            Require(measurementOk, "Canvas event-camera/target-display measurement did not follow display 2.");
            yield return WaitUntil(() => crossDisplayConfirmed, ManualTimeoutSeconds, "CROSS_DISPLAY_HUMAN_PENDING");
            CaptureAndWriteSurface("cross-display", "TextBox", SurfaceForTextBox());
            CaptureAndWriteSurface("cross-display", "TextInput", SurfaceForTextInput());
            CaptureAndWriteSurface("cross-display", "SlimText", SurfaceForSlimText());
            targetCanvas.targetDisplay = originalTargetDisplay;
            if (targetCamera != null)
            {
                targetCamera.targetDisplay = originalTargetDisplay;
            }
            yield return WaitFrames(30);
        }

        private IEnumerator CaptureScreenshot(string label)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(Path.GetTempPath(),
                "screen-space-hidpi." + exactHead + "." + NormalizeBackend(expectedBackend) + "." +
                recorder!.ScenarioId + "." + label + ".png");
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 10f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                AddIncomplete("SCREENSHOT_MISSING_" + label.ToUpperInvariant());
                yield break;
            }

            var record = EnvironmentRecord("screenshot-" + label, false, "Captured framebuffer screenshot.");
            record.screenshotPath = path;
            record.screenshotSha256 = ScreenSpaceHiDpiHash.Sha256File(path);
            recorder.Write(record);
        }

        private IEnumerator WaitForOutputs(int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; ++frame)
            {
                if (SurfacesReady())
                {
                    yield break;
                }
                yield return null;
            }
            throw new InvalidOperationException("Screen-space or WorldSpace output did not become ready.");
        }

        private IEnumerator WaitForResolution(int expectedWidth, int expectedHeight, int maxFrames, string label)
        {
            for (var frame = 0; frame < maxFrames; ++frame)
            {
                if (Screen.width >= expectedWidth && Screen.height >= expectedHeight)
                {
                    yield break;
                }
                yield return null;
            }
            AddIncomplete(label.Replace(' ', '_').ToUpperInvariant() + "_NOT_AVAILABLE");
        }

        private IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string incompleteReason)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (!condition())
            {
                AddIncomplete(incompleteReason);
            }
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (var frame = 0; frame < count; ++frame)
            {
                yield return null;
            }
        }

        private bool SurfacesReady()
        {
            if (textBox == null || textInput == null || slimText == null ||
                worldSpaceTextBox == null || worldSpaceSlimText == null)
            {
                return false;
            }
            return textBox.Texture != null && textInput.Texture != null && slimText.HasOutput &&
                   worldSpaceTextBox.GetComponent<TextBoxRenderTextureProducer>()?.HasOutput == true &&
                   worldSpaceSlimText.GetComponent<SlimTextRenderTextureProducer>()?.HasOutput == true;
        }

        private SurfaceProbe SurfaceForTextBox()
        {
            var producer = textBox!.GetComponent<TextBoxRenderTextureProducer>();
            return SurfaceProbe.FromProducer(producer, textBox.rectTransform);
        }

        private SurfaceProbe SurfaceForTextInput()
        {
            return SurfaceProbe.FromTextInput(textInput!);
        }

        private SurfaceProbe SurfaceForSlimText()
        {
            return SurfaceProbe.FromProducer(slimText!, slimText!.GetComponent<RectTransform>());
        }

        private SurfaceSet CaptureSurfaceSet()
        {
            return new SurfaceSet(SurfaceForTextBox(), SurfaceForTextInput(), SurfaceForSlimText());
        }

        private SurfaceSample CaptureAndWriteSurface(string phase,
            string component,
            SurfaceProbe probe,
            SurfaceSample? counterBaseline = null)
        {
            var sample = probe.Sample();
            var record = EnvironmentRecord(phase, false, component + " surface sample.");
            record.component = component;
            record.requestedScale = sample.RequestedScale;
            record.clampedScale = sample.ClampedScale;
            record.effectiveScale = sample.EffectiveScale;
            record.logicalWidth = sample.LogicalWidth;
            record.logicalHeight = sample.LogicalHeight;
            record.rasterWidth = sample.RasterWidth;
            record.rasterHeight = sample.RasterHeight;
            var configuration = MilestroConfiguration.Configuration.RenderSurface;
            record.runtimeMaxTextureEdge = SystemInfo.maxTextureSize;
            record.backendMaxTextureEdge = sample.BackendMaxTextureEdge;
            record.configuredMaxTextureEdge = configuration.ConservativeMaxTextureEdge;
            record.maxPixelsPerSurface = configuration.MaxPixelsPerSurface;
            record.maxBytesPerSurface = configuration.MaxBytesPerSurface;
            record.maxGlobalBytes = configuration.MaxGlobalBytes;
            record.maxTransitionBytes = configuration.MaxTransitionBytes;
            record.allocationAttempts = sample.Counters.AllocationAttempts;
            record.allocationSuccesses = sample.Counters.AllocationSuccesses;
            record.allocationFailures = sample.Counters.AllocationFailures;
            record.validationFailures = sample.Counters.ValidationFailures;
            record.suppressedAttempts = sample.Counters.SuppressedAttempts;
            record.atomicSwaps = sample.Counters.AtomicSwaps;
            record.retirements = sample.Counters.Retirements;
            if (counterBaseline.HasValue)
            {
                record.hasCounterDelta = true;
                record.allocationAttemptDelta = sample.Counters.AllocationAttempts -
                                                counterBaseline.Value.Counters.AllocationAttempts;
                record.atomicSwapDelta = sample.Counters.AtomicSwaps -
                                         counterBaseline.Value.Counters.AtomicSwaps;
                record.retirementDelta = sample.Counters.Retirements -
                                         counterBaseline.Value.Counters.Retirements;
            }
            record.committedBytes = sample.Budget.CommittedBytes;
            record.reservedBytes = sample.Budget.ReservedBytes;
            record.ledgerGeneration = sample.Budget.Generation;
            record.deviceEpoch = sample.DeviceEpoch;
            recorder!.Write(record);
            return sample;
        }

        private void WriteWorldSpaceRecord(string component,
            int actualWidth,
            int actualHeight,
            Vector2Int expectedSize)
        {
            var record = EnvironmentRecord("world-space", false, component + " exact-size sample.");
            record.component = component;
            record.requestedScale = 1f;
            record.clampedScale = 1f;
            record.effectiveScale = 1f;
            record.logicalWidth = expectedSize.x;
            record.logicalHeight = expectedSize.y;
            record.rasterWidth = actualWidth;
            record.rasterHeight = actualHeight;
            record.worldSpaceExact = actualWidth == expectedSize.x && actualHeight == expectedSize.y;
            recorder!.Write(record);
        }

        private InputStateSnapshot CaptureInputState()
        {
            var inputBox = typeof(TextInput).GetField("inputBox", BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetValue(textInput) as InputBox;
            if (inputBox == null)
            {
                throw new InvalidOperationException("TextInput native InputBox is unavailable for logical-state sampling.");
            }
            inputBox.SetSelectionUtf8(0, Math.Min(7UL, inputBox.Utf16ToUtf8(7)));
            return new InputStateSnapshot(inputBox.GetMetrics(),
                inputBox.Cursor,
                inputBox.Selection,
                inputBox.GetCaretRect(),
                textBox!.GetScrollPercent(),
                textInput!.GetScrollPercent());
        }

        private void AssertStable(SurfaceSet before, SurfaceSet after)
        {
            Require(before.Equivalent(after),
                "Stable 120-frame window changed allocation attempts, swaps, retirements, texture identity or raster size.");
        }

        private void HandleLinkClicked(LinkClickedEventArgs args)
        {
            var physical = pointerAudit != null && pointerAudit.WasPhysicalPointerRecently();
            var payloadValid = args.Href == ExpectedLinkHref && args.Id == ExpectedLinkId;
            if (!physical || !payloadValid)
            {
                return;
            }
            if (highDpiInteractionPhase)
            {
                highDpiPhysicalLink = true;
            }
            else
            {
                baselinePhysicalLink = true;
            }
        }

        private void CompleteTerminal()
        {
            var visualStatus = humanVisualConfirmed ? "HUMAN_VISUAL_PASS" : "HUMAN_VISUAL_PENDING";
            var epochStatus = finalDeviceEpoch != 0 && finalDeviceEpoch != initialDeviceEpoch
                ? "DEVICE_EPOCH_EXERCISED"
                : "DEVICE_EPOCH_NOT_EXERCISED";
            var overall = automationPassed && humanVisualConfirmed && incompleteReasons.Count == 0
                ? "PASS"
                : "INCOMPLETE";
            var record = EnvironmentRecord("terminal", true, string.Join(";", incompleteReasons));
            record.automationStatus = automationPassed ? "AUTOMATION_PASS" : "AUTOMATION_FAIL";
            record.humanVisualStatus = visualStatus;
            record.overallStatus = overall;
            record.deviceEpochStatus = epochStatus;
            record.deviceEpoch = finalDeviceEpoch;
            record.linkPhysicalPointer = baselinePhysicalLink && highDpiPhysicalLink;
            record.linkPayloadValid = baselinePhysicalLink && highDpiPhysicalLink;
            record.imeCompositionObserved = imeCompositionObserved;
            record.imeCommitValid = textInput!.Text.Contains(ExpectedImeCommit);
            record.logicalStateStable = logicalStateStable;
            record.worldSpaceExact = worldSpaceExact;
            record.lastKnownGoodPreserved = lastKnownGoodPreserved;
            record.recoverySucceeded = recoverySucceeded;
            recorder!.Write(record);
            recorder.Dispose();
            Debug.Log("SCREEN_SPACE_HIDPI_HIDPI_RESULT " + overall + " log=" + recorder.LogPath);
            instructions = "Finished: " + overall + ". Preserve the JSONL and screenshots.";
            currentPhase = "terminal";
        }

        private void CompleteFailure(Exception exception)
        {
            try
            {
                if (recorder != null)
                {
                    var record = EnvironmentRecord("terminal", true, "Fail-closed harness exception.");
                    record.automationStatus = "AUTOMATION_FAIL";
                    record.humanVisualStatus = humanVisualConfirmed ? "HUMAN_VISUAL_PASS" : "HUMAN_VISUAL_PENDING";
                    record.overallStatus = "INCOMPLETE";
                    record.deviceEpochStatus = finalDeviceEpoch != 0 && finalDeviceEpoch != initialDeviceEpoch
                        ? "DEVICE_EPOCH_EXERCISED"
                        : "DEVICE_EPOCH_NOT_EXERCISED";
                    record.error = exception.ToString();
                    recorder.Write(record);
                    recorder.Dispose();
                }
            }
            finally
            {
                Debug.LogError("SCREEN_SPACE_HIDPI_HIDPI_RESULT INCOMPLETE " + exception);
                currentPhase = "failed";
                instructions = "Fail-closed. Preserve the JSONL and Player log.";
            }
        }

        private void WriteEnvironment(string phase, bool terminal, string detail)
        {
            recorder!.Write(EnvironmentRecord(phase, terminal, detail));
        }

        private ScreenSpaceHiDpiLogRecord EnvironmentRecord(string phase, bool terminal, string detail)
        {
            var displayIndex = targetCanvas != null ? targetCanvas.targetDisplay : 0;
            var display = displayIndex >= 0 && displayIndex < Display.displays.Length
                ? Display.displays[displayIndex]
                : Display.main;
            return new ScreenSpaceHiDpiLogRecord
            {
                phase = phase,
                terminal = terminal,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                displayCount = Display.displays.Length,
                targetDisplay = displayIndex,
                displaySystemWidth = display.systemWidth,
                displaySystemHeight = display.systemHeight,
                displayRenderWidth = display.renderingWidth,
                displayRenderHeight = display.renderingHeight,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenDpi = Screen.dpi,
                icuResourceSha256 = validatedIcuResourceSha256,
                detail = detail
            };
        }

        private void ValidateBackend()
        {
            var normalized = NormalizeBackend(expectedBackend);
            var valid = normalized == "d3d12" && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 ||
                        normalized == "metal" && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
            Require(valid,
                "Expected " + normalized + " but Unity is running " + SystemInfo.graphicsDeviceType + ".");
        }

        private void RequireConfigured()
        {
            Require(targetCanvas != null && targetCamera != null && textBox != null && textInput != null &&
                    slimText != null && worldSpaceTextBox != null && worldSpaceSlimText != null &&
                    pointerAudit != null && packageManifest != null,
                "Screen Space HiDPI fixture references are incomplete.");
        }

        private void AddIncomplete(string reason)
        {
            if (!incompleteReasons.Contains(reason))
            {
                incompleteReasons.Add(reason);
            }
        }

        private void RestoreRuntimeState()
        {
            if (linkListenerAttached && textBox != null)
            {
                textBox.onLinkClicked.RemoveListener(HandleLinkClicked);
                linkListenerAttached = false;
            }
            if (!runtimeStateCaptured)
            {
                return;
            }
            if (originalRenderSurfaceConfiguration != null)
            {
                MilestroConfiguration.Configuration.RenderSurface = originalRenderSurfaceConfiguration;
            }
            if (targetCanvas != null)
            {
                targetCanvas.targetDisplay = originalTargetDisplay;
            }
            if (targetCamera != null)
            {
                targetCamera.targetDisplay = originalTargetDisplay;
            }
            if (textBox != null)
            {
                textBox.rectTransform.sizeDelta = textBoxOriginalSize;
            }
            runtimeStateCaptured = false;
        }

        private static string NormalizeBackend(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "direct3d12")
            {
                return "d3d12";
            }
            if (normalized != "d3d12" && normalized != "metal")
            {
                throw new InvalidOperationException("Screen Space HiDPI backend must be d3d12 or metal.");
            }
            return normalized;
        }

        private static RenderSurfaceConfiguration CloneConfiguration(RenderSurfaceConfiguration source)
        {
            return new RenderSurfaceConfiguration
            {
                MaxScreenSpaceRasterScale = source.MaxScreenSpaceRasterScale,
                MinimumFallbackScale = source.MinimumFallbackScale,
                ScaleQuantum = source.ScaleQuantum,
                ScaleHysteresis = source.ScaleHysteresis,
                ConservativeMaxTextureEdge = source.ConservativeMaxTextureEdge,
                MaxPixelsPerSurface = source.MaxPixelsPerSurface,
                MaxBytesPerSurface = source.MaxBytesPerSurface,
                MaxGlobalBytes = source.MaxGlobalBytes,
                MaxTransitionBytes = source.MaxTransitionBytes,
                MaxAttemptsPerRequestAndEpoch = source.MaxAttemptsPerRequestAndEpoch
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private readonly struct InputStateSnapshot
        {
            private readonly InputBoxMetrics metrics;
            private readonly InputBoxCaret caret;
            private readonly InputBoxSelection selection;
            private readonly Rect caretRect;
            private readonly Vector2 textBoxScroll;
            private readonly Vector2 textInputScroll;

            internal InputStateSnapshot(InputBoxMetrics metrics,
                InputBoxCaret caret,
                InputBoxSelection selection,
                Rect caretRect,
                Vector2 textBoxScroll,
                Vector2 textInputScroll)
            {
                this.metrics = metrics;
                this.caret = caret;
                this.selection = selection;
                this.caretRect = caretRect;
                this.textBoxScroll = textBoxScroll;
                this.textInputScroll = textInputScroll;
            }

            internal bool Equivalent(InputStateSnapshot other)
            {
                return Close(metrics.Height, other.metrics.Height) &&
                       Close(metrics.ContentWidth, other.metrics.ContentWidth) &&
                       Close(metrics.ViewportWidth, other.metrics.ViewportWidth) &&
                       Close(metrics.ViewportHeight, other.metrics.ViewportHeight) &&
                       caret.Utf8Offset == other.caret.Utf8Offset &&
                       caret.Utf16Offset == other.caret.Utf16Offset &&
                       selection.AnchorUtf8 == other.selection.AnchorUtf8 &&
                       selection.FocusUtf8 == other.selection.FocusUtf8 &&
                       selection.HasSelection == other.selection.HasSelection &&
                       RectClose(caretRect, other.caretRect) &&
                       VectorClose(textBoxScroll, other.textBoxScroll) &&
                       VectorClose(textInputScroll, other.textInputScroll);
            }

            private static bool Close(float left, float right) => Mathf.Abs(left - right) <= 0.01f;
            private static bool VectorClose(Vector2 left, Vector2 right) =>
                Close(left.x, right.x) && Close(left.y, right.y);
            private static bool RectClose(Rect left, Rect right) =>
                Close(left.x, right.x) && Close(left.y, right.y) &&
                Close(left.width, right.width) && Close(left.height, right.height);
        }

        private readonly struct SurfaceSet
        {
            internal SurfaceSet(SurfaceProbe textBox, SurfaceProbe textInput, SurfaceProbe slimText)
            {
                TextBox = new StableSurfaceSnapshot(textBox);
                TextInput = new StableSurfaceSnapshot(textInput);
                SlimText = new StableSurfaceSnapshot(slimText);
            }

            internal StableSurfaceSnapshot TextBox { get; }
            internal StableSurfaceSnapshot TextInput { get; }
            internal StableSurfaceSnapshot SlimText { get; }
            internal bool AllUsable => TextBox.Usable && TextInput.Usable && SlimText.Usable;

            internal bool Equivalent(SurfaceSet other)
            {
                return TextBox.Equivalent(other.TextBox) &&
                       TextInput.Equivalent(other.TextInput) &&
                       SlimText.Equivalent(other.SlimText);
            }
        }

        private readonly struct StableSurfaceSnapshot
        {
            internal StableSurfaceSnapshot(SurfaceProbe probe)
            {
                Texture = probe.Texture;
                Width = probe.Width;
                Height = probe.Height;
                Counters = probe.Counters;
                DeviceEpoch = probe.DeviceEpoch;
            }

            private Texture? Texture { get; }
            private int Width { get; }
            private int Height { get; }
            private RenderSurfaceCounterSnapshot Counters { get; }
            internal ulong DeviceEpoch { get; }
            internal bool Usable => Texture != null && Width > 0 && Height > 0 && DeviceEpoch != 0;

            internal bool Equivalent(StableSurfaceSnapshot other)
            {
                return Texture == other.Texture && Width == other.Width && Height == other.Height &&
                       Counters.AllocationAttempts == other.Counters.AllocationAttempts &&
                       Counters.AllocationSuccesses == other.Counters.AllocationSuccesses &&
                       Counters.AllocationFailures == other.Counters.AllocationFailures &&
                       Counters.ValidationFailures == other.Counters.ValidationFailures &&
                       Counters.SuppressedAttempts == other.Counters.SuppressedAttempts &&
                       Counters.AtomicSwaps == other.Counters.AtomicSwaps &&
                       Counters.Retirements == other.Counters.Retirements;
            }
        }

        private readonly struct SurfaceProbe
        {
            private static readonly FieldInfo TextInputSurfaceField = RequireField(typeof(TextInput), "surface");
            private static readonly FieldInfo ProducerTargetField = RequireField(typeof(TextBoxRenderTextureProducer),
                "renderTarget");
            private static readonly FieldInfo SlimProducerTargetField = RequireField(typeof(SlimTextRenderTextureProducer),
                "renderTarget");
            private static readonly FieldInfo TextTargetSurfaceField = RequireField(typeof(TextBoxRenderTarget),
                "surface");
            private static readonly FieldInfo SlimTargetSurfaceField = RequireField(typeof(SlimTextRenderTarget),
                "surface");
            private static readonly FieldInfo ManagedSurfaceField = RequireField(typeof(ManagedRenderTextureSurface),
                "surface");
            private static readonly FieldInfo StableScaleField = RequireField(typeof(ManagedRenderTextureSurface),
                "stableDesiredScale");

            private SurfaceProbe(ManagedRenderTextureSurface managed, RectTransform rectTransform)
            {
                Managed = managed;
                RectTransform = rectTransform;
                Auto = ManagedSurfaceField.GetValue(managed) as UnityAutoRenderTextureSurface ??
                       throw new InvalidOperationException("Managed surface has no live UnityAutoRenderTextureSurface.");
            }

            private ManagedRenderTextureSurface Managed { get; }
            private UnityAutoRenderTextureSurface Auto { get; }
            private RectTransform RectTransform { get; }
            internal Texture? Texture => Managed.Texture;
            internal int Width => Managed.Width;
            internal int Height => Managed.Height;
            internal RenderSurfaceCounterSnapshot Counters => Auto.CounterSnapshot;
            internal ulong DeviceEpoch => Auto.DeviceEpoch;

            internal static SurfaceProbe FromTextInput(TextInput input)
            {
                var managed = TextInputSurfaceField.GetValue(input) as ManagedRenderTextureSurface ??
                              throw new InvalidOperationException("TextInput has no managed render surface.");
                return new SurfaceProbe(managed, input.rectTransform);
            }

            internal static SurfaceProbe FromProducer(RenderTextureProducer producer, RectTransform rectTransform)
            {
                object? target;
                FieldInfo targetSurface;
                if (producer is TextBoxRenderTextureProducer)
                {
                    target = ProducerTargetField.GetValue(producer);
                    targetSurface = TextTargetSurfaceField;
                }
                else if (producer is SlimTextRenderTextureProducer)
                {
                    target = SlimProducerTargetField.GetValue(producer);
                    targetSurface = SlimTargetSurfaceField;
                }
                else
                {
                    throw new InvalidOperationException("Unknown Screen Space HiDPI render texture producer.");
                }
                var managed = target != null
                    ? targetSurface.GetValue(target) as ManagedRenderTextureSurface
                    : null;
                if (managed == null)
                {
                    throw new InvalidOperationException("Producer has no managed render surface.");
                }
                return new SurfaceProbe(managed, rectTransform);
            }

            internal SurfaceSample Sample()
            {
                if (!ScreenSpaceRasterMetrics.TryMeasure(RectTransform, new Vector3[4], out var measurement))
                {
                    throw new InvalidOperationException("Could not measure actual framebuffer projection.");
                }
                var configuration = MilestroConfiguration.Configuration.RenderSurface;
                if (!(StableScaleField.GetValue(Managed) is float stableScale))
                {
                    throw new InvalidOperationException("Managed surface stable scale is unavailable.");
                }
                if (!RenderSurfacePolicy.TryQuantizeDesiredScale(measurement.DesiredScale,
                        configuration.ScaleQuantum,
                        configuration.ScaleHysteresis,
                        configuration.MaxScreenSpaceRasterScale,
                        stableScale,
                        out var quantized))
                {
                    throw new InvalidOperationException("Could not quantize actual framebuffer scale.");
                }
                var logical = measurement.LogicalSize;
                var textureDescriptor = new UnitySkiaRenderTextureDescriptor(logical.x, logical.y, Managed.ColorSpace);
                var descriptor = new RenderSurfaceDescriptor(Managed.Backend, textureDescriptor, 1);
                var backendEdge = Managed.Backend == UnitySkiaGraphicsBackend.OpenGLES ? 8192 : 16384;
                if (!RenderSurfacePolicy.TryBuildCandidatePlan(new RenderSurfaceRasterRequest(logical.x,
                            logical.y,
                            quantized),
                        new RenderSurfaceRuntimeCaps(SystemInfo.maxTextureSize, backendEdge),
                        configuration,
                        descriptor,
                        out var plan,
                        out var failure))
                {
                    throw new InvalidOperationException("Framebuffer candidate plan failed: " + failure + ".");
                }
                var effectiveScale = Managed.EffectiveRasterScale;
                if (float.IsNaN(effectiveScale) || float.IsInfinity(effectiveScale) || effectiveScale <= 0f ||
                    effectiveScale > plan.ClampedScale + 0.0001f)
                {
                    throw new InvalidOperationException("Actual effective scale is outside the checked candidate plan.");
                }
                var expectedWidth = checked((int)Math.Ceiling(logical.x * (double)effectiveScale));
                var expectedHeight = checked((int)Math.Ceiling(logical.y * (double)effectiveScale));
                if (Managed.Width != expectedWidth || Managed.Height != expectedHeight)
                {
                    throw new InvalidOperationException(
                        "Actual raster size does not match logical size times effective scale.");
                }
                var diagnostics = Auto.DiagnosticsSnapshot;
                if (diagnostics.DeviceEpoch != Auto.DeviceEpoch ||
                    diagnostics.Native.CurrentDeviceEpoch != Auto.DeviceEpoch ||
                    Mathf.Abs(diagnostics.EffectiveScale - effectiveScale) > 0.0001f)
                {
                    throw new InvalidOperationException("Native and managed render diagnostics are not coherent.");
                }
                if (diagnostics.Native.HasLastAcceptedSubmission &&
                    (diagnostics.Native.LastAcceptedRasterWidth <= 0 ||
                     diagnostics.Native.LastAcceptedRasterHeight <= 0 ||
                     diagnostics.Native.LastAcceptedEffectiveScale <= 0f ||
                     diagnostics.Native.LastAcceptedDeviceEpoch == 0))
                {
                    throw new InvalidOperationException("Native last-accepted render diagnostics are invalid.");
                }
                return new SurfaceSample(plan.RequestedScale,
                    plan.ClampedScale,
                    effectiveScale,
                    logical.x,
                    logical.y,
                    Managed.Width,
                    Managed.Height,
                    diagnostics.Counters,
                    diagnostics.Budget,
                    Auto.DeviceEpoch,
                    backendEdge);
            }

            private static FieldInfo RequireField(Type type, string name)
            {
                return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
                       throw new MissingFieldException(type.FullName, name);
            }
        }

        private readonly struct SurfaceSample
        {
            internal SurfaceSample(float requestedScale,
                float clampedScale,
                float effectiveScale,
                int logicalWidth,
                int logicalHeight,
                int rasterWidth,
                int rasterHeight,
                RenderSurfaceCounterSnapshot counters,
                RenderSurfaceBudgetSnapshot budget,
                ulong deviceEpoch,
                int backendMaxTextureEdge)
            {
                RequestedScale = requestedScale;
                ClampedScale = clampedScale;
                EffectiveScale = effectiveScale;
                LogicalWidth = logicalWidth;
                LogicalHeight = logicalHeight;
                RasterWidth = rasterWidth;
                RasterHeight = rasterHeight;
                Counters = counters;
                Budget = budget;
                DeviceEpoch = deviceEpoch;
                BackendMaxTextureEdge = backendMaxTextureEdge;
            }

            internal float RequestedScale { get; }
            internal float ClampedScale { get; }
            internal float EffectiveScale { get; }
            internal int LogicalWidth { get; }
            internal int LogicalHeight { get; }
            internal int RasterWidth { get; }
            internal int RasterHeight { get; }
            internal RenderSurfaceCounterSnapshot Counters { get; }
            internal RenderSurfaceBudgetSnapshot Budget { get; }
            internal ulong DeviceEpoch { get; }
            internal int BackendMaxTextureEdge { get; }
            internal bool IsHiDpi => EffectiveScale > 1f &&
                                      RasterWidth > LogicalWidth && RasterHeight > LogicalHeight;
        }
    }

    [Preserve]
    public sealed class ScreenSpaceHiDpiPhysicalPointerAudit : MonoBehaviour, IPointerDownHandler
    {
        private int lastPhysicalPointerFrame = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Input.GetMouseButton(0) || Input.touchCount > 0)
            {
                lastPhysicalPointerFrame = Time.frameCount;
            }
        }

        public bool WasPhysicalPointerRecently()
        {
            return Time.frameCount - lastPhysicalPointerFrame >= 0 &&
                   Time.frameCount - lastPhysicalPointerFrame <= 12;
        }
    }

    [Preserve]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScreenSpaceHiDpiSlimTextGraphic : RenderTextureGraphic
    {
        [SerializeField] private SlimTextRenderTextureProducer? producer;
        private long observedVersion = long.MinValue;

        public void Configure(SlimTextRenderTextureProducer value)
        {
            producer = value;
            Apply(force: true);
        }

        private void Update()
        {
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            if (producer == null || !producer.HasOutput)
            {
                return;
            }
            if (!force && observedVersion == producer.OutputVersion)
            {
                return;
            }
            Texture = producer.OutputTexture;
            UvRect = producer.OutputUvRect;
            observedVersion = producer.OutputVersion;
        }
    }
}
