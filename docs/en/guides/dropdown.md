# Dropdown

[English](dropdown.md) | [简体中文](../../zh/guides/dropdown.md)

`MilestroDropdown` is a single-select uGUI control for settings and other
index-backed choices. It uses `CanvasSlimText` for the caption, placeholder,
arrow, and rows. It does not reference TextMesh Pro, create a hidden
`UnityEngine.UI.Text`, start text editing, enable IME, or open a software
keyboard.

## Create and initialize

Choose **GameObject → UI → Milestro Dropdown**. The menu creates a control with
a background, caption, placeholder, arrow, scrollable list template, row text,
selection checkmark, and scrollbar. Keep the template inactive; the component
instantiates it only while the list is open. Add an EventSystem with
`InputSystemUIInputModule` if the scene does not already have one.

Set the complete list and its initial index together. Initialization and list
replacement are silent:

```csharp
using Milestro.Components;
using UnityEngine;

public sealed class QualitySetting : MonoBehaviour
{
    [SerializeField] private MilestroDropdown dropdown;

    private void Awake()
    {
        dropdown.SetOptions(new[] { "Low", "Medium", "High" }, initialIndex: 1);
        dropdown.onValueChanged.AddListener(SaveQuality);
    }

    private void SaveQuality(int index)
    {
        // Map the index to the application's setting and save it here.
    }
}
```

The control copies the supplied strings. `options` is a read-only view;
`optionCount` and `GetOptionText(index)` query it. Duplicate strings remain
distinct indices. A null list becomes empty, and null entries become empty
strings.

## Value and text contract

| Member | Behavior |
| --- | --- |
| `value` | Committed index; a normalized actual change invokes `onValueChanged` once |
| `SetValueWithoutNotify(index)` | Changes the committed index and caption without notification |
| `SetOptions(strings, initialIndex = 0)` | Atomically replaces content, closes the list, discards the candidate, and sends no event |
| `ClearOptions()` | Silently produces an empty list with `value == -1` |
| `SetOptionText(index, text)` | Updates the caption when needed and the matching open row without changing `value` or sending an event |
| `Show()`, `Hide()`, `isExpanded` | Programmatically opens, dismisses, or queries the popup; repeated calls are idempotent |
| `placeholder`, `placeholderText` | Sets the no-selection text and its `CanvasSlimText` presenter |
| `captionText`, `template`, `maxVisibleItems` | Configures presentation and the maximum visible row count |
| `interactable`, `navigation`, `Select()` | Uses the inherited `Selectable` behavior; selecting does not open the list |

For a nonempty list, indices below zero normalize to `-1`, and indices past the
end normalize to the last row. An empty list always has `value == -1`. Explicit
`-1` is a valid no-selection state even when options exist.

Opening starts candidate highlight at the committed value, or at row zero when
no value is selected. Pointer hover and Up/Down navigation move only that
candidate. Confirming closes first and then commits; confirming the existing
value sends no event. Escape, an outside click, and `Hide()` discard the
candidate. Programmatic `value` changes while open also move the candidate so a
later confirmation cannot restore stale state.

`SetOptionText` is the translation refresh boundary. A translation system may
retain a dropdown plus row index and call this method when its language changes.
The control does not store language keys or implement a translation service.

## Input and focus

The control handles only uGUI events from the active input module; it does not
poll a keyboard in parallel.

- Left click, touch click, or the module's Submit action opens the closed
  control. The same Submit action confirms while open.
- Navigate Up/Down moves the candidate without wrapping and scrolls it into
  view. Other navigation is consumed while open instead of moving the form
  underneath.
- Cancel dismisses without committing. With the default Input System actions,
  Enter submits and Escape cancels. Space submits when the project's Submit
  action includes it.
- `ScrollRect` handles wheel and drag input. Its drag threshold suppresses a
  row click after a scroll gesture.
- A full-canvas blocker consumes the outside click that closes the popup. A
  later click can operate the underlying control.
- Normal confirmation and cancellation restore the EventSystem selection to
  the dropdown only when no other object has acquired selection.

One popup is open per root canvas. Disabling interaction, blocking raycasts,
deactivating, destroying, replacing options, or opening another dropdown cleans
up the popup without sending a value event. Moving or resizing the control or
changing its display geometry safely closes the popup rather than leaving a
detached clickable list.

## Popup layout and styling

The popup is re-parented to the root screen-space canvas, outside the settings
row's masks, with an overriding sorting canvas. Its own viewport clips rows.
Placement uses root-canvas coordinates and the screen safe area, opens upward
when that has more useful space, clamps horizontal and vertical extents, and
shrinks the viewport so every row remains scrollable. Screen Space Overlay,
Screen Space Camera, nested canvases, and scaled canvases are supported. World
Space Canvas is not supported.

The default template reserves eight rows and uses fixed-size, single-line
SlimText. Long strings are clipped at the caption or row bounds; they are not
parsed as rich text, wrapped, or allowed to expand the settings row. Configure
caption and row styles independently through their
`SlimTextRenderTextureProducer` components. Duplicate the generated hierarchy
when creating a prefab so its internal row prototype and `ScrollRect` references
are preserved.

The list builds rows on open and updates text only on explicit changes. It does
not rebuild rows every frame. It is a normal, non-virtualized list; 100 rows are
supported, but applications with large lists should measure performance on
their target platforms.

## Platform status and controller support

Physical mobile safe areas, touch behavior, and platform performance are not
currently device-verified.

Controllers are **not yet adapted or device-verified**. Shared uGUI actions may
provide basic operation, but this is not completed controller support. A future
controller implementation needs closed-control navigation, Confirm to open,
candidate movement, Confirm to commit, Cancel to dismiss, focus restoration,
hold/repeat behavior, single activation per press, and input-method switching.
