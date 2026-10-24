# Add your first input field

`Milestro.Components.TextInput` is the editable uGUI component. It handles
caret, selection, composition text, clipboard shortcuts, scrolling, and the
mobile soft keyboard. Its keyboard and IME events come from one process-wide
HybridInput dispatcher.

Add `TextInput`, not an `InputBox`. The native `InputBox` is the low-level
editing model owned by `TextInput`, not a scene component.

## 1. Configure an EventSystem

The scene must contain exactly one active `EventSystem`:

- **GameObject** → **UI** → **Event System**.
- Add or replace the input module with `InputSystemUIInputModule`.
- Install `com.unity.inputsystem` in the version range `[1.16.0,2.0.0-0)`.

With Active Input Handling set to **Input System Package (New)** or **Both**,
this module gives `TextInput` the strict focus session it needs. A legacy
`StandaloneInputModule` is not enough for editable `TextInput` focus.

For provider details and migration from the old input components, see
[Hybrid input](../reference/hybrid-input.md).

## 2. Create the input field

1. Create a UI GameObject under a Canvas.
2. Add **Milestro** → **Text Input**.
3. Set its `RectTransform` size, for example `600 × 80`.
4. Set `Size`, `Text Color`, and `Caret Color` in the inspector.
5. Leave `Line Mode` as `Single Line` for a one-line field.

`TextInput` automatically creates and owns the internal editable text box. You
do not add a separate `InputBox` component, a child `Text`, or a child
`TMP_InputField`; the component renders its own value, caret, and selection.

The optional `Placeholder` field accepts a separate uGUI `Graphic`, such as a
`TextBox` or a Unity `Text` component. Place it inside the input's bounds and
disable its raycast target; Milestro controls whether it is visible.

## 3. Subscribe to events

Create `FirstInput.cs` and attach it to the input GameObject:

```csharp
using Milestro.Components;
using UnityEngine;

public sealed class FirstInput : MonoBehaviour
{
    [SerializeField] private TextInput input = null;

    private void OnEnable()
    {
        input.onValueChanged.AddListener(HandleValueChanged);
        input.onSubmit.AddListener(HandleSubmitted);
        input.onFocusGained.AddListener(HandleFocusGained);
        input.onFocusLost.AddListener(HandleFocusLost);
    }

    private void OnDisable()
    {
        input.onValueChanged.RemoveListener(HandleValueChanged);
        input.onSubmit.RemoveListener(HandleSubmitted);
        input.onFocusGained.RemoveListener(HandleFocusGained);
        input.onFocusLost.RemoveListener(HandleFocusLost);
    }

    private void HandleValueChanged(string value)
    {
        Debug.Log($"Value changed: {value}");
    }

    private void HandleSubmitted(string value)
    {
        Debug.Log($"Submitted: {value}");
    }

    private void HandleFocusGained()
    {
        Debug.Log("Input focused");
    }

    private void HandleFocusLost()
    {
        Debug.Log("Input lost focus");
    }
}
```

Assign the field and enter Play Mode. Click the field, type, and press Enter.
For a single-line input, Enter submits after any value change from the same
input frame.

## 4. Set and read text from code

`TextInput.Text` is the canonical committed value:

```csharp
input.Text = "Initial value";
var value = input.Text;
```

Assigning a different canonical value invokes `onValueChanged` once. Assigning
the same value does not. Use `SetTextWithoutNotify` for updates that must not
fire the event:

```csharp
input.SetTextWithoutNotify("Initial value");
```

The canonicalization and validation pipeline also applies to programmatic
assignments, so line breaks, invalid UTF-16, character limits, and configured
validation rules still apply.

## 5. Common form settings

The frequently used properties are:

| Setting | Property | Notes |
| --- | --- | --- |
| Character limit | `characterLimit` | `0` means unlimited; counts Unicode scalar values. |
| Preset behavior | `contentType` | Numeric, email, name, password, PIN, and other presets. |
| Keyboard type | `keyboardType` | Passed to the mobile soft keyboard. |
| Read-only | `readOnly` | Prevents editing but keeps the text selectable. |
| Masking | `maskInput`, `maskChar` | Password-style display. |
| Submit on Enter | `submitOnEnter` | Single-line Enter submits automatically; multiline can opt in. |
| Selection | `selectionEnabled` | Enables pointer and keyboard selection. |
| Scrolling | `lineMode`, `wrapMode`, `textOverflow` | Single or multiline, wrapped or horizontal, clip/ellipsis/overflow. |

For custom validation, use `onValidateInput` for per-code-point filtering or
`onValidateEdit` when a complete replacement (including grapheme clusters and
pastes) must be accepted or rejected. See [Input and IME](../guides/input.md).

## 6. Mobile keyboard

On platforms where `TouchScreenKeyboard.isSupported` is true, focusing the
input opens the soft keyboard. `shouldHideMobileInput` defaults to `true`, so
the keyboard edits the Unity-rendered field directly while the extra native
input box stays hidden. Set it to `false` to use the platform-native input box.

Use `onTouchScreenKeyboardStatusChanged` to observe the keyboard session. The
soft keyboard is ordinary platform text input; strict native composition
cancellation remains platform-limited. See
[Known limitations](../reference/limitations.md#hybridinput-ime-composition-and-session-control).
