using System;
using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextBoxNoWrapHorizontalLayoutTests
    {
        [TestCase(TextAlign.Left, TextDirection.Ltr, 0f, 0f, 0f, 0f)]
        [TestCase(TextAlign.Center, TextDirection.Ltr, 20f, 12f, 0f, 0f)]
        [TestCase(TextAlign.Right, TextDirection.Ltr, 40f, 24f, 0f, 0f)]
        [TestCase(TextAlign.Start, TextDirection.Ltr, 0f, 0f, 0f, 0f)]
        [TestCase(TextAlign.End, TextDirection.Ltr, 40f, 24f, 0f, 0f)]
        [TestCase(TextAlign.Start, TextDirection.Rtl, 40f, 24f, 0f, 0f)]
        [TestCase(TextAlign.End, TextDirection.Rtl, 0f, 0f, 0f, 0f)]
        public void ShortContentUsesViewportForLogicalAlignment(TextAlign align,
            TextDirection direction,
            float expectedVisualLeft,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                direction,
                viewportWidth: 100f,
                contentWidth: 60f,
                layoutWidth: 124f);

            AssertLayout(result,
                60f,
                expectedVisualLeft,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Left, 0f, 0f, 0f)]
        [TestCase(TextAlign.Center, 32f, 0f, 0f)]
        [TestCase(TextAlign.Right, 64f, 0f, 0f)]
        public void ContentAtViewportWidthRemovesOnlySafetyAlignment(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 100f,
                layoutWidth: 164f);

            AssertLayout(result,
                100f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Left, 0f, 0f, 20f)]
        [TestCase(TextAlign.Center, 32f, 10f, 20f)]
        [TestCase(TextAlign.Right, 64f, 20f, 20f)]
        [TestCase(TextAlign.Start, 0f, 0f, 20f)]
        [TestCase(TextAlign.End, 64f, 20f, 20f)]
        public void NarrowOverflowDoesNotExposeSafetyPaddingAsScrollLtr(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 120f,
                layoutWidth: 184f);

            AssertLayout(result,
                120f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Start, 64f, 20f)]
        [TestCase(TextAlign.End, 0f, 0f)]
        public void NarrowOverflowResolvesStartEndForRtl(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Rtl,
                100f,
                120f,
                184f);

            AssertLayout(result,
                120f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                20f);
        }

        [TestCase(99.75f, 164f, 0.25f, 64f, 0f)]
        [TestCase(100.25f, 165f, 0f, 64.75f, 0.25f)]
        public void NearViewportBoundaryKeepsLogicalAndSafetyWidthsSeparate(float contentWidth,
            float layoutWidth,
            float expectedVisualLeft,
            float expectedLayoutOffset,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                contentWidth,
                layoutWidth);

            AssertLayout(result,
                contentWidth,
                expectedVisualLeft,
                expectedLayoutOffset,
                expectedMaxScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Left, 0f, 0f, 80f)]
        [TestCase(TextAlign.Center, 32f, 40f, 80f)]
        [TestCase(TextAlign.Right, 64f, 80f, 80f)]
        public void WideOverflowStartsAtPhysicalAlignmentAnchor(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 180f,
                layoutWidth: 244f);

            AssertLayout(result,
                180f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [Test]
        public void NonWideLayoutReturnsNoHorizontalOverride()
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(false,
                TextAlign.Right,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 180f,
                layoutWidth: 244f);

            AssertLayout(result, 0f, 0f, 0f, 0f, 0f);
        }

        [Test]
        public void EmptyAndNonFiniteInputsResolveToFiniteZeroState()
        {
            var empty = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity);
            AssertLayout(empty, 0f, 0f, 0f, 0f, 0f);

            var populated = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var state = default(TextBoxHorizontalScrollState)
                .Resolve(populated, TextBoxHorizontalScrollRequest.None)
                .WithUserRequest(float.NaN)
                .Resolve(populated, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(state, 0f, 20f, false);
        }

        [Test]
        public void ProducerKeepsOnePixelUserMoveAtTenMillionAcrossFixedRebuilds()
        {
            var layout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                10000100f,
                10000164f);
            var gameObject = new GameObject("TextBox large scroll test", typeof(RectTransform));
            gameObject.SetActive(false);
            var producer = gameObject.AddComponent<TextBoxRenderTextureProducer>();
            var target = new TrackingRenderTarget(layout);
            producer.DisposeRenderTarget();
            producer.renderTargetFactory = () => target;
            try
            {
                producer.ApplyHorizontalScrollState(default(TextBoxHorizontalScrollState)
                    .Resolve(layout, TextBoxHorizontalScrollRequest.None));
                producer.scrollX = 9999999f;

                producer.RebuildOutput(forceText: true);
                producer.RebuildOutput(forceText: false);

                Assert.That(target.RebuildCalls, Is.EqualTo(2));
                Assert.That(target.LastViewport.HasHorizontalScrollRequest, Is.True);
                Assert.That(producer.scrollX, Is.EqualTo(9999999f));
                AssertHorizontalState(producer.horizontalScrollState, 9999999f, 10000000f, false);
            }
            finally
            {
                producer.DisposeRenderTarget();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TextBoxTryScrollXDeliversOnePixelMoveAtTenMillionToProducerState()
        {
            var layout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                10000100f,
                10000164f);
            var gameObject = new GameObject("TextBox large scroll input test", typeof(RectTransform));
            gameObject.SetActive(false);
            var textBox = gameObject.AddComponent<TextBox>();
            var producer = gameObject.GetComponent<TextBoxRenderTextureProducer>();
            var target = new TrackingRenderTarget(layout);
            producer.DisposeRenderTarget();
            producer.renderTargetFactory = () => target;
            try
            {
                producer.ApplyHorizontalScrollState(default(TextBoxHorizontalScrollState)
                    .Resolve(layout, TextBoxHorizontalScrollRequest.None));
                producer.RebuildOutput(forceText: true);

                var consumed = InvokeNonPublic<bool>(textBox,
                    "TryScrollX",
                    producer,
                    -1f,
                    1f,
                    false,
                    false,
                    new ScrollElasticSettings());

                Assert.That(consumed, Is.True);
                Assert.That(target.RebuildCalls, Is.EqualTo(2));
                Assert.That(producer.scrollX, Is.EqualTo(9999999f));
                AssertHorizontalState(producer.horizontalScrollState, 9999999f, 10000000f, false);

                producer.RebuildOutput(forceText: false);
                Assert.That(target.RebuildCalls, Is.EqualTo(3));
                AssertHorizontalState(producer.horizontalScrollState, 9999999f, 10000000f, false);
            }
            finally
            {
                producer.DisposeRenderTarget();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FirstLayoutUsesAlignmentAnchorAndPaintComposesOffsetsIndependently()
        {
            var right = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var center = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Center,
                TextDirection.Ltr,
                100f,
                120f,
                184f);

            var rightScroll = default(TextBoxHorizontalScrollState)
                .Resolve(right, TextBoxHorizontalScrollRequest.None).ScrollX;
            var centerScroll = default(TextBoxHorizontalScrollState)
                .Resolve(center, TextBoxHorizontalScrollRequest.None).ScrollX;
            Assert.That(rightScroll, Is.EqualTo(20f));
            Assert.That(centerScroll, Is.EqualTo(10f));
            Assert.That(64f + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                right.LayoutAlignmentOffset,
                rightScroll,
                0f), Is.EqualTo(-20f));
            Assert.That(32f + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                center.LayoutAlignmentOffset,
                centerScroll,
                0f), Is.EqualTo(-10f));
            Assert.That(64f + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                right.LayoutAlignmentOffset,
                7f,
                3f), Is.EqualTo(-10f));
            Assert.That(TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(64f, 7f, float.NaN),
                Is.EqualTo(-71f));
        }

        [Test]
        public void FixedViewportKeepsLogicalAndElasticOffsetsSeparate()
        {
            var layout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var state = default(TextBoxHorizontalScrollState)
                .Resolve(layout, TextBoxHorizontalScrollRequest.None);
            var viewport = TextBoxRenderViewport.Fixed(new Vector2Int(100, 40),
                state,
                new Vector2(7f, 7f),
                new Vector2(3f, -2f));

            Assert.That(viewport.HasHorizontalScrollRequest, Is.True);
            Assert.That(viewport.HorizontalScrollRequest.Value, Is.EqualTo(7f));
            Assert.That(viewport.RequestedScrollY, Is.EqualTo(7f));
            Assert.That(viewport.VisualScrollOffset, Is.EqualTo(new Vector2(3f, -2f)));
            AssertHorizontalState(viewport.ResolveHorizontalScroll(layout), 7f, 20f, false);
        }

        [TestCase(TextAlign.Right, TextDirection.Ltr, 20f)]
        [TestCase(TextAlign.Center, TextDirection.Ltr, 10f)]
        [TestCase(TextAlign.End, TextDirection.Ltr, 20f)]
        [TestCase(TextAlign.Start, TextDirection.Rtl, 20f)]
        public void RepeatedFlowAndInvisiblePassesKeepDefaultAnchor(TextAlign align,
            TextDirection direction,
            float expectedAnchor)
        {
            var layout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                direction,
                100f,
                120f,
                184f);
            var gameObject = new GameObject("TextBox flow state test");
            gameObject.SetActive(false);
            var producer = gameObject.AddComponent<TextBoxRenderTextureProducer>();
            try
            {
                producer.scrollY = 13f;
                producer.flowMode = true;
                producer.SetVisibleRange(5f, 25f, 20f);

                for (var index = 0; index < 3; ++index)
                {
                    var visible = producer.CurrentViewport(new Vector2Int(100, 80));
                    Assert.That(visible.HasHorizontalScrollRequest, Is.False);
                    Assert.That(visible.RequestedScrollY, Is.EqualTo(5f));
                    producer.ApplyHorizontalScrollState(visible.ResolveHorizontalScroll(layout));
                    AssertHorizontalState(producer.horizontalScrollState,
                        expectedAnchor,
                        expectedAnchor,
                        true);
                    Assert.That(producer.scrollY, Is.EqualTo(13f));
                }

                producer.ClearVisibleRange();
                for (var index = 0; index < 3; ++index)
                {
                    var hidden = producer.CurrentViewport(new Vector2Int(100, 80));
                    Assert.That(hidden.HasHorizontalScrollRequest, Is.False);
                    producer.ApplyHorizontalScrollState(hidden.ResolveHorizontalScroll(layout));
                    AssertHorizontalState(producer.horizontalScrollState,
                        expectedAnchor,
                        expectedAnchor,
                        true);
                    Assert.That(producer.scrollY, Is.EqualTo(13f));
                }

                producer.flowMode = false;
                var fixedViewport = producer.CurrentViewport(new Vector2Int(100, 80));
                Assert.That(fixedViewport.HasHorizontalScrollRequest, Is.True);
                producer.ApplyHorizontalScrollState(fixedViewport.ResolveHorizontalScroll(layout));
                AssertHorizontalState(producer.horizontalScrollState,
                    expectedAnchor,
                    expectedAnchor,
                    true);
                Assert.That(fixedViewport.RequestedScrollY, Is.EqualTo(13f));
            }
            finally
            {
                producer.DisposeRenderTarget();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(TextAlign.Right, TextDirection.Ltr, 100f, 5f)]
        [TestCase(TextAlign.Center, TextDirection.Ltr, 50f, 2.5f)]
        [TestCase(TextAlign.End, TextDirection.Ltr, 100f, 5f)]
        [TestCase(TextAlign.Start, TextDirection.Rtl, 100f, 5f)]
        public void HiddenRelayoutReanchorsDefaultButPreservesAndClampsUserPosition(TextAlign align,
            TextDirection direction,
            float expandedAnchor,
            float narrowedAnchor)
        {
            var initial = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                direction,
                100f,
                120f,
                184f);
            var expanded = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                direction,
                80f,
                180f,
                244f);
            var narrowed = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                direction,
                100f,
                105f,
                169f);

            var defaultState = TextBoxRenderViewport.Invisible(new Vector2Int(100, 80), default)
                .ResolveHorizontalScroll(initial);
            defaultState = TextBoxRenderViewport.Invisible(new Vector2Int(80, 80), defaultState)
                .ResolveHorizontalScroll(expanded);
            AssertHorizontalState(defaultState, expandedAnchor, expandedAnchor, true);

            var userState = TextBoxRenderViewport.Invisible(new Vector2Int(100, 80), default)
                .ResolveHorizontalScroll(initial)
                .WithUserRequest(7f);
            userState = TextBoxRenderViewport.Invisible(new Vector2Int(80, 80), userState)
                .ResolveHorizontalScroll(expanded);
            AssertHorizontalState(userState, 7f, expandedAnchor, false);
            userState = TextBoxRenderViewport.Invisible(new Vector2Int(100, 80), userState)
                .ResolveHorizontalScroll(narrowed);
            AssertHorizontalState(userState, 5f, narrowedAnchor, false);
        }

        [Test]
        public void RuntimeAlignmentChangeReanchorsDefaultAndPreservesUserPosition()
        {
            var right = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var center = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Center,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var rtlEnd = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.End,
                TextDirection.Rtl,
                100f,
                120f,
                184f);

            var defaultState = default(TextBoxHorizontalScrollState)
                .Resolve(right, TextBoxHorizontalScrollRequest.None)
                .Resolve(center, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(defaultState, 10f, 10f, true);
            defaultState = defaultState.Resolve(rtlEnd, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(defaultState, 0f, 0f, true);

            var userState = default(TextBoxHorizontalScrollState)
                .Resolve(right, TextBoxHorizontalScrollRequest.None)
                .WithUserRequest(7f)
                .Resolve(center, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(userState, 7f, 10f, false);
            userState = userState.Resolve(rtlEnd, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(userState, 7f, 0f, false);
        }

        [Test]
        public void ContentAndViewportChangesReanchorDefaultIndependently()
        {
            var initial = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var contentChanged = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                180f,
                244f);
            var viewportChanged = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                80f,
                120f,
                184f);

            var initialDefault = default(TextBoxHorizontalScrollState)
                .Resolve(initial, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(initialDefault.Resolve(contentChanged,
                TextBoxHorizontalScrollRequest.None), 80f, 80f, true);
            AssertHorizontalState(initialDefault.Resolve(viewportChanged,
                TextBoxHorizontalScrollRequest.None), 40f, 40f, true);

            var initialUser = initialDefault.WithUserRequest(7f);
            AssertHorizontalState(initialUser.Resolve(contentChanged,
                TextBoxHorizontalScrollRequest.None), 7f, 80f, false);
            AssertHorizontalState(initialUser.Resolve(viewportChanged,
                TextBoxHorizontalScrollRequest.None), 7f, 40f, false);
        }

        [Test]
        public void WideNonWideTransitionKeepsDefaultOrUserClassification()
        {
            var wide = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var nonWide = TextBoxNoWrapHorizontalLayout.Resolve(false,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);

            var defaultState = default(TextBoxHorizontalScrollState)
                .Resolve(wide, TextBoxHorizontalScrollRequest.None)
                .Resolve(nonWide, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(defaultState, 0f, 0f, true);
            defaultState = defaultState.Resolve(wide, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(defaultState, 20f, 20f, true);

            var userState = default(TextBoxHorizontalScrollState)
                .Resolve(wide, TextBoxHorizontalScrollRequest.None)
                .WithUserRequest(7f)
                .Resolve(nonWide, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(userState, 0f, 0f, false);
            userState = userState.Resolve(wide, TextBoxHorizontalScrollRequest.None);
            AssertHorizontalState(userState, 0f, 20f, false);
        }

        [TestCase(false, 100f, true)]
        [TestCase(true, 7f, false)]
        public void ProducerRecreatesTargetThatConsumesRetainedHorizontalStateWithoutWritingFlowY(
            bool useUserPosition,
            float expectedScrollX,
            bool expectedFollowsDefault)
        {
            var gameObject = new GameObject("TextBox horizontal state test", typeof(RectTransform));
            gameObject.SetActive(false);
            var producer = gameObject.AddComponent<TextBoxRenderTextureProducer>();
            var initialLayout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var expandedLayout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                80f,
                180f,
                244f);
            var initialTarget = new TrackingRenderTarget(initialLayout);
            var recreatedTarget = new TrackingRenderTarget(expandedLayout);
            var createCount = 0;
            producer.DisposeRenderTarget();
            producer.renderTargetFactory = () => ++createCount == 1 ? initialTarget : recreatedTarget;
            try
            {
                var state = TextBoxRenderViewport.Invisible(new Vector2Int(100, 80), default)
                    .ResolveHorizontalScroll(initialLayout);
                producer.ApplyHorizontalScrollState(state);
                if (useUserPosition)
                {
                    producer.scrollX = 7f;
                }
                producer.scrollY = 13f;
                producer.flowMode = true;
                producer.SetVisibleRange(5f, 25f, 20f);
                producer.RebuildOutput(forceText: true);

                Assert.That(createCount, Is.EqualTo(1));
                Assert.That(initialTarget.RebuildCalls, Is.EqualTo(1));
                Assert.That(initialTarget.LastViewport.HasHorizontalScrollRequest, Is.False);

                InvokeNonPublic(producer, "OnDisable");
                Assert.That(initialTarget.Disposed, Is.True);
                Assert.That(producer.scrollX, Is.EqualTo(useUserPosition ? 7f : 20f));
                Assert.That(producer.scrollY, Is.EqualTo(13f));

                InvokeNonPublic(producer, "OnEnable");

                Assert.That(createCount, Is.EqualTo(2));
                Assert.That(recreatedTarget.RebuildCalls, Is.EqualTo(1));
                Assert.That(recreatedTarget.LastViewport.HasHorizontalScrollRequest, Is.False);
                Assert.That(recreatedTarget.LastViewport.RequestedScrollY, Is.EqualTo(5f));
                Assert.That(producer.scrollX, Is.EqualTo(expectedScrollX));
                Assert.That(producer.scrollY, Is.EqualTo(13f));
                AssertHorizontalState(producer.horizontalScrollState,
                    expectedScrollX,
                    100f,
                    expectedFollowsDefault);
            }
            finally
            {
                producer.DisposeRenderTarget();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class TrackingRenderTarget : ITextBoxRenderTarget
        {
            private readonly TextBoxNoWrapHorizontalLayout layout;

            internal TrackingRenderTarget(TextBoxNoWrapHorizontalLayout layout)
            {
                this.layout = layout;
            }

            public Texture? OutputTexture => null;
            public Rect OutputUvRect => new Rect(0f, 0f, 1f, 1f);
            public int OutputWidth => 0;
            public int OutputHeight => 0;
            public bool HasOutput => false;
            public long OutputVersion => 0;
            public Vector2 ScrollOffset { get; private set; }
            public Vector2 ContentSize => new Vector2(layout.ContentWidth, 0f);
            public Vector2 ViewportSize => Vector2.zero;
            public Vector2 MaxScrollOffset => new Vector2(layout.MaxScrollX, 0f);
            public TextBoxHorizontalScrollState HorizontalScrollState { get; private set; }
            internal TextBoxRenderViewport LastViewport { get; private set; }
            internal int RebuildCalls { get; private set; }
            internal bool Disposed { get; private set; }

            public event Action<Milestro.Skia.UnitySkiaRenderTextureSurface.RenderSubmissionStatus>?
                RenderEventCompleted;

            public void MarkPropertiesChanged()
            {
            }

            public void MarkPaintChanged()
            {
            }

            public bool Rebuild(TextBoxRenderViewport viewport,
                ColorSpace colorSpace,
                TextBoxRenderTargetSettings settings,
                bool forceText,
                UnityEngine.Object? logContext)
            {
                LastViewport = viewport;
                RebuildCalls++;
                HorizontalScrollState = viewport.ResolveHorizontalScroll(layout);
                ScrollOffset = new Vector2(HorizontalScrollState.ScrollX, viewport.RequestedScrollY);
                return true;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private static void AssertLayout(TextBoxNoWrapHorizontalLayout result,
            float contentWidth,
            float expectedVisualLeft,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            Assert.That(result.ContentWidth, Is.EqualTo(contentWidth).Within(0.0001f));
            Assert.That(result.LogicalVisualLeft, Is.EqualTo(expectedVisualLeft).Within(0.0001f));
            Assert.That(result.LayoutAlignmentOffset, Is.EqualTo(expectedLayoutOffset).Within(0.0001f));
            Assert.That(result.InitialScrollX, Is.EqualTo(expectedInitialScroll).Within(0.0001f));
            Assert.That(result.MaxScrollX, Is.EqualTo(expectedMaxScroll).Within(0.0001f));
        }

        private static void AssertHorizontalState(TextBoxHorizontalScrollState state,
            float expectedScroll,
            float expectedAnchor,
            bool expectedFollowsDefault)
        {
            Assert.That(state.HasLayout, Is.True);
            Assert.That(state.ScrollX, Is.EqualTo(expectedScroll).Within(0.0001f));
            Assert.That(state.DefaultAnchor, Is.EqualTo(expectedAnchor).Within(0.0001f));
            Assert.That(state.FollowsDefaultAnchor, Is.EqualTo(expectedFollowsDefault));
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            method.Invoke(target, null);
        }

        private static T InvokeNonPublic<T>(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            return (T)method.Invoke(target, arguments)!;
        }
    }
}
