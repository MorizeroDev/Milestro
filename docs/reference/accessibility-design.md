# TextInput accessibility design

Status: implemented by `Milestro.Components.TextInputAccessibility`
(`apps/unity-plugins/Milestro/Components/TextInputAccessibility.cs`). Tracks
the gap where `TextBoxAccessibility` publishes static text and link nodes,
while `TextInput` used to publish nothing about its value, selection, caret,
or editing state.

## How mainstream platforms model editable text

Concept-level survey of the established design languages (no source auditing):

- **UIA (Windows)**: a text control exposes `ValuePattern` (value string,
  `IsReadOnly`), `TextPattern` (document range, `GetSelection`, caret range,
  granular navigation), and for IME-aware controls `TextEditPattern`
  (composition sessions). Events: text-changed, selection-changed,
  conversion-target-changed. Narrator announces "editing" on entry and echoes
  caret movement per granularity (character / word / line).
- **NSAccessibility (macOS / iOS)**: `AXTextField` / `AXTextArea` roles with
  `AXValue`, settable `AXSelectedText` / `AXSelectedTextRange`,
  `AXInsertionPointLineNumber`. Notifications: `AXValueChanged`,
  `AXSelectedTextChanged`. VoiceOver says "is editing" while the caret session
  is active and speaks the character under the caret as it moves.
- **ATK / AT-SPI (Linux)**: split across `AtkText` (contents, caret offset,
  `get_selection`) and `AtkEditableText` (insert / delete / set). Signals:
  `text-changed::insert/delete`, `text-caret-moved`,
  `text-selection-changed`.
- **Android**: `AccessibilityNodeInfo` carries `text`, `isEditable`,
  `isPassword`, `textSelectionStart/End`; actions `ACTION_SET_TEXT`,
  `ACTION_SET_SELECTION`; events `TYPE_VIEW_TEXT_CHANGED` (with
  added/removed text) and `TYPE_VIEW_TEXT_SELECTION_CHANGED`.
- **Web / Chromium AX**: `textbox` role with `aria-multiline`, `aria-readonly`,
  `aria-placeholder`, `aria-required`, `aria-invalid`; Chromium maps native
  inputs to AX nodes with value, selection (selStart/selEnd), and editable /
  readonly / multiline / sensitive states, emitting value-changed,
  text-changed, and document-selection-changed events. Where structured
  selection is unavailable to authors, the accepted fallback is an
  `aria-live` announcement region.

Four recurring concerns fall out of every one of these:

1. **Identity and value** — role, label, current value, and state flags
   (disabled / readonly / password / multiline / required / invalid).
2. **Selection domain** — caret offset and selection range as structured data,
   with granular navigation.
3. **Editable actions** — the assistive technology can set text and selection,
   not just read them.
4. **Event stream** — value-changed, selection-changed, caret-moved,
   composition begin/update/commit, editing begin/end.

Concern 3 (assistive-technology-initiated editing) is out of reach for a Unity
component today and is listed under Future work. The rest is achievable.

## What Unity 6000.3 can express

The built-in Accessibility module (`UnityEngine.Accessibility`, gated by
`MILESTRO_UNITY_ACCESSIBILITY`) offers:

| API | What it gives us |
| --- | --- |
| `AccessibilityRole.TextField` / `SearchField` | Identity (concern 1) |
| `AccessibilityNode.label` / `hint` | Name and guidance |
| `AccessibilityNode.value` (`string`) | Current value — the only structured text channel |
| `AccessibilityNode.state` | `Disabled`, `Selected`, `Expanded` only — no readonly/password/multiline |
| `AccessibilityNode.frame` / `frameGetter` | Screen-space rect for focus ring / touch exploration |
| `AccessibilityNode.isFocused`, `focusChanged` event | Screen-reader focus tracking |
| `AccessibilityNode.invoked` / `dismissed` events | Screen reader can request edit start / end |
| `AccessibilityNode.allowsDirectInteraction` | Pass-through touch while editing |
| `AssistiveSupport.notificationDispatcher.SendAnnouncement(string)` | Live-region-style spoken announcements |

