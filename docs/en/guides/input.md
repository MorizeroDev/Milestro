# Input and IME

[English](input.md) | [简体中文](../../zh/guides/input.md)

`Milestro.Components.TextInput` is the editable text component. It is not a
TextMesh Pro input field assembled from child graphics: the component owns an
internal native editable text box, draws it into a render texture, and routes
keyboard, composition, and IME ownership through Milestro's HybridInput
dispatcher.

Read [Add your first input field](../getting-started/first-input.md) for the
minimal scene setup. This page covers the parameters and behavior.

## Required setup

- Exactly one active `EventSystem` in the scene.
- `InputSystemUIInputModule` for editable focus.
- `com.unity.inputsystem` in `[1.16.0,2.0.0-0)`.
- Active Input Handling set to **Input System Package (New)** or **Both**.

`StandaloneInputModule` can still provide normal uGUI navigation, but the legacy
polled provider cannot prove capture-time ownership and therefore cannot
acquire a strict `TextInput` focus session. See
[Hybrid input](../reference/hybrid-input.md) for the contract.

## Component parameters

### Appearance

| Property | Purpose |
| --- | --- |
| `size` | Font size |
| `fontFamilies` | Ordered font-family list; defaults to `system-ui` |
| `textColor` | Text color |
| `caretColor`, `caretWidth`, `blinkInterval` | Caret appearance |
| `selectionColor`, `selectionEnabled` | Selection behavior |
| `textAlignment` | `Left`, `Center`, or `Right` |
| `lineMode` | `SingleLine` or `MultiLine` |
| `wrapMode` | `Wrap` or `NoWrap`; single-line forces `NoWrap` |
| `textOverflow`, `ellipsisString` | Overflow behavior |
| `locale` | Locale for shaping and fallback |
| `srgb` | Selects linear or gamma color space for the input surface |

### Form behavior

| Property | Purpose |
| --- | --- |
| `Text` | Canonical committed value |
| `SetTextWithoutNotify(string)` | Change the value without firing `onValueChanged` |
| `placeholder` | Separate uGUI `Graphic` shown while empty |
| `characterLimit` | Maximum Unicode scalar count, not a grapheme-cluster count; `0` is unlimited |
| `contentType` | Standard, autocorrected, numeric, alphanumeric, name, email, password, PIN, or custom |
| `characterValidation` | Built-in character filter; setting it changes `contentType` to `Custom` |
| `keyboardType` | `TouchScreenKeyboardType` passed to mobile/keyboard providers |
| `readOnly` | Prevents editing while retaining focus, caret navigation, and selection |
| `maskInput`, `maskChar`, `isPassword` | Password-style display |
| `allowCopy` | Allows copying the selected value |
| `submitOnEnter` | In multiline mode, Enter submits instead of inserting a newline; Shift+Enter inserts a newline |
| `smoothScroll`, `scrollTweenDurationSeconds` | Caret-following and wheel scroll animation |
| `shouldHideMobileInput` | Hides the platform-native input box and edits the Unity field directly |

`TextInput` also exposes `Submit()`. Submission retains focus; your listener may
move focus or close the form, which then produces the normal `onEndEdit` and
`onFocusLost` sequence.

### Focus, selection, and read-only state

These settings control different behavior; there is no separate TMP-style
`interactable` state to reproduce:

- `readOnly = true` still allows focus, caret navigation, range selection, and
  copying when `selectionEnabled` and `allowCopy` allow them. It rejects edits
  and submission and does not open the mobile soft keyboard.
- `selectionEnabled = false` disables range selection, not focus or editing.
  The caret and mobile keyboard remain available, and copying still requires a
  selection permitted by `allowCopy`.
- `enabled = false`, or an inactive GameObject, rejects dispatcher focus and
  closes the soft keyboard. Disabling an already focused input ends that
  session once. Re-enable it and select it through the normal `EventSystem`
  path to focus it again.

Pointer selection and programmatic uGUI selection enter editing directly. There
is no intermediate selected-but-not-editing state for ordinary mouse, touch, or
programmatic selection.

## Events

