# Hybrid Input

Milestro routes keyboard, committed text, IME composition, and IME ownership
through one process-wide dispatcher. `TextInput` instances register private
frame sinks and only the focused owner receives text events. Provider, device,
application-focus, and component-lifecycle resets clear pending state without
leaking input to another owner.

## Provider Selection

Automatic selection follows the active module on the single active Unity
`EventSystem`:

| Active module | Provider ID | Availability |
| --- | --- | --- |
| `StandaloneInputModule` | `legacy` | Unity build has `ENABLE_LEGACY_INPUT_MANAGER` |
| `InputSystemUIInputModule` | `input-system` | `com.unity.inputsystem` is `[1.18.0,2.0.0)` |

Selection fails closed when there is no matching provider, more than one active
event system, or a tie between equally ranked providers. Inspect the current
state with:

```csharp
HybridInputDiagnostics diagnostics = HybridInputRuntime.Diagnostics;
```

`SelectionStatus` distinguishes selected, no-match, conflict, missing-override,
and rejected-override states. `ProviderId`, `ProviderKind`, `Capabilities`,
`ScrollCapability`, `ActiveModuleType`, and `EventSystemCount` describe the
selected provider and environment. `ImeCancellationResult` reports the most
recent platform composition-cancellation result, and
`ImeCancellationFailureCount` counts results other than `Succeeded`.

Applications can request a registered provider by ID:

```csharp
HybridInputRuntime.SetProviderOverride("legacy");
HybridInputRuntime.SetProviderOverride("input-system");
HybridInputRuntime.SetProviderOverride(null); // Return to automatic selection.
```

An override does not bypass environment matching. For example, requesting
`input-system` while the active module is `StandaloneInputModule` is rejected.
Custom integrations implement `IHybridInputProvider` and register through
`HybridInputRuntime.RegisterProvider`; dispose the returned registration to
remove the provider.

## Capabilities

Both built-in providers expose immutable per-frame key state, key edges,
committed text, composition, and IME control. The optional Input System provider
also reports `ScrollDelta` with `HybridScrollCapability.DeltaOnly`.

Delta-only means Milestro does not yet claim scroll device classification,
gesture or momentum phases, gesture IDs, or scroll capture. Existing uGUI
scroll target, bubbling, and axis-lock behavior remains responsible for scroll
delivery. Code must inspect `HybridInputCapabilities` and
`HybridScrollCapability` instead of inferring richer behavior from the provider
kind.

## Optional Input System Assembly

The base `Milestro` assembly does not reference `Unity.InputSystem`. The
`Milestro.InputSystem` assembly contains the optional adapter and has a Unity
version define for `com.unity.inputsystem` `[1.18.0,2.0.0)`. A project without
that package can import and compile the core runtime; the optional assembly is
not enabled, and automatic selection can still use the legacy provider when the
Legacy Input Manager is available.

The Gradle release package recursively includes the complete `Milestro`,
`Milestro.Editor`, `Milestro.Experimental`, `Milestro.InputSystem`, and
`Resources` roots, including their existing top-level metadata. It excludes the
`Milestro.Tests` and `Milestro.InputSystem.Tests` roots.

## Text And Composition Ordering

Committed text and composition are independent ordered events. A committed-text
event ends the preedit that precedes it. A later composition event in the same
frame establishes the remaining or next preedit, allowing partial IME commits
to apply committed text and display the remaining composition without a frame
delay. Providers must not assume that a commit is paired with a separate empty
composition callback.

## Platform IME Cancellation

Losing text focus cancels an unconfirmed platform composition. Milestro
invalidates the old ownership token before requesting native cancellation, so
callbacks from that session cannot enter the old or new owner. When native
cancellation fails or is unavailable, the result is visible through diagnostics
and boundary completion remains fail-closed until a real empty-composition
acknowledgement arrives.

On macOS, cancellation prevents the old composition from being flushed into
either text owner, but the candidate window may remain visible. Dismissing that
candidate UI through Unity's actual `NSTextInputClient` owner context is a known
limitation tracked by task #112. Windows supports native cancellation of the
focused composition during handoff. iOS currently reports `Unsupported` because
Unity's actual `UITextInput` owner is not integrated.

## Breaking Migration

The old `Milestro.InputManagement.HybridInput` and `InputStringReceiver` path
was removed without a compatibility shim. `TextInput` no longer requires or
polls an `InputStringReceiver`; it acquires dispatcher focus and IME ownership
internally.

When updating an existing Unity project:

1. Remove scripts and serialized component references to
   `InputStringReceiver`.
2. Replace old held-key calls with `HybridInputRuntime.IsKeyPressed` where that
   process-wide snapshot is appropriate. There is no public replacement for
   consuming Milestro's private text frames or key edges; application gameplay
   input should continue through the application's own Unity input layer.
3. Use one active `EventSystem` with the module matching the intended provider.
4. Install `com.unity.inputsystem` `[1.18.0,2.0.0)` only when the project uses
   `InputSystemUIInputModule`; it is not a core Milestro dependency.