Gaps: no structured selection or caret property, no granular caret-movement
callbacks, no readonly/password state flags, no insert/delete semantics.
Everything missing collapses into one mechanism: `SendAnnouncement`. This is
the same translation web apps use with `aria-live` regions and the same shape
as Android's `TYPE_VIEW_TEXT_SELECTION_CHANGED` event — publishing selection as
speech rather than as structured data is the industry-standard fallback when
the host accessibility tree has no text-pattern equivalent.

## Implementation

`Milestro.Components.TextInputAccessibility` is a uGUI `Selectable` on the same
GameObject as `TextInput`, compiled behind `#if UNITY_6000_3_OR_NEWER &&
MILESTRO_UNITY_ACCESSIBILITY` with the same warning-without-module fallback as
`TextBoxAccessibility`. Its label, announcement templates, and text echo limit
are serialized so projects can localize them.

### Node publication

One `AccessibilityNode` per `TextInput` (no container, unlike TextBox):

- `role`: always `TextField`. A `SearchField` mapping is not implemented.
- `label`: the serialized label; otherwise the text of a uGUI `Text`
  placeholder; otherwise the GameObject name.
- `value`: the committed text. When `maskInput` is set, publishes the mask
  character repeated once per grapheme cluster, so length is exposed but
  contents are not.
- `state`: `Selected` while the input holds edit focus; otherwise no state
  flag is published. Read-only is expressed through the hint rather than the
  `Disabled` state.
- `hint`: includes the read-only and password hints when applicable.
  Multiline is not represented.
- `frameGetter`: the input's screen-space rect (same helper style as
  `GetTextBoxScreenRect`).
- `allowsDirectInteraction`: true while an editing session is active so touch
  reaches the caret.

### Reverse channel: screen-reader-initiated editing

- `invoked` requests the same editing path that pointer focus uses. The
  transition is announced when edit focus is acquired.
- `dismissed` ends editing and clears EventSystem selection.

The component does not subscribe to `focusChanged`; announcements follow the
`TextInput` editing snapshot rather than screen-reader focus.

### Snapshot diffing and announcements

Each frame the component captures a `TextInputAccessibilitySnapshot` from the
`TextInput` and its `InputBox`:

```
Text                     | Cursor.Utf16Offset
SelectionStart/EndUtf16  | compositionActive
editingActive (IsDispatcherFocused)
```

Diffs against the previous snapshot drive announcements:

- **Editing begin / end** — "editing" / "editing ended" (UIA editing state,
  VoiceOver "is editing").
- **Value changed** — a common-prefix/suffix diff announces the inserted or
  removed text (ATK `text-changed::insert/delete` semantics). Text longer than
  the echo limit is announced as a count. Counts are grapheme clusters, not
  UTF-16 code units.
- **Caret moved, no selection** — uses the change in UTF-16 offset to choose a
  single-character, line, or word echo. It cannot distinguish keyboard from
  pointer movement.
- **Selection changed** — announces the selected text when small, otherwise
  "N characters selected", counted in grapheme clusters. Removing a selection
  without moving the caret produces no separate collapse announcement.
- **Composition (IME)** — pre-edit updates are silent; the committed change is
  announced through the value-diff path.
- **Password** — a masked value change announces one localized password
  character, never the plaintext. Masked selection still reports a grapheme
  count.

Announcements are generated once per frame and sent immediately. There is no
rate limiter or per-category coalescing beyond the frame boundary.

## Limits and current gaps

- Structured selection (a real TextPattern equivalent) requires native
  bridges beyond Unity's module: `AccessibilityNodeInfo` extras on Android,
  `UIAccessibility` informal text-input protocol on iOS. The announcement
  design above is intentionally shaped like those platforms' event models so
  a bridge can replace speech with structured data later without changing
  component semantics.
- Screen-reader-driven text mutation (AT types into the field) is not
  expressible through `UnityEngine.Accessibility`; out of scope.
- Caret/selection frame publishing for Braille displays is also bridge
  territory; not covered by the module.
- `SearchField` roles, multiline hints, and a disabled state are not mapped yet.
- The label fallback reads only a uGUI `Text` placeholder, not a `TextBox` or
  TMP placeholder.
- Pointer-driven caret placement still produces caret-echo announcements;
  movement-cause filtering needs device validation.
- `value` is synchronized every frame while enabled. Some screen readers may
  re-read it on focus more often than desired.
