# Unity components and events

This page is the compact component/API index. Task guides contain the usage
recipes and parameter explanations.

## Choose a component

| Component | Namespace | Use it for |
| --- | --- | --- |
| `TextBox` | `Milestro.Components` | UI paragraph rendering, rich text, links, Ruby, scrolling |
| `TextBoxRenderTextureProducer` | `Milestro.Components.Internal` | Content and style configuration for `TextBox` |
| `CanvasSlimText` | `Milestro.Components` | Single-line UI label |
| `WorldSpaceTextBox` | `Milestro.Components` | Paragraph or rich text in world space |
| `WorldSpaceSlimText` | `Milestro.Components` | Single-line label in world space |
| `WorldSpaceTextBatch` | `Milestro.Components` | Batch compatible world-space text renderers |
| `TextInput` | `Milestro.Components` | Editable UI text field |
| `TextBoxLayoutElement` | `Milestro.Components` | Content-based sizing for `TextBox` |
| `TextBoxScrollbar` | `Milestro.Components` | Connect `TextBox`/`TextInput` to uGUI scrollbars |
| `MilestroScrollRect` | `Milestro.Components` | `ScrollRect` with Milestro wheel/Elastic behavior |
| `TextBoxAccessibility` | `Milestro.Components` | Keyboard/controller link navigation and screen-reader nodes |
| `TextInputAccessibility` | `Milestro.Components` | Screen-reader value and editing announcements |

## `TextBox`

`TextBox` is a uGUI `Graphic`. It creates or discovers a
`TextBoxRenderTextureProducer` on the same GameObject. The producer is the
source of content and style; `TextBox` presents its output.

Public surface:

| Member | Type | Purpose |
| --- | --- | --- |
| `onLinkClicked` | `LinkClickedEvent` | Click activation for an `<a>` range |
| `onLinkHoverChanged` | `LinkHoverEvent` | Pointer enter/leave for a link |
| `linkHoverColor`, `linkHoverUnderlineColor`, `linkHoverUnderlineThickness` | settings | Hover feedback |
| `linkHoverCursorEnabled`, `linkHoverCursor`, `linkHoverCursorHotspot`, `linkHoverCursorMode` | settings | Cursor feedback |
| `scrollElastic` | `ScrollElasticSettings` | Presentation-only overscroll |
| `GetScrollPercent()`, `GetScrollPercentX/Y()` | methods | Current normalized position |
| `ScrollToPercent(Vector2, bool)`, `ScrollToPercentX/Y(float, bool)` | methods | Programmatic scrolling |
| `TryGetScrollState(out TextBoxScrollState)` | method | Logical offset, viewport, and content size |

Configure the producer with:

```csharp
var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.content = "Text";
```

See [Text, fonts, and rich text](../guides/text.md) and
[Layout engine parameters](layout-engine.md).

## `TextInput`

`TextInput` is a `RenderTextureGraphic` and an editable text component. It owns
an internal native `InputBox`; do not add a child `TMP_InputField` or a manual
`InputBox` MonoBehaviour.

### Events

| Member | Type | Payload |
| --- | --- | --- |
| `onValueChanged` | `UnityEvent<string>` | Canonical committed text |
| `onEndEdit` | `UnityEvent<string>` | Final text when editing ends |
| `onSubmit` | `UnityEvent<string>` | Submitted text |
| `onFocusGained` | `UnityEvent` | Focus acquired |
| `onFocusLost` | `UnityEvent` | Focus lost |
| `onTouchScreenKeyboardStatusChanged` | `TouchScreenKeyboardEvent` | `TouchScreenKeyboard.Status` |

### Properties

- Text: `Text`, `SetTextWithoutNotify(string)`, `Submit()`
- Appearance: `size`, `fontFamilies`, `textColor`, `caretColor`, `caretWidth`,
  `selectionColor`, `textAlignment`, `lineMode`, `wrapMode`, `textOverflow`,
  `ellipsisString`, `locale`, `srgb`
- Form: `placeholder`, `characterLimit`, `contentType`, `characterValidation`,
  `keyboardType`, `readOnly`, `maskInput`, `maskChar`, `isPassword`,
  `allowCopy`, `selectionEnabled`, `submitOnEnter`
- Scroll: `smoothScroll`, `scrollTweenDurationSeconds`, `scrollElastic`,
  `GetScrollPercent*`, `ScrollToPercent*`, `TryGetScrollState`
- Mobile: `shouldHideMobileInput`, `touchScreenKeyboard`
- Validation: `onValidateInput`, `onValidateEdit`

See [Input and IME](../guides/input.md).

## `SlimText` components

### `CanvasSlimText`

Requires a `RectTransform`, `RawImage`, and `SlimTextRenderTextureProducer`.

