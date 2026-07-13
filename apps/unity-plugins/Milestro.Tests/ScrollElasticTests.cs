using System;
using System.Collections.Generic;
using System.Reflection;
using Milestro.Components;
using Milestro.Input;
using Milestro.Model;
using Milestro.Util;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public class ScrollElasticTests
    {
        [Test]
        public void SettingsDefaultEnabledAndZeroVersionMigratesWithoutOverwritingSavedValues()
        {
            ScrollElasticSettings? missing = null;
            var resolvedMissing = ScrollElasticSettings.Resolve(ref missing);
            Assert.That(resolvedMissing, Is.SameAs(missing));
            Assert.That(resolvedMissing.Enabled, Is.True);

            var defaults = new ScrollElasticSettings();
            Assert.That(defaults.Enabled, Is.True);
            Assert.That(defaults.MaxOverscroll, Is.EqualTo(ScrollElasticSettings.DefaultMaxOverscroll));

            var migrated = Settings(false, 0.2f, 12f, 0.5f, 0.18f);
            SetSerializedVersion(migrated, 0);
            ((ISerializationCallbackReceiver)migrated).OnAfterDeserialize();
            Assert.That(migrated.Enabled, Is.True);
            Assert.That(migrated.Resistance, Is.EqualTo(ScrollElasticSettings.DefaultResistance));
            Assert.That(migrated.MaxOverscroll, Is.EqualTo(ScrollElasticSettings.DefaultMaxOverscroll));
            Assert.That(migrated.ReturnDurationSeconds,
                Is.EqualTo(ScrollElasticSettings.DefaultReturnDurationSeconds));
            Assert.That(migrated.ReleaseDelaySeconds,
                Is.EqualTo(ScrollElasticSettings.DefaultReleaseDelaySeconds));

            var saved = Settings(false, 0.2f, 12f, 0.5f, 0.18f);
            ((ISerializationCallbackReceiver)saved).OnAfterDeserialize();
            saved.Validate();
            Assert.That(saved.Enabled, Is.False);
            Assert.That(saved.Resistance, Is.EqualTo(0.2f));
            Assert.That(saved.MaxOverscroll, Is.EqualTo(12f));
            Assert.That(saved.ReturnDurationSeconds, Is.EqualTo(0.5f));
            Assert.That(saved.ReleaseDelaySeconds, Is.EqualTo(0.18f));
        }

        [Test]
        public void OwnerSettingsRecoverMissingSerializedValues()
        {
            var textBoxObject = new GameObject("TextBox elastic settings");
            var textInputObject = new GameObject("TextInput elastic settings");
            var scrollRectObject = new GameObject("ScrollRect elastic settings");
            textBoxObject.SetActive(false);
            textInputObject.SetActive(false);
            scrollRectObject.SetActive(false);

            try
            {
                var textBox = textBoxObject.AddComponent<TextBox>();
                SetOwnerSettings(textBox, null);
                Assert.That(textBox.scrollElastic.Enabled, Is.True);

                var zeroVersion = Settings(false, 0.2f, 12f, 0.5f, 0.18f);
                SetSerializedVersion(zeroVersion, 0);
                var textInput = textInputObject.AddComponent<TextInput>();
                SetOwnerSettings(textInput, zeroVersion);
                Assert.That(textInput.scrollElastic.Enabled, Is.True);
                Assert.That(textInput.scrollElastic.MaxOverscroll,
                    Is.EqualTo(ScrollElasticSettings.DefaultMaxOverscroll));

                var scrollRect = scrollRectObject.AddComponent<MilestroScrollRect>();
                SetOwnerSettings(scrollRect, null);
                Assert.That(scrollRect.scrollElastic.Enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(textBoxObject);
                UnityEngine.Object.DestroyImmediate(textInputObject);
                UnityEngine.Object.DestroyImmediate(scrollRectObject);
            }
        }

        [Test]
        public void ScrollRectVisualOffsetLifecycleIsExactAcrossBothOverflowingAxes()
        {
            var ownerObject = new GameObject("Overflowing Elastic ScrollRect", typeof(RectTransform));
            var contentObject = new GameObject("Content", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var ownerRect = ownerObject.GetComponent<RectTransform>();
                var content = contentObject.GetComponent<RectTransform>();
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
                content.SetParent(ownerObject.transform, false);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300f);
                var logicalPosition = new Vector2(24f, -32f);
                content.anchoredPosition = logicalPosition;
                owner.content = content;

                var maxScrollX = InvokePrivate<float>(owner, "MaxScrollX");
                var maxScrollY = InvokePrivate<float>(owner, "MaxScrollY");
                Assert.That(maxScrollX, Is.GreaterThan(ScrollElasticAxis.VisualEpsilon));
                Assert.That(maxScrollY, Is.GreaterThan(ScrollElasticAxis.VisualEpsilon));

                var settings = Settings(true, 0.5f, 96f, 0.24f, 0.08f);
                var x = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticX");
                var y = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY");
                x.Apply(maxScrollX, maxScrollX, 24f, settings, out _);
                y.Apply(0f, maxScrollY, -16f, settings, out _);
                var expectedVisualDelta = new Vector2(-x.Offset, y.Offset);
                Assert.That(x.IsActive, Is.True);
                Assert.That(y.IsActive, Is.True);

                for (var i = 0; i < 10000; ++i)
                {
                    InvokePrivate(owner, "ApplyVisualOffset");
                    Assert.That(content.anchoredPosition, Is.EqualTo(logicalPosition + expectedVisualDelta));
                    Assert.That(GetPrivateField<RectTransform?>(owner, "appliedVisualContent"),
                        Is.SameAs(content));
                    Assert.That(GetPrivateField<Vector2>(owner, "appliedVisualBasePosition"),
                        Is.EqualTo(logicalPosition));
                    Assert.That(GetPrivateField<Vector2>(owner, "appliedVisualDelta"),
                        Is.EqualTo(expectedVisualDelta));

                    InvokePrivate(owner, "RemoveVisualOffset");
                    Assert.That(content.anchoredPosition, Is.EqualTo(logicalPosition));
                }

                Assert.That(GetPrivateField<RectTransform?>(owner, "appliedVisualContent"), Is.Null);
                Assert.That(GetPrivateField<Vector2>(owner, "appliedVisualBasePosition"),
                    Is.EqualTo(Vector2.zero));
                Assert.That(GetPrivateField<Vector2>(owner, "appliedVisualDelta"),
                    Is.EqualTo(Vector2.zero));
                Assert.That(x.IsActive, Is.True);
                Assert.That(y.IsActive, Is.True);
            }
            finally
            {
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectVisualOffsetRemovalPreservesExternalMovementAndSettles()
        {
            var ownerObject = new GameObject("Externally moved Elastic ScrollRect", typeof(RectTransform));
            var contentObject = new GameObject("Content", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var ownerRect = ownerObject.GetComponent<RectTransform>();
                var content = contentObject.GetComponent<RectTransform>();
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
                content.SetParent(ownerObject.transform, false);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300f);
                var logicalPosition = new Vector2(24f, -32f);
                content.anchoredPosition = logicalPosition;
                owner.content = content;

                var maxScrollX = InvokePrivate<float>(owner, "MaxScrollX");
                var maxScrollY = InvokePrivate<float>(owner, "MaxScrollY");
                Assert.That(maxScrollX, Is.GreaterThan(ScrollElasticAxis.VisualEpsilon));
                Assert.That(maxScrollY, Is.GreaterThan(ScrollElasticAxis.VisualEpsilon));

                var settings = Settings(true, 0.5f, 96f, 0.24f, 0.08f);
                var x = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticX");
                var y = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY");
                var releaseX = GetPrivateField<ScrollElasticReleasePolicy>(owner, "scrollElasticReleaseX");
                var releaseY = GetPrivateField<ScrollElasticReleasePolicy>(owner, "scrollElasticReleaseY");
                x.Apply(maxScrollX, maxScrollX, 24f, settings, out _);
                y.Apply(0f, maxScrollY, -16f, settings, out _);
                releaseX.Observe(DeltaOnly(), 1d, settings.ReleaseDelaySeconds);
                releaseY.Observe(DeltaOnly(), 1d, settings.ReleaseDelaySeconds);
                var visualDelta = new Vector2(-x.Offset, y.Offset);
                InvokePrivate(owner, "ApplyVisualOffset");
                Assert.That(content.anchoredPosition, Is.EqualTo(logicalPosition + visualDelta));

                var externalDelta = new Vector2(4f, -3f);
                content.anchoredPosition += externalDelta;
                InvokePrivate(owner, "RemoveVisualOffset");

                Assert.That(content.anchoredPosition, Is.EqualTo(logicalPosition + externalDelta));
                Assert.That(x.Offset, Is.Zero);
                Assert.That(y.Offset, Is.Zero);
                Assert.That(releaseX.IsPending, Is.False);
                Assert.That(releaseY.IsPending, Is.False);
                Assert.That(GetPrivateField<RectTransform?>(owner, "appliedVisualContent"), Is.Null);
                Assert.That(GetPrivateField<Vector2>(owner, "appliedVisualDelta"),
                    Is.EqualTo(Vector2.zero));
            }
            finally
            {
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectDragDeltaMapsToLogicalScrollDirections()
        {
            Assert.That(MilestroScrollRect.DragDeltaToContentOffset(new Vector2(-6f, 8f)),
                Is.EqualTo(new Vector2(6f, 8f)));
            Assert.That(MilestroScrollRect.DragDeltaToContentOffset(new Vector2(6f, -8f)),
                Is.EqualTo(new Vector2(-6f, -8f)));
        }

        [Test]
        public void AllScrollOwnersKeepDirectHorizontalDeltaInLogicalDirection()
        {
            var textBoxObject = new GameObject("TextBox direct X");
            var textInputObject = new GameObject("TextInput direct X");
            var scrollRectObject = new GameObject("ScrollRect direct X", typeof(RectTransform));
            textBoxObject.SetActive(false);
            textInputObject.SetActive(false);
            scrollRectObject.SetActive(false);

            try
            {
                var owners = new object[]
                {
                    textBoxObject.AddComponent<TextBox>(),
                    textInputObject.AddComponent<TextInput>(),
                    scrollRectObject.AddComponent<MilestroScrollRect>()
                };

                foreach (var owner in owners)
                {
                    var axisLock = GetPrivateField<ScrollAxisLock>(owner, "scrollAxisLock");
                    AssertDirectHorizontalDirection(axisLock, 4f);
                    AssertDirectHorizontalDirection(axisLock, -4f);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(textBoxObject);
                UnityEngine.Object.DestroyImmediate(textInputObject);
                UnityEngine.Object.DestroyImmediate(scrollRectObject);
            }
        }

        [Test]
        public void DirectXFixDoesNotChangeFreeVerticalForceHorizontalOrRawResidualDirections()
        {
            var free = new ScrollAxisLock(1f, 0f, 1.2f, 0f, 0f);
            Assert.That(free.Resolve(new Vector2(4f, 3.5f),
                    false,
                    out var freeContent,
                    out var freeRaw),
                Is.EqualTo(ScrollAxis.Free));
            Assert.That(freeContent, Is.EqualTo(new Vector2(4f, -3.5f)));
            Assert.That(freeRaw, Is.EqualTo(new Vector2(4f, 3.5f)));

            var vertical = new ScrollAxisLock(1f, 0f, 1.2f, 0f, 0f);
            Assert.That(vertical.Resolve(new Vector2(0f, 4f),
                    false,
                    out var verticalContent,
                    out var verticalRaw),
                Is.EqualTo(ScrollAxis.Vertical));
            Assert.That(verticalContent, Is.EqualTo(new Vector2(0f, -4f)));
            Assert.That(verticalRaw, Is.EqualTo(new Vector2(0f, 4f)));

            var forced = new ScrollAxisLock(1f, 0f, 1.2f, 0f, 0f);
            Assert.That(forced.Resolve(new Vector2(0f, 4f),
                    true,
                    out var forcedContent,
                    out var forcedRaw),
                Is.EqualTo(ScrollAxis.Horizontal));
            Assert.That(forcedContent, Is.EqualTo(new Vector2(-4f, 0f)));
            Assert.That(forcedRaw, Is.EqualTo(new Vector2(0f, 4f)));

            Assert.That(InvokePrivateStatic<Vector2>(typeof(MilestroScrollRect),
                    "ApplyUnusedX",
                    new object[] { freeRaw, freeRaw, 2f, 4f }),
                Is.EqualTo(new Vector2(2f, 3.5f)));
            Assert.That(InvokePrivateStatic<Vector2>(typeof(MilestroScrollRect),
                    "ApplyUnusedX",
                    new object[] { forcedRaw, forcedRaw, -2f, -4f }),
                Is.EqualTo(new Vector2(0f, 2f)));
        }

        [Test]
        public void ScrollRectDirectXMovesLogicalPositionAndStretchesTheMatchingEdge()
        {
            var ownerObject = new GameObject("Direct X ScrollRect", typeof(RectTransform));
            var contentObject = new GameObject("Content", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var ownerRect = ownerObject.GetComponent<RectTransform>();
                var content = contentObject.GetComponent<RectTransform>();
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
                content.SetParent(ownerObject.transform, false);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300f);
                owner.content = content;
                owner.smoothScroll = false;
                owner.scrollWheelStepPixels = 1f;
                ownerObject.SetActive(true);

                owner.SetLogicalHorizontalNormalizedPosition(0.5f);
                var beforePositive = owner.GetLogicalHorizontalNormalizedPosition();
                owner.OnScroll(PointerEvent(new Vector2(4f, 0f)));
                Assert.That(owner.GetLogicalHorizontalNormalizedPosition(), Is.GreaterThan(beforePositive));

                GetPrivateField<ScrollAxisLock>(owner, "scrollAxisLock").Reset();
                var beforeNegative = owner.GetLogicalHorizontalNormalizedPosition();
                owner.OnScroll(PointerEvent(new Vector2(-4f, 0f)));
                Assert.That(owner.GetLogicalHorizontalNormalizedPosition(), Is.LessThan(beforeNegative));

                InvokePrivate(owner, "SettleElastic");
                GetPrivateField<ScrollAxisLock>(owner, "scrollAxisLock").Reset();
                owner.SetLogicalHorizontalNormalizedPosition(1f);
                var rightLogicalPosition = content.anchoredPosition;
                owner.OnScroll(PointerEvent(new Vector2(4f, 0f)));
                var x = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticX");
                Assert.That(x.Offset, Is.GreaterThan(0f));
                InvokePrivate(owner, "ApplyVisualOffset");
                Assert.That(content.anchoredPosition.x, Is.LessThan(rightLogicalPosition.x));
                InvokePrivate(owner, "RemoveVisualOffset");
                Assert.That(content.anchoredPosition, Is.EqualTo(rightLogicalPosition));

                InvokePrivate(owner, "SettleElastic");
                GetPrivateField<ScrollAxisLock>(owner, "scrollAxisLock").Reset();
                owner.SetLogicalHorizontalNormalizedPosition(0f);
                var leftLogicalPosition = content.anchoredPosition;
                owner.OnScroll(PointerEvent(new Vector2(-4f, 0f)));
                Assert.That(x.Offset, Is.LessThan(0f));
                InvokePrivate(owner, "ApplyVisualOffset");
                Assert.That(content.anchoredPosition.x, Is.GreaterThan(leftLogicalPosition.x));
                InvokePrivate(owner, "RemoveVisualOffset");
                Assert.That(content.anchoredPosition, Is.EqualTo(leftLogicalPosition));
            }
            finally
            {
                ownerObject.SetActive(false);
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectContentReplacementUnappliesOldContentAndSettlesState()
        {
            var ownerObject = new GameObject("Elastic ScrollRect", typeof(RectTransform));
            var oldContentObject = new GameObject("Old content", typeof(RectTransform));
            var newContentObject = new GameObject("New content", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var ownerRect = ownerObject.GetComponent<RectTransform>();
                var oldContent = oldContentObject.GetComponent<RectTransform>();
                var newContent = newContentObject.GetComponent<RectTransform>();
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
                ownerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100f);
                oldContent.SetParent(ownerObject.transform, false);
                newContent.SetParent(ownerObject.transform, false);
                oldContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
                oldContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300f);
                newContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
                newContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300f);

                var logicalPosition = new Vector2(20f, -30f);
                var newContentPosition = new Vector2(-11f, 13f);
                oldContent.anchoredPosition = logicalPosition;
                newContent.anchoredPosition = newContentPosition;
                owner.content = oldContent;

                var maxScrollX = InvokePrivate<float>(owner, "MaxScrollX");
                var axis = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticX");
                axis.Apply(maxScrollX,
                    maxScrollX,
                    30f,
                    Settings(true, 0.5f, 96f, 0.24f, 0.08f),
                    out _);
                InvokePrivate(owner, "ApplyVisualOffset");
                var visualPosition = logicalPosition + new Vector2(-axis.Offset, 0f);
                Assert.That(oldContent.anchoredPosition, Is.EqualTo(visualPosition));
                Assert.That(GetPrivateField<RectTransform?>(owner, "appliedVisualContent"),
                    Is.SameAs(oldContent));

                ((ScrollRect)owner).content = newContent;
                InvokePrivate(owner, "RemoveVisualOffset");

                Assert.That(oldContent.anchoredPosition, Is.EqualTo(logicalPosition));
                Assert.That(newContent.anchoredPosition, Is.EqualTo(newContentPosition));
                Assert.That(axis.Offset, Is.Zero);
                Assert.That(GetPrivateField<RectTransform?>(owner, "appliedVisualContent"), Is.Null);
                Assert.That(GetPrivateField<Vector2>(owner, "appliedVisualDelta"),
                    Is.EqualTo(Vector2.zero));
            }
            finally
            {
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectDisableSafetyNetUnappliesAndRestoresMovementType()
        {
            var ownerObject = new GameObject("Disabled Elastic ScrollRect", typeof(RectTransform));
            var contentObject = new GameObject("Content", typeof(RectTransform));
            ownerObject.SetActive(false);

            try
            {
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var content = contentObject.GetComponent<RectTransform>();
                content.SetParent(ownerObject.transform, false);
                ((ScrollRect)owner).content = content;
                var logicalPosition = new Vector2(-14f, 22f);
                var visualDelta = new Vector2(6f, -9f);
                content.anchoredPosition = logicalPosition + visualDelta;
                var axis = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY");
                axis.Apply(100f, 100f, 30f, Settings(true, 0.5f, 96f, 0.24f, 0.08f), out _);
                SetPrivateField(owner, "observedContent", content);
                SetPrivateField(owner, "appliedVisualContent", content);
                SetPrivateField(owner, "appliedVisualBasePosition", logicalPosition);
                SetPrivateField(owner, "appliedVisualDelta", visualDelta);
                SetPrivateField(owner, "movementTypeOverridden", true);
                SetPrivateField(owner, "savedMovementType", ScrollRect.MovementType.Elastic);
                owner.movementType = ScrollRect.MovementType.Clamped;

                InvokePrivate(owner, "OnDisable");

                Assert.That(content.anchoredPosition, Is.EqualTo(logicalPosition));
                Assert.That(axis.Offset, Is.Zero);
                Assert.That(owner.movementType, Is.EqualTo(ScrollRect.MovementType.Elastic));
                Assert.That(GetPrivateField<bool>(owner, "movementTypeOverridden"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectCustomElasticOwnsAndRestoresMovementType()
        {
            var ownerObject = new GameObject("Movement owner", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                owner.movementType = ScrollRect.MovementType.Elastic;

                ownerObject.SetActive(true);
                Assert.That(owner.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));

                owner.scrollElastic.Enabled = false;
                InvokePrivate(owner, "EnsureSingleMotionOwner");
                Assert.That(owner.movementType, Is.EqualTo(ScrollRect.MovementType.Elastic));
            }
            finally
            {
                ownerObject.SetActive(false);
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectEnabledSettingsOwnClampedMovementAcrossCapabilities()
        {
            var ownerObject = new GameObject("Capability movement owner", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                var unsupportedRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.Unsupported,
                    eventData => new HybridScrollInput(eventData.scrollDelta, default)));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                owner.movementType = ScrollRect.MovementType.Elastic;

                ownerObject.SetActive(true);
                Assert.That(owner.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(GetPrivateField<ScrollRect.MovementType>(owner, "savedMovementType"),
                    Is.EqualTo(ScrollRect.MovementType.Elastic));
                Assert.That(GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticX").Offset, Is.Zero);
                Assert.That(GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY").Offset, Is.Zero);

                unsupportedRegistration.Dispose();
                using var deltaOnlyRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                InvokePrivate(owner, "EnsureSingleMotionOwner");
                Assert.That(owner.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(GetPrivateField<ScrollRect.MovementType>(owner, "savedMovementType"),
                    Is.EqualTo(ScrollRect.MovementType.Elastic));

                deltaOnlyRegistration.Dispose();
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                InvokePrivate(owner, "EnsureSingleMotionOwner");
                Assert.That(owner.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(GetPrivateField<ScrollRect.MovementType>(owner, "savedMovementType"),
                    Is.EqualTo(ScrollRect.MovementType.Elastic));

                owner.scrollElastic.Enabled = false;
                InvokePrivate(owner, "EnsureSingleMotionOwner");
                Assert.That(owner.movementType, Is.EqualTo(ScrollRect.MovementType.Elastic));
            }
            finally
            {
                ownerObject.SetActive(false);
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectNonLeftDragDoesNotChangeExistingElasticState()
        {
            var ownerObject = new GameObject("Non-left drag owner", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                ownerObject.SetActive(true);
                var axis = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY");
                var release = GetPrivateField<ScrollElasticReleasePolicy>(owner, "scrollElasticReleaseY");
                var settings = Settings(true, 0.5f, 96f, 0.24f, 0.08f);
                axis.Apply(100f, 100f, 30f, settings, out _);
                release.Observe(DeltaOnly(), 1d, settings.ReleaseDelaySeconds);
                var originalOffset = axis.Offset;

                foreach (var button in new[]
                         {
                             PointerEventData.InputButton.Right,
                             PointerEventData.InputButton.Middle
                         })
                {
                    var eventData = new PointerEventData(null!)
                    {
                        button = button,
                        delta = new Vector2(12f, -8f),
                        position = new Vector2(40f, 30f)
                    };
                    owner.OnInitializePotentialDrag(eventData);
                    owner.OnBeginDrag(eventData);
                    owner.OnDrag(eventData);
                    owner.OnEndDrag(eventData);

                    Assert.That(axis.Offset, Is.EqualTo(originalOffset));
                    Assert.That(release.IsPending, Is.True);
                    Assert.That(GetPrivateField<PointerEventData?>(owner, "activeDragEventData"), Is.Null);
                    Assert.That(GetPrivateField<bool>(owner, "customDragActive"), Is.False);
                    Assert.That(GetPrivateFieldFromHierarchy<bool>(owner, "m_Dragging"), Is.False);
                }
            }
            finally
            {
                ownerObject.SetActive(false);
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectCancelEndsBaseLeftDragAndAllowsNextDrag()
        {
            var ownerObject = new GameObject("Canceled drag owner", typeof(RectTransform));
            var contentObject = new GameObject("Content", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var content = contentObject.GetComponent<RectTransform>();
                content.SetParent(ownerObject.transform, false);
                ((ScrollRect)owner).content = content;
                ownerObject.SetActive(true);
                var firstDrag = LeftDragEvent();

                owner.OnInitializePotentialDrag(firstDrag);
                owner.OnBeginDrag(firstDrag);
                owner.OnDrag(firstDrag);
                Assert.That(GetPrivateFieldFromHierarchy<bool>(owner, "m_Dragging"), Is.True);
                Assert.That(GetPrivateField<PointerEventData?>(owner, "activeDragEventData"),
                    Is.SameAs(firstDrag));
                Assert.That(GetPrivateField<bool>(owner, "customDragActive"), Is.True);

                var axis = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY");
                axis.Apply(100f, 100f, 30f, Settings(true, 0.5f, 96f, 0.24f, 0.08f), out _);
                owner.OnCancel(new BaseEventData(null!));

                Assert.That(GetPrivateFieldFromHierarchy<bool>(owner, "m_Dragging"), Is.False);
                Assert.That(GetPrivateField<PointerEventData?>(owner, "activeDragEventData"), Is.Null);
                Assert.That(GetPrivateField<bool>(owner, "customDragActive"), Is.False);
                Assert.That(owner.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(GetPrivateField<ScrollElasticReleasePolicy>(owner, "scrollElasticReleaseY").IsPending,
                    Is.True);

                var secondDrag = LeftDragEvent();
                owner.OnInitializePotentialDrag(secondDrag);
                owner.OnBeginDrag(secondDrag);
                Assert.That(GetPrivateFieldFromHierarchy<bool>(owner, "m_Dragging"), Is.True);
                Assert.That(GetPrivateField<PointerEventData?>(owner, "activeDragEventData"),
                    Is.SameAs(secondDrag));
                owner.OnEndDrag(secondDrag);
                Assert.That(GetPrivateFieldFromHierarchy<bool>(owner, "m_Dragging"), Is.False);
                Assert.That(GetPrivateField<PointerEventData?>(owner, "activeDragEventData"), Is.Null);
            }
            finally
            {
                ownerObject.SetActive(false);
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectIdleRenderGuardDoesNotInspectParentHierarchy()
        {
            var ownerObject = new GameObject("Idle Elastic ScrollRect", typeof(RectTransform));
            ownerObject.SetActive(false);

            try
            {
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var scratch = GetPrivateField<List<MonoBehaviour>>(owner, "parentScrollHandlerScratch");
                scratch.Add(owner);

                for (var i = 0; i < 10; ++i)
                {
                    InvokePrivate(owner, "ApplyVisualOffset");
                }

                Assert.That(scratch, Has.Count.EqualTo(1));
                Assert.That(scratch[0], Is.SameAs(owner));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ScrollRectRenderGuardSettlesStateWithoutOverflow()
        {
            var ownerObject = new GameObject("No-overflow Elastic ScrollRect", typeof(RectTransform));
            ownerObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                var owner = ownerObject.AddComponent<MilestroScrollRect>();
                var axis = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY");
                axis.Apply(100f, 100f, 30f, Settings(true, 0.5f, 96f, 0.24f, 0.08f), out _);
                Assert.That(axis.IsActive, Is.True);

                InvokePrivate(owner, "ApplyVisualOffset");

                Assert.That(axis.Offset, Is.Zero);
                Assert.That(axis.IsActive, Is.False);
            }
            finally
            {
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ActiveParentScrollHandlerDisablesChildElasticRegardlessOfOverflow()
        {
            var parent = new GameObject("Parent ScrollRect", typeof(RectTransform), typeof(ScrollRect));
            var child = new GameObject("Child scroll owner", typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);

            try
            {
                var parentScrollRect = parent.GetComponent<ScrollRect>();
                var scratch = new List<MonoBehaviour>();
                Assert.That(ScrollEventUtil.HasActiveParentScrollHandler(child.transform, scratch), Is.True);
                Assert.That(scratch, Is.Empty);

                parentScrollRect.enabled = false;
                Assert.That(ScrollEventUtil.HasActiveParentScrollHandler(child.transform, scratch), Is.False);
                Assert.That(scratch, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TextInputElasticPresentationDeltaUsesLocalCoordinates()
        {
            var gameObject = new GameObject("Scaled TextInput transform", typeof(RectTransform));
            try
            {
                var rectTransform = gameObject.GetComponent<RectTransform>();
                rectTransform.localScale = new Vector3(2f, 3f, 1f);
                var settings = Settings(true, 0.5f, 96f, 0.24f, 0.08f);
                var x = new ScrollElasticAxis();
                var y = new ScrollElasticAxis();
                x.Apply(100f, 100f, 30f, settings, out _);
                y.Apply(0f, 100f, -30f, settings, out _);
                var elasticOffset = new Vector2(x.Offset, y.Offset);
                var contentDelta = TextInput.ResolveElasticContentPresentationDelta(elasticOffset);
                var localDelta = TextInput.ResolveElasticLocalPresentationDelta(elasticOffset);
                Assert.That(contentDelta, Is.EqualTo(-elasticOffset));
                Assert.That(localDelta, Is.EqualTo(new Vector2(-elasticOffset.x, elasticOffset.y)));

                var logicalLocalPoint = new Vector2(24f, -16f);
                var baselineScreenPoint = TextInput.ResolveElasticPresentationScreenPoint(rectTransform,
                    logicalLocalPoint,
                    Vector2.zero,
                    null);
                var overscrollScreenPoint = TextInput.ResolveElasticPresentationScreenPoint(rectTransform,
                    logicalLocalPoint,
                    elasticOffset,
                    null);
                Assert.That(overscrollScreenPoint - baselineScreenPoint,
                    Is.EqualTo(new Vector2(localDelta.x * 2f, localDelta.y * 3f)));

                x.BeginReturn(settings);
                y.BeginReturn(settings);
                x.TickReturn(0.05f, settings);
                y.TickReturn(0.05f, settings);
                var returningScreenPoint = TextInput.ResolveElasticPresentationScreenPoint(rectTransform,
                    logicalLocalPoint,
                    new Vector2(x.Offset, y.Offset),
                    null);
                Assert.That(Vector2.Distance(returningScreenPoint, baselineScreenPoint),
                    Is.LessThan(Vector2.Distance(overscrollScreenPoint, baselineScreenPoint)));

                x.Settle();
                y.Settle();
                var settledScreenPoint = TextInput.ResolveElasticPresentationScreenPoint(rectTransform,
                    logicalLocalPoint,
                    new Vector2(x.Offset, y.Offset),
                    null);
                Assert.That(settledScreenPoint, Is.EqualTo(baselineScreenPoint));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void IdleTextOwnersReturnBeforeRuntimeOrLayoutInspection()
        {
            var textBoxObject = new GameObject("Idle TextBox");
            var textInputObject = new GameObject("Idle TextInput");
            textBoxObject.SetActive(false);
            textInputObject.SetActive(false);
            HybridInputRuntime.ResetState();

            try
            {
                using var providerRegistration = HybridInputRuntime.RegisterProvider(new ScrollProvider(
                    HybridScrollCapability.DeltaOnly,
                    eventData => new HybridScrollInput(eventData.scrollDelta, DeltaOnly())));
                RuntimeDispatcher().RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
                Assert.That(HybridInputRuntime.Diagnostics.ScrollCapability,
                    Is.EqualTo(HybridScrollCapability.DeltaOnly));

                var textBox = textBoxObject.AddComponent<TextBox>();
                var textInput = textInputObject.AddComponent<TextInput>();
                var textBoxScratch = GetPrivateField<List<MonoBehaviour>>(textBox, "parentScrollHandlerScratch");
                var textInputScratch = GetPrivateField<List<MonoBehaviour>>(textInput, "parentScrollHandlerScratch");
                textBoxScratch.Add(null!);
                textInputScratch.Add(null!);

                for (var i = 0; i < 10; ++i)
                {
                    Assert.DoesNotThrow(() => InvokePrivateTickElastic(textBox));
                    Assert.DoesNotThrow(() => InvokePrivateTickElastic(textInput));
                }

                Assert.That(textBoxScratch.Count, Is.EqualTo(1));
                Assert.That(textInputScratch.Count, Is.EqualTo(1));
            }
            finally
            {
                HybridInputRuntime.ResetState();
                UnityEngine.Object.DestroyImmediate(textBoxObject);
                UnityEngine.Object.DestroyImmediate(textInputObject);
            }
        }

        [Test]
        public void SettingsSanitizeNonFiniteValuesWithoutResettingEnabled()
        {
            var settings = new ScrollElasticSettings
            {
                Enabled = false,
                Resistance = float.PositiveInfinity,
                MaxOverscroll = float.NaN,
                ReturnDurationSeconds = float.NegativeInfinity,
                ReleaseDelaySeconds = float.NaN
            };

            settings.Validate();

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Resistance, Is.EqualTo(ScrollElasticSettings.DefaultResistance));
            Assert.That(settings.MaxOverscroll, Is.EqualTo(ScrollElasticSettings.DefaultMaxOverscroll));
            Assert.That(settings.ReturnDurationSeconds,
                Is.EqualTo(ScrollElasticSettings.DefaultReturnDurationSeconds));
            Assert.That(settings.ReleaseDelaySeconds,
                Is.EqualTo(ScrollElasticSettings.DefaultReleaseDelaySeconds));
        }

        [Test]
        public void AxisKeepsLogicalOffsetClampedAndConsumesReverseVisualOffsetFirst()
        {
            var axis = new ScrollElasticAxis();
            var settings = Settings(true, 0.5f, 100f, 0.24f, 0.08f);

            Assert.That(axis.Apply(90f, 100f, 30f, settings, out var logical), Is.True);
            Assert.That(logical, Is.EqualTo(100f));
            Assert.That(axis.Offset, Is.EqualTo(10f).Within(0.001f));

            axis.Apply(logical, 100f, -6f, settings, out logical);
            Assert.That(logical, Is.EqualTo(100f));
            Assert.That(axis.Offset, Is.EqualTo(4f).Within(0.001f));

            axis.Apply(logical, 100f, -14f, settings, out logical);
            Assert.That(axis.Offset, Is.Zero);
            Assert.That(logical, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void AxisUsesTheSameClampedBehaviorAtTheMinimumEdge()
        {
            var axis = new ScrollElasticAxis();
            var settings = Settings(true, 0.5f, 100f, 0.24f, 0.08f);

            Assert.That(axis.Apply(10f, 100f, -30f, settings, out var logical), Is.True);
            Assert.That(logical, Is.Zero);
            Assert.That(axis.Offset, Is.EqualTo(-10f).Within(0.001f));

            axis.Apply(logical, 100f, 16f, settings, out logical);
            Assert.That(axis.Offset, Is.Zero);
            Assert.That(logical, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void AxisSettlesWhenOwnerRangeShrinksToVisualEpsilon()
        {
            var settings = Settings(true, 0.5f, 100f, 0.24f, 0.08f);
            var axis = new ScrollElasticAxis();
            var release = new ScrollElasticReleasePolicy();
            axis.Apply(100f, 100f, 30f, settings, out _);
            release.Observe(DeltaOnly(), 1d, settings.ReleaseDelaySeconds);

            Assert.That(axis.IsActive, Is.True);
            Assert.That(release.IsPending, Is.True);
            Assert.That(axis.SettleIfUnavailable(0.05f, release), Is.True);
            Assert.That(axis.Offset, Is.Zero);
            Assert.That(release.IsPending, Is.False);

            axis.Apply(100f, 100f, 30f, settings, out _);
            Assert.That(axis.SettleIfUnavailable(0.11f, release), Is.False);
            Assert.That(axis.IsActive, Is.True);
        }

        [Test]
        public void TextOwnersSettleActiveAxesAndReleaseWhenRangeShrinksToEpsilon()
        {
            var textBoxObject = new GameObject("TextBox range gate");
            var textInputObject = new GameObject("TextInput range gate");
            textBoxObject.SetActive(false);
            textInputObject.SetActive(false);

            try
            {
                AssertOwnerRangeGate(textBoxObject.AddComponent<TextBox>());
                AssertOwnerRangeGate(textInputObject.AddComponent<TextInput>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(textBoxObject);
                UnityEngine.Object.DestroyImmediate(textInputObject);
            }
        }

        [Test]
        public void AxisHoldsAtCapAndNewInputInterruptsReturnWithoutJump()
        {
            var axis = new ScrollElasticAxis();
            var settings = Settings(true, 0.5f, 20f, 0.24f, 0.08f);
            axis.Apply(100f, 100f, 200f, settings, out _);
            Assert.That(axis.Offset, Is.EqualTo(20f));

            axis.Apply(100f, 100f, 200f, settings, out _);
            Assert.That(axis.Offset, Is.EqualTo(20f));

            axis.BeginReturn(settings);
            axis.TickReturn(0.05f, settings);
            var returningOffset = axis.Offset;
            axis.Apply(100f, 100f, 5f, settings, out _);

            Assert.That(axis.IsReturning, Is.False);
            Assert.That(axis.Offset, Is.GreaterThanOrEqualTo(returningOffset));
            Assert.That(axis.Offset, Is.LessThanOrEqualTo(settings.MaxOverscroll));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void MonotonicExponentialReturnReachesVisualEpsilonAtConfiguredDuration(int framesPerSecond)
        {
            var settings = Settings(true, 1f, 96f, 0.24f, 0.08f);
            var axis = new ScrollElasticAxis();
            axis.Apply(100f, 100f, 1000f, settings, out _);
            axis.BeginReturn(settings);
            var previous = axis.Offset;
            var elapsed = 0f;
            var step = 1f / framesPerSecond;
            while (elapsed < settings.ReturnDurationSeconds)
            {
                var delta = Mathf.Min(step, settings.ReturnDurationSeconds - elapsed);
                axis.TickReturn(delta, settings);
                Assert.That(Mathf.Abs(axis.Offset), Is.LessThanOrEqualTo(Mathf.Abs(previous) + 0.0001f));
                previous = axis.Offset;
                elapsed += delta;
            }

            Assert.That(axis.Offset, Is.Zero);
            var rate = Mathf.Log(settings.MaxOverscroll / ScrollElasticAxis.VisualEpsilon) /
                       settings.ReturnDurationSeconds;
            var snapMagnitude = Mathf.Abs(ScrollElasticAxis.EvaluateReturnOffset(settings.MaxOverscroll,
                rate,
                settings.ReturnDurationSeconds));
            Assert.That(snapMagnitude, Is.LessThanOrEqualTo(ScrollElasticAxis.VisualEpsilon + 0.001f));
        }

        [TestCase(96f, 0.24f)]
        [TestCase(20f, 0.19f)]
        [TestCase(5f, 0.14f)]
        public void SmallerOffsetsReachEpsilonEarlierWithTheSameExponentialCoefficient(float offset,
            float maximumExpectedSeconds)
        {
            var settings = Settings(true, 1f, 96f, 0.24f, 0.08f);
            var axis = new ScrollElasticAxis();
            axis.Apply(100f, 100f, offset, settings, out _);
            axis.BeginReturn(settings);
            var elapsed = 0f;
            while (axis.IsReturning && elapsed < 1f)
            {
                axis.TickReturn(0.001f, settings);
                elapsed += 0.001f;
            }

            Assert.That(elapsed, Is.LessThanOrEqualTo(maximumExpectedSeconds + 0.002f));
        }

        [Test]
        public void ReturnSettlesOnNonFiniteOrLargeDeltaTime()
        {
            var settings = Settings(true, 1f, 96f, 0.24f, 0.08f);
            var nonFinite = OverscrolledAxis(settings);
            nonFinite.BeginReturn(settings);
            nonFinite.TickReturn(float.NaN, settings);
            Assert.That(nonFinite.Offset, Is.Zero);

            var large = OverscrolledAxis(settings);
            large.BeginReturn(settings);
            large.TickReturn(settings.ReturnDurationSeconds * 5f, settings);
            Assert.That(large.Offset, Is.Zero);
        }

        [Test]
        public void ReturnSnapshotsCurveParametersAndNeverMovesOutwardWhenSettingsChange()
        {
            var settings = Settings(true, 1f, 96f, 0.24f, 0.08f);
            var axis = OverscrolledAxis(settings);
            axis.BeginReturn(settings);
            axis.TickReturn(0.05f, settings);
            var beforeMutation = Mathf.Abs(axis.Offset);

            settings.ReturnDurationSeconds = 1f;
            settings.MaxOverscroll = 20f;
            axis.TickReturn(0.01f, settings);

            Assert.That(Mathf.Abs(axis.Offset), Is.LessThan(beforeMutation));
        }

        [Test]
        public void ReturnSettlesWhenRuntimeSettingsDisableOrInvalidateIt()
        {
            var disabledSettings = Settings(true, 1f, 96f, 0.24f, 0.08f);
            var disabled = OverscrolledAxis(disabledSettings);
            disabled.BeginReturn(disabledSettings);
            disabledSettings.Enabled = false;
            disabled.TickReturn(0.01f, disabledSettings);
            Assert.That(disabled.Offset, Is.Zero);

            var invalidSettings = Settings(true, 1f, 96f, 0.24f, 0.08f);
            var invalid = OverscrolledAxis(invalidSettings);
            invalid.BeginReturn(invalidSettings);
            invalidSettings.ReturnDurationSeconds = 0f;
            invalid.TickReturn(0.01f, invalidSettings);
            Assert.That(invalid.Offset, Is.Zero);
        }

        [Test]
        public void ChangedCurveParametersApplyToTheNextReturn()
        {
            var settings = Settings(true, 1f, 96f, 0.24f, 0.08f);
            var axis = OverscrolledAxis(settings);
            axis.BeginReturn(settings);
            axis.TickReturn(0.1f, settings);
            var fastOffset = Mathf.Abs(axis.Offset);

            axis.Settle();
            axis.Apply(100f, 100f, 1000f, settings, out _);
            settings.ReturnDurationSeconds = 1f;
            axis.BeginReturn(settings);
            axis.TickReturn(0.1f, settings);

            Assert.That(Mathf.Abs(axis.Offset), Is.GreaterThan(fastOffset));
        }

        [TestCase(0f)]
        [TestCase(0.05f)]
        [TestCase(0.08f)]
        [TestCase(0.18f)]
        public void DeltaOnlyReleaseUsesOwnerLocalDeadline(float delay)
        {
            var policy = new ScrollElasticReleasePolicy();
            policy.Observe(DeltaOnly(), 1d, delay);

            if (delay > 0f)
            {
                Assert.That(policy.TryBeginReturn(1d + delay - 0.001d), Is.False);
            }
            Assert.That(policy.TryBeginReturn(1d + delay + 0.001d), Is.True);
            Assert.That(policy.TryBeginReturn(2d), Is.False);
        }

        [Test]
        public void IndependentAxisDeadlinesDoNotResetEachOther()
        {
            var x = new ScrollElasticReleasePolicy();
            var y = new ScrollElasticReleasePolicy();
            x.Observe(DeltaOnly(), 1d, 0.08f);
            y.Observe(DeltaOnly(), 1.1d, 0.08f);

            Assert.That(x.TryBeginReturn(1.081d), Is.True);
            Assert.That(y.TryBeginReturn(1.179d), Is.False);
            Assert.That(y.TryBeginReturn(1.181d), Is.True);
        }

        [Test]
        public void PhasedReleaseWaitsForLastRealGestureOrMomentumEnd()
        {
            var policy = new ScrollElasticReleasePolicy();
            policy.Observe(Phased(HybridInputPhase.Changed, HybridInputPhase.None), 1d, 0.08f);
            Assert.That(policy.TryBeginReturn(10d), Is.False);

            policy.Observe(Phased(HybridInputPhase.Ended, HybridInputPhase.Began), 2d, 0.08f);
            Assert.That(policy.TryBeginReturn(10d), Is.False);

            policy.Observe(Phased(HybridInputPhase.None, HybridInputPhase.Ended), 3d, 0.08f);
            Assert.That(policy.TryBeginReturn(3d), Is.True);
        }

        [Test]
        public void UnsupportedCancelsReleaseAndRealDragEndReleasesImmediately()
        {
            var policy = new ScrollElasticReleasePolicy();
            policy.Observe(DeltaOnly(), 1d, 0.08f);
            policy.Observe(new HybridScrollMetadata(HybridScrollCapability.Unsupported,
                HybridInputDeviceKind.Unknown,
                HybridInputPhase.Unknown,
                HybridInputPhase.Unknown,
                0d,
                0L), 1.01d, 0.08f);
            Assert.That(policy.TryBeginReturn(10d), Is.False);

            policy.ReleaseImmediately(2d);
            Assert.That(policy.TryBeginReturn(2d), Is.True);
        }

        [Test]
        public void DispatcherResolverEnrichesTheSameUguiDeltaExactlyOnce()
        {
            var provider = new ScrollProvider(HybridScrollCapability.DeltaOnly,
                input => new HybridScrollInput(input.scrollDelta, DeltaOnly()));
            var dispatcher = Select(provider);
            var eventData = PointerEvent(new Vector2(1.25f, -2.5f));

            var resolved = dispatcher.ResolveScrollInput(eventData);

            Assert.That(provider.ResolveCount, Is.EqualTo(1));
            Assert.That(resolved.Delta, Is.EqualTo(eventData.scrollDelta));
            Assert.That(resolved.Metadata.Capability, Is.EqualTo(HybridScrollCapability.DeltaOnly));
        }

        [Test]
        public void DispatcherRejectsSecondDeltaAndDowngradesUnreliablePhasedMetadata()
        {
            var mismatched = Select(new ScrollProvider(HybridScrollCapability.DeltaOnly,
                _ => new HybridScrollInput(Vector2.one, DeltaOnly())));
            Assert.That(mismatched.ResolveScrollInput(PointerEvent(new Vector2(2f, 3f))).Metadata.Capability,
                Is.EqualTo(HybridScrollCapability.Unsupported));

            var unreliable = Select(new ScrollProvider(HybridScrollCapability.Phased,
                input => new HybridScrollInput(input.scrollDelta,
                    new HybridScrollMetadata(HybridScrollCapability.Phased,
                        HybridInputDeviceKind.Unknown,
                        HybridInputPhase.Changed,
                        HybridInputPhase.None,
                        1d,
                        1L))));
            Assert.That(unreliable.ResolveScrollInput(PointerEvent(new Vector2(2f, 3f))).Metadata.Capability,
                Is.EqualTo(HybridScrollCapability.DeltaOnly));
        }

        private static ScrollElasticAxis OverscrolledAxis(ScrollElasticSettings settings)
        {
            var axis = new ScrollElasticAxis();
            axis.Apply(100f, 100f, 1000f, settings, out _);
            return axis;
        }

        private static ScrollElasticSettings Settings(bool enabled,
            float resistance,
            float maxOverscroll,
            float duration,
            float delay)
        {
            return new ScrollElasticSettings
            {
                Enabled = enabled,
                Resistance = resistance,
                MaxOverscroll = maxOverscroll,
                ReturnDurationSeconds = duration,
                ReleaseDelaySeconds = delay
            };
        }

        private static void SetSerializedVersion(ScrollElasticSettings settings, int version)
        {
            typeof(ScrollElasticSettings)
                .GetField("m_serializedVersion", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(settings, version);
        }

        private static void SetOwnerSettings(object owner, ScrollElasticSettings? settings)
        {
            owner.GetType()
                .GetField("m_scrollElastic", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(owner, settings);
        }

        private static void AssertOwnerRangeGate(object owner)
        {
            var settings = Settings(true, 0.5f, 96f, 0.24f, 0.08f);
            var x = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticX");
            var y = GetPrivateField<ScrollElasticAxis>(owner, "scrollElasticY");
            var releaseX = GetPrivateField<ScrollElasticReleasePolicy>(owner, "scrollElasticReleaseX");
            var releaseY = GetPrivateField<ScrollElasticReleasePolicy>(owner, "scrollElasticReleaseY");
            x.Apply(100f, 100f, 30f, settings, out _);
            y.Apply(100f, 100f, 30f, settings, out _);
            releaseX.Observe(DeltaOnly(), 1d, settings.ReleaseDelaySeconds);
            releaseY.Observe(DeltaOnly(), 1d, settings.ReleaseDelaySeconds);

            var changed = owner switch
            {
                TextBox textBox => textBox.SettleElasticAxesForRange(0.05f, 0.11f),
                TextInput textInput => textInput.SettleElasticAxesForRange(0.05f, 0.11f),
                _ => throw new InvalidOperationException($"Unsupported owner {owner.GetType().FullName}")
            };

            Assert.That(changed, Is.True);
            Assert.That(x.Offset, Is.Zero);
            Assert.That(releaseX.IsPending, Is.False);
            Assert.That(y.IsActive, Is.True);
            Assert.That(releaseY.IsPending, Is.True);
        }

        private static void AssertDirectHorizontalDirection(ScrollAxisLock axisLock, float rawDeltaX)
        {
            axisLock.Reset();
            Assert.That(axisLock.Resolve(new Vector2(rawDeltaX, 0f),
                    false,
                    out var contentOffsetDelta,
                    out var lockedScrollDelta),
                Is.EqualTo(ScrollAxis.Horizontal));
            Assert.That(contentOffsetDelta, Is.EqualTo(new Vector2(rawDeltaX, 0f)));
            Assert.That(lockedScrollDelta, Is.EqualTo(new Vector2(rawDeltaX, 0f)));
        }

        private static void InvokePrivateTickElastic(object owner)
        {
            InvokePrivate(owner, "TickElastic", new object?[] { null });
        }

        private static void InvokePrivate(object owner, string methodName, object?[]? arguments = null)
        {
            owner.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(owner, arguments);
        }

        private static T InvokePrivate<T>(object owner, string methodName, object?[]? arguments = null)
        {
            return (T)owner.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(owner, arguments)!;
        }

        private static T InvokePrivateStatic<T>(Type ownerType, string methodName, object?[]? arguments = null)
        {
            return (T)ownerType
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, arguments)!;
        }

        private static T GetPrivateField<T>(object owner, string fieldName)
        {
            return (T)owner.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(owner)!;
        }

        private static T GetPrivateFieldFromHierarchy<T>(object owner, string fieldName)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return (T)field.GetValue(owner)!;
                }
            }

            throw new MissingFieldException(owner.GetType().FullName, fieldName);
        }

        private static void SetPrivateField(object owner, string fieldName, object value)
        {
            owner.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(owner, value);
        }

        private static HybridScrollMetadata DeltaOnly()
        {
            return new HybridScrollMetadata(HybridScrollCapability.DeltaOnly,
                HybridInputDeviceKind.Unknown,
                HybridInputPhase.Unknown,
                HybridInputPhase.Unknown,
                1d,
                0L);
        }

        private static HybridScrollMetadata Phased(HybridInputPhase gesture, HybridInputPhase momentum)
        {
            return new HybridScrollMetadata(HybridScrollCapability.Phased,
                HybridInputDeviceKind.Touchpad,
                gesture,
                momentum,
                1d,
                42L);
        }

        private static PointerEventData PointerEvent(Vector2 delta)
        {
            return new PointerEventData(null!) { scrollDelta = delta };
        }

        private static PointerEventData LeftDragEvent()
        {
            return new PointerEventData(null!)
            {
                button = PointerEventData.InputButton.Left,
                delta = new Vector2(0f, 12f),
                position = new Vector2(40f, 40f)
            };
        }

        private static HybridInputDispatcher Select(ScrollProvider provider)
        {
            var dispatcher = new HybridInputDispatcher();
            dispatcher.RegisterProvider(provider);
            dispatcher.RefreshEnvironment(new HybridInputEnvironment(null, 1, true));
            return dispatcher;
        }

        private static HybridInputDispatcher RuntimeDispatcher()
        {
            return (HybridInputDispatcher)typeof(HybridInputRuntime)
                .GetField("Dispatcher", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
        }

        private sealed class ScrollProvider : IHybridInputProvider, IHybridScrollInputProvider
        {
            private readonly Func<PointerEventData, HybridScrollInput> resolve;

            internal ScrollProvider(HybridScrollCapability capability,
                Func<PointerEventData, HybridScrollInput> resolve)
            {
                ScrollCapability = capability;
                this.resolve = resolve;
            }

            public string Id => "scroll";
            public int Priority => 0;
            public HybridInputProviderKind Kind => HybridInputProviderKind.Custom;
            public HybridInputCapabilities Capabilities => HybridInputCapabilities.ScrollDelta |
                                                           HybridInputCapabilities.ScrollDevice |
                                                           HybridInputCapabilities.ScrollPhase;
            public HybridScrollCapability ScrollCapability { get; }
            internal int ResolveCount { get; private set; }

            public HybridInputProviderMatch Match(HybridInputEnvironment environment)
            {
                return HybridInputProviderMatch.Exact;
            }

            public bool TryResolveScrollInput(PointerEventData eventData, out HybridScrollInput scrollInput)
            {
                ++ResolveCount;
                scrollInput = resolve(eventData);
                return true;
            }

            public void Start(IHybridInputEventSink sink)
            {
            }

            public void Stop()
            {
            }

            public void Collect(HybridInputCollectContext context)
            {
            }

            public void SetImeEnabled(bool enabled)
            {
            }

            public void SetImeCursorPosition(Vector2 position)
            {
            }
        }
    }
}