| Event | Payload | When it fires |
| --- | --- | --- |
| `onValueChanged` | `string` | After the committed canonical value changes |
| `onEndEdit` | `string` | A focus session ends with the final value |
| `onSubmit` | `string` | Single-line Enter, submitted multiline Enter, mobile Done, or `Submit()` |
| `onFocusGained` | none | The input acquires dispatcher focus |
| `onFocusLost` | none | The input loses dispatcher focus |
| `onTouchScreenKeyboardStatusChanged` | `TouchScreenKeyboard.Status` | Soft keyboard status transition |

The event fields are getter-only `UnityEvent` instances. Use `AddListener` and
`RemoveListener` at runtime. Inspector listeners are serialized with the scene
or prefab; runtime listeners must be registered again after a domain reload.

Callbacks observe committed state. `onValueChanged` runs after `Text` already
contains the canonical value. Assigning the same value does not invoke it.
Assigning a different value invokes it once, even while the component is
disabled or inactive. `SetTextWithoutNotify`, deserialization, scene loading,
and inspector validation are silent.

IME preedit is not committed into `Text` and does not raise `onValueChanged`.
An actual session end sends its final snapshot through `onEndEdit` once, then
raises `onFocusLost`. A submit listener may close, disable, destroy, switch, or
reopen inputs without allowing the old session's cleanup to close the new one.

## Validation

Validation happens in layers:

1. line canonicalization for the current line mode;
2. built-in `characterValidation` rules;
3. `characterLimit`;
4. `onValidateInput`, when assigned;
5. `onValidateEdit`, when assigned.

### `onValidateInput`

```csharp
input.onValidateInput = (proposedText, charIndex, codePoint) =>
{
    // Return the accepted code point, a replacement code point, or 0.
    return codePoint == ' ' ? codePoint : 0;
};
```

This callback is scalar-based. A ZWJ emoji produces several callbacks,
including its joiner code points, so it is not delivered atomically.

### `onValidateEdit`

Use `onValidateEdit` when a complete sequence or paste must be accepted or
rejected as one edit:

```csharp
using Milestro.Model;

input.onValidateEdit = edit =>
    edit.InsertedText == "👨‍👩‍👧‍👦"
        ? TextInputEditResult.Reject()
        : TextInputEditResult.Accept();
```

`TextInputEditContext` exposes:

- `CurrentText`
- `ProposedText`
- `InsertedText`
- `SelectionStart`, `SelectionEnd`, `HasSelection`
- `Source` (`Typing`, `Paste`, `ImeCommit`, or `Programmatic`)

`TextInputEditResult.Accept()` accepts, `Reject()` leaves the value and
selection unchanged, and `Replace(text)` substitutes the complete candidate.
The replacement still passes through canonicalization, limits, and built-in
validation, but `onValidateEdit` is not called recursively.

Changing limits, content rules, or `onValidateInput` revalidates the current
value and clears undo history so old edits cannot restore disallowed text. IME
preedit remains unrestricted until commit.

## Presets and custom rules

`contentType` selects a preset:

- `Standard`, `Autocorrected`, `Custom`
- `IntegerNumber`, `DecimalNumber`, `Alphanumeric`
- `Name`, `EmailAddress`
- `Password`, `Pin`

Numeric validation uses ASCII digits, an optional leading minus, and `.` for
decimals. PIN accepts digits only. Name validation accepts Unicode letters and
combining marks and capitalizes word starts. Email validation filters characters
and duplicate `@`/adjacent dots, but the application must still validate the
complete address on submission.

Filtering only constrains editing form; it does not prove that the final value
is parseable. Empty text and incomplete values such as `-`, `.`, and `-.` must
be handled by the consumer with `TryParse` or equivalent validation. Formatting
and parsing must use a consistent culture. In particular, the decimal filter
always accepts the ASCII period, so invariant-culture formatting and parsing are
usually the simplest match.

Password and PIN presets enable masking and disable copying. Presets other than
Standard, Autocorrected, and Custom select single-line input. Overriding
`characterValidation` or `keyboardType` switches `contentType` to `Custom`;
setting a keyboard type does not validate the text.

## Mobile keyboard

On platforms where `TouchScreenKeyboard.isSupported` is true, focusing the
component opens a keyboard session with the current keyboard type,
autocorrection, multiline, masking, and read-only settings.