| Member | Purpose |
| --- | --- |
| `Text`, `TextColor` | Convenience property accessors |
| `SetContent(string, Color)` | Update both values together |
| `Configure(string, float, SlimTextHorizontalAlign, Color)` | Set content, size, alignment, and color |

For all parameters, access the `SlimTextRenderTextureProducer` directly. See
[SlimText](../guides/slim-text.md).

### `WorldSpaceSlimText`

Exposes `text`, `fontFamily`, `fontSize`, `fontWeight`, `color`,
`textureSizePixels`, `pixelsPerUnit`, `rectOffset`, alignment, width/height
modes, auto font size, material, and renderer settings. It also exposes:

- `PrepareTextUtf8NoAlloc(int capacity)`
- `SetTextUtf8NoAlloc(byte[] buffer, int offset, int length)`

## World-space components

### `WorldSpaceTextBox`

Public settings:

- `textureSizePixels`
- `pixelsPerUnit`
- `material`
- `texturePropertyName`
- `disableRendererWhenNoOutput`

The producer is created automatically and uses the same
`TextBoxRenderTextureProducer` API. See [World-space text](../guides/world-space.md).

### `WorldSpaceTextBatch`

- `BatchedRendererCount`
- `BatchedTextCount`
- `Rebuild()`

The batch combines compatible child renderers. Enable the render-surface atlas
before creating text for the largest benefit. See
[Rendering, batching, and backends](rendering.md#draw-call-batching).

## Layout and scrolling helpers

### `TextBoxLayoutElement`

- `widthMode`: `Fixed`, `MaxContent`, `FitContent`
- `heightMode`: `Fixed`, `Auto`
- `forwardPointerScrollToParent`
- `layoutPriority`

### `TextBoxScrollbar`

Assign exactly one target (`TextBox` or `TextInput`) and optional horizontal and
vertical Unity `Scrollbar` references. It hides bars without overflow and keeps
their values synchronized.

### `MilestroScrollRect`

Derives from `ScrollRect`. Adds:

- `scrollElastic`
- `scrollWheelStepPixels`
- `smoothScroll`
- `scrollTweenDurationSeconds`
- `GetLogicalNormalizedPosition`, `SetLogicalNormalizedPosition`
- logical horizontal/vertical get/set helpers
- `new content` returning the logical content RectTransform

## Event payloads

All event payloads live in `Milestro.Events`:

| Type | Fields |
| --- | --- |
| `LinkClickedEventArgs` | `Href`, `Id` |
| `LinkHoverEventArgs` | `Href`, `Id`, `Hovered` |

`TouchScreenKeyboardEvent` is a `UnityEvent<TouchScreenKeyboard.Status>`.

Event properties are getter-only; subscribe with `AddListener` and remove with
`RemoveListener`.

## Low-level managed wrappers

These are not scene components. Dispose them with `using` or `Dispose()` when
you create them directly.

| Type | Namespace | Purpose |
| --- | --- | --- |
| `TextStyle` | `Milestro.Skia.TextLayout` | Native text run style |
| `ParagraphStyle` | `Milestro.Skia.TextLayout` | Native paragraph style |
| `ParagraphBuilder` | `Milestro.Skia.TextLayout` | Build a native paragraph |
| `Paragraph` | `Milestro.Skia.TextLayout` | Layout, measure, paint, hit-test, and outline a paragraph |
| `InputBox` | `Milestro.Skia.TextLayout` | Native-backed editable text model |
| `RubyStyle` | `Milestro.Skia.TextLayout` | Ruby position, alignment, size, gap, overhang, overflow |
| `StrutStyle` | `Milestro.Skia.TextLayout` | Native strut metrics |
| `FontRegistry` | `Milestro.Skia` | Register and inspect fonts |
| `FontCollection` | `Milestro.Skia.TextLayout` | Font fallback/cache controls |
| `Canvas` | `Milestro.Skia` | Native Skia drawing target |
| `UnityAutoRenderTextureSurface` | `Milestro.Skia` | Render-target backend selection |

### `Paragraph` query surface

`Height`, `LongestLine`, `MinIntrinsicWidth`, `MaxIntrinsicWidth`, `Layout`,
`Paint`, `SplitGlypl`, `ToPath`, and `ToSDF` are the main low-level operations.

### `InputBox` query and edit surface

`Text`, `Cursor`, `Selection`, `SetViewport`, `SetSoftWrap`, `MaskInput`,
`Focused`, `SetTextOverflow`, `InsertText`, composition/commit, undo/redo,
selection movement, `HitTest`, `EnsureCaretVisible`, `GetCaretRect`,
`GetCompositionRect`, `GetMetrics`, `GetLineCount`, and `GetLineMetrics`.

See [Layout engine parameters](layout-engine.md#low-level-paragraph-api) for a
build example.
