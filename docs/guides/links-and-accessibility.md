# Links and accessibility

`TextBox` can expose clickable ranges and pointer-hover feedback. Two optional
components add keyboard/controller navigation and screen-reader nodes.

## Clickable links

Use the `<a>` element in rich text:

```xml
Read the <a href="https://example.com/docs" id="docs">documentation</a>.
```

Requirements:

- `href` is required and non-empty.
- `id` is optional, but is rejected when present and empty.
- Anchors cannot be nested.
- Only `href` and optional `id` are accepted.

The target is application data. Milestro returns it unchanged and never opens
or interprets the URL.

Subscribe to the event:

```csharp
using Milestro.Components.Internal;
using Milestro.Events;
using UnityEngine;

var producer = textBox.GetComponent<TextBoxRenderTextureProducer>();
producer.content = "Open the <a href=\"help\">help</a>.";

textBox.onLinkClicked.AddListener((LinkClickedEventArgs args) =>
{
    Debug.Log($"href={args.Href}, id={args.Id}");
});
```

A click is emitted only when the primary mouse button or touch press and
release hit the same visible link range without becoming a drag. Hover feedback
is available through `onLinkHoverChanged`:

```csharp
textBox.onLinkHoverChanged.AddListener((LinkHoverEventArgs args) =>
{
    Debug.Log($"{args.Href} hovered={args.Hovered}");
});
```

The inspector's **Link Hover** settings control the tint, underline, and hand
cursor. `linkHoverCursorEnabled` can be turned off when an application-level
cursor manager owns the cursor. Milestro does not persist visited state; record
`Href` or `Id` yourself if you need it.

## Keyboard and controller navigation

Add `Milestro.Components.TextBoxAccessibility` to the same GameObject as the
`TextBox`. It is a uGUI `Selectable`, so it uses the scene's `EventSystem`.

- Left/Up selects the previous link.
- Right/Down selects the next link.
- Submit activates the link through the same `onLinkClicked` event as a pointer
  click.
- Cancel clears EventSystem selection.

At the end of the link list, navigation returns to uGUI instead of wrapping.
Keyboard/controller focus draws an indicator around each line of the active
link and scrolls an off-screen link into view when the text has a fixed
viewport.

Inspector fields include **Focus Color**, **Focus Thickness**, **Link Hint**,
and **Screen Reader Enabled**.

## Screen readers

Screen-reader support requires:

- Unity 6000.3 or newer;
- the built-in `com.unity.modules.accessibility` package;
- `TextBoxAccessibility` on the static text object or
  `TextInputAccessibility` on the editable object.

Without the module, navigation still works and the component logs a warning
instead of publishing nodes.

`TextBoxAccessibility` publishes static-text and link nodes. Link nodes use the
visible link label, the configured hint, a button role, and screen-space frames
derived from the rendered link geometry. Activation routes back to
`onLinkClicked`, and screen-reader focus scrolls the link into view.

`TextInputAccessibility` publishes one `TextField` node. Its value tracks the
committed text; masked fields publish only mask characters. Because the Unity
Accessibility module has no structured caret/selection channel, changes are
delivered as spoken announcements. Password fields never announce plaintext.
Masked values contain one mask character per grapheme cluster.

```csharp
using Milestro.Components;

textBox.gameObject.AddComponent<TextBoxAccessibility>();
input.gameObject.AddComponent<TextInputAccessibility>();
```

The announcement strings and echo limit are serialized fields so projects can
localize or retune them. See
[TextInput accessibility design](../reference/accessibility-design.md) for the
design rationale and current platform gaps.
