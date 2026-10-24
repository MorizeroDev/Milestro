# Scrolling and scrollbars

Milestro text can scroll its own render-texture viewport. This is separate from
a Unity `ScrollRect` moving a content RectTransform.

## Scroll a TextBox

1. Give the `TextBox` a fixed `RectTransform` size.
2. Set `textOverflow` to `Overflow` when you want a scrollable content range.
3. Keep `wrapMode` as needed for the content.

```csharp
using Milestro.Components.Internal;
using Milestro.Model;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.textOverflow = TextOverflow.Overflow;
producer.wrapMode = TextBoxWrapMode.Wrap;
```

Mouse-wheel and trackpad input scrolls the text viewport. The `TextBox`
inspector exposes wheel step, smooth scrolling, tween duration, and the
`Scroll Elastic` settings group.

Read or change the normalized position from code:

```csharp
Vector2 position = textBox.GetScrollPercent();
textBox.ScrollToPercent(new Vector2(0f, 1f), animated: true);

float vertical = textBox.GetScrollPercentY();
textBox.ScrollToPercentY(vertical, animated: false);
```

`TryGetScrollState(out TextBoxScrollState state)` returns the current logical
scroll offset, viewport size, and content size.

## Scrollbar binding

Add **Milestro** → **Text Box Scrollbar** to a GameObject and assign either:

- one `TextBox` target, or
- one `TextInput` target.

Do not assign both. Then assign a horizontal and/or vertical Unity `Scrollbar`.
The connector:

- hides a scrollbar when its axis has no overflow;
- sets the thumb size from viewport/content ratio;
- writes the normalized position to the scrollbar;
- sends scrollbar changes back to the text viewport.

For vertical scrolling, the scrollbar value is inverted so that `1` means the
top of the content, matching the usual uGUI convention.

## Scroll a TextInput

`TextInput` scrolls when its content exceeds the viewport. Use:

- `lineMode = MultiLine` with `wrapMode = NoWrap` for horizontal scrolling;
- `lineMode = MultiLine` with `wrapMode = Wrap` for vertical soft-wrapped
  scrolling;
- `textOverflow = Overflow` when you want a scrollable range instead of
  clipping;
- `smoothScroll` and `scrollTweenDurationSeconds` for wheel animation.

The same `GetScrollPercentX/Y`, `ScrollToPercent*`, and
`TryGetScrollState` accessors are available. Caret-following scrolling is
handled by the native editable box.

## ScrollRect integration

Use `Milestro.Components.MilestroScrollRect` instead of the stock `ScrollRect`
when the scroll view itself should receive Milestro wheel tweening and Elastic
behavior. It derives from `ScrollRect`, so it can be used as a normal scroll
view, with these Milestro-specific additions:

```csharp
using Milestro.Components;
using UnityEngine;

var scrollRect = GetComponent<MilestroScrollRect>();
scrollRect.SetLogicalNormalizedPosition(new Vector2(0f, 1f));
```

Use the logical helpers when Elastic is active:

- `GetLogicalNormalizedPosition`
- `SetLogicalNormalizedPosition`
- `GetLogicalHorizontalNormalizedPosition`
- `GetLogicalVerticalNormalizedPosition`
- `SetLogicalHorizontalNormalizedPosition`
- `SetLogicalVerticalNormalizedPosition`

`MilestroScrollRect.content` returns the logical content object. The stock
`ScrollRect` normalized-position properties can observe a short
render-time visual offset while Elastic is applied; use the logical helpers when
that distinction matters.

## Elastic overscroll

`TextBox`, `TextInput`, and `MilestroScrollRect` each have a serialized
`scrollElastic` group:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Enables presentation-only overscroll |
| `Resistance` | `0.5` | Drag resistance |
| `MaxOverscroll` | `96` | Maximum visual offset |
| `ReturnDurationSeconds` | `0.24` | Return animation duration |
| `ReleaseDelaySeconds` | `0.08` | Idle delay before returning |

Elastic is presentation-only: logical scroll offsets, scrollbars, selection,
caret geometry, and hit testing remain clamped. It requires a selected input
provider with at least delta-only scroll capability. An active parent scroll
handler disables child Elastic; nested edge handoff is not part of this behavior.

Delta-only providers have no reliable gesture-end signal, so release timing and
orthogonal gesture behavior have documented limits. See
[Known limitations](../reference/limitations.md#hybridinput-scroll-deltaonly).

## Tuning global scroll behavior

Project-wide defaults live under
`MilestroConfiguration.Configuration`:

- `ScrollAxisLock`: deadzone, dominance ratio, and timeout.
- `ScrollTween`: pointer-wheel tween mode and fractional-delta tolerance.
- `TextInput`: wheel step, key repeat timing, and keyboard scroll interlock.

See [Runtime configuration](../reference/configuration.md).
