# TextInput Accessibility Design

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

## Design

New component `Milestro.Components.TextInputAccessibility`, parallel to
`TextBoxAccessibility`: a uGUI `Selectable` on the same GameObject as the
`TextInput`, compiled behind `#if UNITY_6000_3_OR_NEWER &&
MILESTRO_UNITY_ACCESSIBILITY` with the same warning-without-module fallback.
Inspector fields follow the existing precedent (`m_screenReaderEnabled`,
`m_linkHint`): serialized, localizable strings for every announcement
template.

### Node publication

One `AccessibilityNode` per `TextInput` (no container, unlike TextBox):

- `role`: `TextField`, or `SearchField` when `contentType` maps to search.
- `label`: explicit serialized field; falls back to placeholder text if the
  placeholder `Graphic` exposes one, then to the GameObject name.
- `value`: current text. When `maskInput` is set, publishes the masked
  rendering (mask char repeated) — mirroring Android `isPassword` and
  Chromium's sensitive state: length is exposed, contents are not.
- `state`: `Disabled` when interaction is off; `Selected` while the input
  holds edit focus.
- `hint`: composed from properties the `state` enum cannot express —
  readonly, multiline, password — e.g. `"Read-only text field"`,
  `"Password field"`.
- `frameGetter`: the input's screen-space rect (same helper style as
  `GetTextBoxScreenRect`).
- `allowsDirectInteraction`: true while an editing session is active so touch
  reaches the caret.

### Reverse channel: screen-reader-initiated editing

- `invoked` → begin editing (route focus through the same path
  `OnSelect`/`AcquireDispatcherFocus` uses) and announce the "editing" hint.
- `dismissed` → end editing (`ReleaseInputFocus` path), announce end-of-edit
  with the final value (the spoken equivalent of `onEndEdit`).
- `focusChanged` → track screen-reader focus for the frame highlight and to
  gate announcements, mirroring `OnAccessibilityFocus` in
  `TextBoxAccessibility`.

### Snapshot diffing and announcements

Follow the repository's existing snapshot pattern
(`TextBoxContentSnapshot`, `TextInputFrameState`). Each frame the component
captures a `TextInputAccessibilitySnapshot` from `InputBox`:

```
Text (version or hash)   | Cursor.Utf16Offset, Affinity
Selection (Start/End/HasSelection, via Utf8ToUtf16)
compositionActive        | editingActive (IsDispatcherFocused)
```

Diffs against the previous snapshot drive announcements:

- **Editing begin / end** — "editing" / "editing ended" (UIA editing state,
  VoiceOver "is editing").
- **Value changed** — classify the diff: insertion announces the inserted
  text, deletion announces the removed text (ATK `text-changed::insert/delete`
  semantics). Large pastes and IME commits announce a summary
  ("pasted, 42 characters") instead of the full payload.
- **Caret moved, no selection** — announce per movement granularity:
  character move speaks the character now right of the caret, word move the
  word, line move the line, document edge a boundary hint. This matches
  Narrator / VoiceOver caret echo.
- **Selection changed** — announce the selected text when small, otherwise
  "N characters selected"; collapse announces the new caret context.
- **Composition (IME)** — commit announces the committed string; pre-edit
  updates are silent by default behind a serialized toggle.
- **Password** — all announcements use the mask char or a localized
  "bullet"; the plaintext value never reaches the announcement channel.

Announcements are coalesced per frame and rate-limited so sustained typing
produces a steady cadence rather than a queue backlog; a cheap policy is to
keep only the latest pending announcement per category.

## Limits and future work

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

## Open questions

- Should `value` update every frame or only on commit/focus loss? Every
  change matches platform norms but risks redundant speech on screen readers
  that re-read `value` on focus; needs device validation.
- Granularity announcements need caret-movement cause (keyboard vs pointer);
  pointer-driven caret placement probably should not echo.
- Localization surface: one serialized template per announcement kind, or a
  shared string table asset?