`characterLimit` counts Unicode scalar values. It does not count UTF-16 code
units or user-perceived grapheme clusters: a supplementary-plane emoji is one
scalar, while a multi-scalar ZWJ emoji consumes multiple units. Milestro does
not forward this limit to `TouchScreenKeyboard`, whose platform counting is not
required to use the same scalar semantics. Managed canonicalization and
validation remain the single strict limit when native snapshots are applied.

`shouldHideMobileInput` defaults to `true`, matching TextMesh Pro's default:

- `true`: hide the platform-native input field; edit the Unity-rendered field
  directly, with text and selection mirrored into the keyboard for context.
- `false`: show the platform-native input field and poll its text back through
  the same canonicalization and validation pipeline.

Android hardware keyboards suppress the soft keyboard in the same way as TMP.
Blurring closes the session; Done submits and closes; Cancel or LostFocus closes
without submitting.

`touchScreenKeyboard` exposes the active `TouchScreenKeyboard` or `null`.
`onTouchScreenKeyboardStatusChanged` fires once for `Visible`, `Done`,
`Canceled`, or `LostFocus`.

## Multi-line behavior

For `TextInputLineMode.MultiLine`:

- `wrapMode = NoWrap` allows horizontal scrolling.
- `wrapMode = Wrap` soft-wraps at the viewport width.
- Enter inserts a newline by default.
- `submitOnEnter = true` makes Enter submit; Shift+Enter inserts a newline.
- Enter used to confirm an IME composition does not submit.

## Gamepad selection and editing: future work

**Status: no gamepad is available for verification. Dedicated adaptation is not
implemented and has not been tested on a device. Existing uGUI navigation or
controller support in other controls does not mean this input flow is
supported.**

The expected future interaction is recorded here without introducing a current
API or changing ordinary pointer/programmatic selection:

1. Directional navigation selects and highlights a field without starting text
   editing or opening the soft keyboard.
2. Confirm on the selected field starts editing; that same Confirm must not also
   submit the new session.
3. While editing, text navigation does not also move UI focus to another field.
   Gamepad caret behavior still needs design.
4. Confirm submits and exits editing; Cancel exits without submitting.
5. Exiting editing returns to UI navigation with the field still selected.
6. Future implementation must validate key repeat, input-method switches, field
   switches, focus loss, and session reentrancy.

A possible Windows keyboard mapping for this future model is:

- While a field is selected but not editing, arrow keys move the active UI
  navigation selection.
- Enter starts editing; that same Enter must not also submit the new session.
- While editing, arrow keys move the text caret, or extend its selection with
  the appropriate modifiers, and must not also invoke `Selectable` navigation.
- Enter while editing follows the future Confirm behavior above; Escape exits
  without submitting and returns to the selected, non-editing state.

`TextInputAccessibility` inherits `Selectable`, so this combination needs
explicit move-event arbitration before it can claim to support the model. That
arbitration is not currently implemented or validated with a real
`InputSystemUIInputModule`, and ordinary `Button` navigation must remain
unaffected when it is implemented.

No gamepad bridge, two-stage activation property, key-release barrier, or form
navigation router is currently provided by this flow.

## `TextInput` versus low-level `InputBox`

`Milestro.Skia.TextLayout.InputBox` is the low-level native-backed editing
model. It is useful when building a custom renderer, not as a scene component.
It owns:

- the committed text and caret;
- selection and hit testing;
- undo/redo and composition;
- soft wrap, overflow, masking, viewport, and caret visibility;
- metrics and line metrics for custom drawing.

The component manages the `InputBox` lifecycle, viewport, styling, input focus,
pointer handling, validation, and the Unity events for you. Start with
`TextInput` unless you are implementing a custom text field.

The low-level surface includes `Text`, `Cursor`, `Selection`, `SetViewport`,
`SetSoftWrap`, `SetTextOverflow`, `InsertText`, `SetComposition`,
`CommitComposition`, `Undo`, `Redo`, selection movement, `HitTest`,
`EnsureCaretVisible`, `GetCaretRect`, `GetMetrics`, `GetLineCount`, and
`GetLineMetrics`. See [Unity components and events](../reference/components.md)
for the type-level reference.
