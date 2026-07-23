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
| `InputSystemUIInputModule` | `input-system` | `com.unity.inputsystem` is `[1.16.0,2.0.0-0)` |
| `InputSystemUIInputModule` | `legacy` delta-only fallback | Active Input Handling is Both and the package is outside the supported range |

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
`InputSystemPackageStatus` independently reports `NotApplicable`, `Missing`,
`BelowMinimum`, `Supported`, or `Unsupported` for the optional package; it does
not replace or reinterpret provider-selection fields.
`LastDiagnostic` and `DiagnosticCount` expose bounded-dispatch failures such as
unsupported session isolation, ring overflow, work exhaustion, and listener
exceptions.

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

## Strict Focus Sessions

Providers that publish focused key, committed-text, or composition events must
also implement `IHybridInputFocusSessionProvider`. The dispatcher supplies a
new immutable `IHybridInputEventSink` to every `BeginFocusSession` call. A
provider callback must capture that sink when the source callback is bound; it
must not look up a mutable current sink when the callback eventually runs.

The dispatcher seals the old sink before flushing the accepted tail of a focus
session. Calls through that sink after sealing are dropped before event
sequence, input-ring, or pressed-key state changes. Provider changes,
dispatcher reset, sink disposal, and device-session replacement also invalidate
old sinks. Provider callbacks are main-thread-only in this contract.

The Input System adapter implements strict focus sessions by binding fresh text,
composition, and key-edge callback closures for each session. Key edges are
captured from the source after-update callback; `Collect()` only snapshots held
state and cannot retag an old edge. The ordinary legacy provider and the
Both-mode compatibility fallback remain available for provider selection and
direct custom use, but Unity's polled
`Input.inputString` API does not expose capture-time ownership. It therefore
does not currently implement the focus-session contract and cannot acquire
`TextInput` focus. Failed admission reports
`HybridInputDiagnosticCode.SessionIsolationUnsupported`; there is no unscoped
or best-effort fallback.

## Bounded Dispatch

Focus intents, input delivery, and lifecycle callbacks run through one
non-recursive transaction pump. It uses a fixed 128-record input ring, a fixed
64-record notification ring, and a 256-step outer transaction limit. The 129th
input event drops the complete undelivered batch, seals and releases that
session, and reports `InputEventBufferOverflow`. Listener loops, notification
overflow, and listener exceptions stop the remaining callbacks in the current
outer transaction; the guard is restored for the next ordinary update.

## Capabilities

Both built-in providers describe key state, key edges, committed text,
composition, and IME control capabilities. Capability flags do not imply strict
focus-session support; check the focus-session contract described above. The
optional Input System provider also reports `ScrollDelta` with
`HybridScrollCapability.DeltaOnly`.

Delta-only means Milestro does not yet claim scroll device classification,
gesture or momentum phases, gesture IDs, or scroll capture. Existing uGUI
scroll target, bubbling, and axis-lock behavior remains responsible for scroll
delivery. Code must inspect `HybridInputCapabilities` and
`HybridScrollCapability` instead of inferring richer behavior from the provider
kind.

See [limitations.md](limitations.md#hybridinput-scroll-deltaonly) for the
user-visible release-timing and axis-lock limitations of delta-only scroll.

Scroll owners continue to receive one uGUI `PointerEventData`. An optional
`IHybridScrollInputProvider` may enrich that same event with capability and
phase metadata; it is not a second delta source. A mismatched delta or unusable
phased metadata is rejected instead of being merged or guessed. Delta-only
Elastic uses the owner-local unscaled release delay, while phased providers
hold until the last real gesture and momentum phase ends. `Unsupported`
disables and settles presentation-only Elastic without disabling normal clamped
scrolling.

## Optional Input System Assembly

The base `Milestro` assembly does not reference `Unity.InputSystem`. The
`Milestro.InputSystem` assembly contains the optional adapter and has a Unity
version define for `com.unity.inputsystem` `[1.16.0,2.0.0-0)`. A project without
that package can import and compile the core runtime; the optional assembly is
not enabled, and automatic selection can still use the legacy provider when the
Legacy Input Manager is available.

When Active Input Handling is Both and the package is missing, below `1.16.0`
(including `1.16.0` prereleases), unparseable, or `2.0.0-0` and newer, an exact
`InputSystemUIInputModule` match uses the core legacy provider only as a
delta-only scroll route. It enriches the existing uGUI
`PointerEventData.scrollDelta`; it does not read a second source. Strict
`TextInput` focus remains unavailable on that fallback. The Editor reports a
warning. With Input System Package (New) only, the same unsupported states are
Editor errors and fail the player build. Diagnostics name the current version,
the `1.16.0` minimum, and three remedies: upgrade to a compatible package,
change Active Input Handling to Both, or change it to Input Manager (Old).
Legacy-only projects do not receive this warning and retain ordinary
`StandaloneInputModule` behavior.

This compatibility range follows Unity's package-version comparison. A valid
1.x prerelease above the `1.16.0` floor therefore compiles and selects the same
optional provider; prerelease contents are not separately runtime-certified.
Official stable 1.x releases provide the API/runtime certification matrix.

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

Losing text focus ends the managed composition ownership session. Milestro
invalidates the old ownership token before any required native cancellation
request, so callbacks from that session cannot enter the old or new owner. On
platforms that require strict native cancellation, a failed or unavailable
request is visible through diagnostics and boundary completion remains
fail-closed until a real empty-composition acknowledgement arrives.

Windows supports native cancellation of the focused composition during
handoff. See [limitations.md](limitations.md#hybridinput-ime-composition-and-session-control)
for platform-specific IME composition and session-control limitations.

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
4. Install `com.unity.inputsystem` `[1.16.0,2.0.0-0)` when the project uses
   `InputSystemUIInputModule`; it is not a core Milestro dependency.
