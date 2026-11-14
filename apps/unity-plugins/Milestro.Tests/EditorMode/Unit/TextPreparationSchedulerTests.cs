using System;
using System.Reflection;
using Milestro.Components.Internal;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Canvas = UnityEngine.Canvas;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public sealed class TextPreparationSchedulerTests
    {
        [Test]
        public void QueueCoalescesChangesAndReadsFinalState()
        {
            var queue = new TextPreparationQueue();
            var state = "initial";
            var observed = "";
            var participant = new Participant(() => observed = state);
            queue.Enqueue(participant);
            state = "intermediate";
            queue.Enqueue(participant);
            state = "final";
            queue.Enqueue(participant);

            queue.Process();
            queue.Process();

            Assert.That(participant.Calls, Is.EqualTo(1));
            Assert.That(observed, Is.EqualTo("final"));
        }

        [Test]
        public void ReentrantChangesRemainPendingForNextPreparation()
        {
            var queue = new TextPreparationQueue();
            Participant participant = null!;
            participant = new Participant(() =>
            {
                queue.Enqueue(participant);
                queue.Process();
            });
            queue.Enqueue(participant);

            queue.Process();

            Assert.That(participant.Calls, Is.EqualTo(1));
            Assert.That(queue.Count, Is.EqualTo(1));
            queue.Process();
            Assert.That(participant.Calls, Is.EqualTo(2));
        }

        [Test]
        public void CancelledAndInactiveParticipantsDoNotPrepare()
        {
            var queue = new TextPreparationQueue();
            var cancelled = new Participant(() => { });
            var inactive = new Participant(() => { }) { IsPreparationActive = false };
            queue.Enqueue(cancelled);
            queue.Enqueue(inactive);
            queue.Remove(cancelled);

            queue.Process();

            Assert.That(cancelled.Calls, Is.Zero);
            Assert.That(inactive.Calls, Is.Zero);
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void ParagraphChangesPrepareFinalStateAtCanvasLayoutDeadline()
        {
            WithProducer((producer, target) =>
            {
                var baseline = target.RebuildCalls;
                producer.content = "first";
                producer.textStyle.size = 22f;
                producer.content = "final";
                producer.textStyle.size = 38f;

                Assert.That(target.RebuildCalls, Is.EqualTo(baseline));
                Canvas.ForceUpdateCanvases();

                Assert.That(target.RebuildCalls, Is.EqualTo(baseline + 1));
                Assert.That(target.LastContent, Is.EqualTo("final"));
                Assert.That(target.LastFontSize, Is.EqualTo(38f));
                Canvas.ForceUpdateCanvases();
                Assert.That(target.RebuildCalls, Is.EqualTo(baseline + 1));
            });
        }

        [Test]
        public void LayoutChangesArePreparedBeforeGraphicRebuild()
        {
            WithProducer((producer, target) =>
            {
                var baseline = target.RebuildCalls;
                var observedDuringGraphics = "";
                producer.content = "before layout";
                var probe = new CanvasProbe(producer.transform,
                    () => producer.content = "layout final",
                    () => observedDuringGraphics = target.LastContent);
                CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(probe);
                CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(probe);
                try
                {
                    Canvas.ForceUpdateCanvases();

                    Assert.That(target.RebuildCalls, Is.EqualTo(baseline + 1));
                    Assert.That(observedDuringGraphics, Is.EqualTo("layout final"));
                }
                finally
                {
                    CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(probe);
                }
            });
        }

        [Test]
        public void ExplicitSynchronousPreparationConsumesQueuedWork()
        {
            WithProducer((producer, target) =>
            {
                var baseline = target.RebuildCalls;
                producer.content = "synchronous query";

                producer.RebuildOutput(forceText: false);

                Assert.That(target.RebuildCalls, Is.EqualTo(baseline + 1));
                Assert.That(target.LastContent, Is.EqualTo("synchronous query"));
                Canvas.ForceUpdateCanvases();
                Assert.That(target.RebuildCalls, Is.EqualTo(baseline + 1));
            });
        }

        [Test]
        public void StandaloneProducerPreparesWithoutCanvasCallbacks()
        {
            WithProducer((producer, target) =>
            {
                var baseline = target.RebuildCalls;
                producer.content = "no canvas";

                InvokeProducerLifecycle(producer, "Update");

                Assert.That(target.RebuildCalls, Is.EqualTo(baseline + 1));
                Assert.That(target.LastContent, Is.EqualTo("no canvas"));
                Canvas.ForceUpdateCanvases();
                Assert.That(target.RebuildCalls, Is.EqualTo(baseline + 1));
            });
        }

        [Test]
        public void DisablingParagraphCancelsQueuedPreparation()
        {
            WithProducer((producer, target) =>
            {
                var baseline = target.RebuildCalls;
                producer.content = "pending";
                producer.enabled = false;
                InvokeProducerLifecycle(producer, "OnDisable");

                Canvas.ForceUpdateCanvases();

                Assert.That(target.RebuildCalls, Is.EqualTo(baseline));
                Assert.That(target.Disposed, Is.True);
            });
        }

        private static void WithProducer(Action<TextBoxRenderTextureProducer, CountingTarget> test)
        {
            var root = new GameObject("Text preparation scheduling test", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var producer = root.AddComponent<TextBoxRenderTextureProducer>();
                var target = new CountingTarget();
                producer.DisposeRenderTarget();
                producer.renderTargetFactory = () => target;
                producer.screenSpaceRasterization = false;
                root.SetActive(true);
                InvokeProducerLifecycle(producer, "OnEnable");
                test(producer, target);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void InvokeProducerLifecycle(TextBoxRenderTextureProducer producer, string methodName)
        {
            typeof(TextBoxRenderTextureProducer).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(producer, null);
        }

        private sealed class CanvasProbe : ICanvasElement
        {
            private readonly Action layout;
            private readonly Action graphics;
            public Transform transform { get; }

            internal CanvasProbe(Transform transform, Action layout, Action graphics)
            {
                this.transform = transform;
                this.layout = layout;
                this.graphics = graphics;
            }

            public void Rebuild(CanvasUpdate executing)
            {
                if (executing == CanvasUpdate.Layout)
                {
                    layout();
                }
                else if (executing == CanvasUpdate.PreRender)
                {
                    graphics();
                }
            }

            public void LayoutComplete() { }
            public void GraphicUpdateComplete() { }
            public bool IsDestroyed() => false;
        }

        private sealed class Participant : ITextPreparationParticipant
        {
            private readonly Action prepare;
            public bool IsPreparationActive { get; set; } = true;
            internal int Calls { get; private set; }

            internal Participant(Action prepare) => this.prepare = prepare;

            public void PrepareText()
            {
                ++Calls;
                prepare();
            }
        }

        private sealed class CountingTarget : ITextBoxRenderTarget
        {
            public Texture? OutputTexture => null;
            public Rect OutputUvRect => new Rect(0f, 0f, 1f, 1f);
            public int OutputWidth => 0;
            public int OutputHeight => 0;
            public bool HasOutput => false;
            public bool HasPendingWork { get; private set; } = true;
            public long OutputVersion => 0;
            public Vector2 ScrollOffset => Vector2.zero;
            public Vector2 ContentSize => Vector2.zero;
            public Vector2 ViewportSize => Vector2.zero;
            public Vector2 MaxScrollOffset => Vector2.zero;
            public TextBoxHorizontalScrollState HorizontalScrollState => default;
            public bool IsVertical => false;
            public Exception? LastLayoutError => null;
            internal int RebuildCalls { get; private set; }
            internal string LastContent { get; private set; } = "";
            internal float LastFontSize { get; private set; }
            internal bool Disposed { get; private set; }

            public event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>?
                RenderEventCompleted;

            public void MarkPropertiesChanged() => HasPendingWork = true;
            public void MarkContentChanged() => HasPendingWork = true;
            public void MarkPaintChanged() => HasPendingWork = true;
            public void InvalidateFailedOutput() { }

            public bool TryHitLink(Vector2 normalizedViewportPoint, out TextBoxLinkHit hit)
            {
                hit = default;
                return false;
            }

            public bool TryGetContentSnapshot(out TextBoxContentSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }

            public bool TryGetLinkGeometrySnapshot(out TextBoxLinkGeometrySnapshot snapshot,
                out long generation)
            {
                snapshot = default;
                generation = 0;
                return false;
            }

            public bool Rebuild(TextBoxRenderViewport viewport, ColorSpace colorSpace,
                TextBoxRenderTargetSettings settings, bool forceText, Object? logContext)
            {
                ++RebuildCalls;
                LastContent = settings.Content;
                LastFontSize = settings.TextStyle.size;
                HasPendingWork = false;
                return true;
            }

            public void Dispose() => Disposed = true;
        }
    }
}
