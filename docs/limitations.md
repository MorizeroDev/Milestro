# Known Limitations

This page records stable, user-visible limitations caused by external platform
or provider contracts. It does not track temporary test gaps or diagnostic
experiments.

## HybridInput Scroll (DeltaOnly)

These limitations apply when the selected provider reports
`HybridScrollCapability.DeltaOnly`. Unity Input System and uGUI remain the sole
production source of scroll delta.

### Delayed Elastic Release

**Observable behavior:** Elastic overscroll starts returning after the
owner-local idle delay (80 ms by default) following the last valid non-zero
scroll delta. The content can therefore appear to remain stretched briefly
after a wheel or trackpad gesture has ended.

**Cause:** Delta-only input has no gesture ID, gesture phase, or momentum phase.
Zero-delta Input Action callbacks can be interleaved with later non-zero
callbacks, so zero is not treated as gesture end.

**Workaround:** Applications can tune the owner-local release delay, maximum
overscroll, and return duration, or disable scroll Elastic for that owner. Such
tuning is an experience tradeoff rather than a reliable gesture-end signal.

**Resolution requirement:** Immediate release requires real phase metadata with
a deterministic association to the uGUI scroll event.

Milestro intentionally does not infer gesture end from zero delta, frame or
timestamp proximity, or delta ratio and direction. Native delta is not used as
a second production input source.

### Orthogonal Gesture During Residual Momentum

**Observable behavior:** After axis A has been committed, residual momentum on
A can delay or suppress a new orthogonal gesture on axis B until an idle gap
resets the axis lock.

**Cause:** Without gesture identity, the axis lock cannot distinguish a new
orthogonal gesture from drift within the existing gesture. Residual momentum
continues to refresh the gesture timeout.

**Workaround:** There is no reliable automatic workaround in delta-only mode.
Axis-lock retargeting based on dominance, accumulated distance, or hysteresis
would be an explicit heuristic with different diagonal-scroll tradeoffs.

**Resolution requirement:** A deterministic fix requires source gesture
identity or reliably associated gesture and momentum phases.

Milestro intentionally does not switch axes from a single-frame dominance
ratio, and shortening the Elastic idle delay does not end an axis lock while
residual non-zero momentum continues.

## HybridInput IME Composition And Session Control

These limitations apply to strict cancellation and ownership transfer of an
active platform composition. They do not mean that ordinary platform IME text
input is unsupported.

### macOS Candidate UI Residue

**Affected capability:** macOS strict composition cancellation during a text
owner handoff.

**Observable behavior:** The old composition does not enter either text owner,
pressing Space does not flush it, and the new owner remains usable. The system
candidate window can nevertheless remain visible after the handoff.

**Cause:** Milestro can discard marked text through the current
`NSTextInputContext`, but it does not own Unity's actual `NSTextInputClient`
context that controls the candidate UI.

**Current behavior:** The old managed ownership token is invalidated before
native cancellation, so stale callbacks cannot write into the old or new
owner. The remaining candidate window is a system UI residue rather than live
Milestro composition state.

**Resolution requirement:** Dismissing the candidate UI requires a verified
integration with Unity's actual `NSTextInputClient` owner context.

### iOS Strict Composition Control Is Unsupported

**Affected capability:** iOS strict native cancellation of an active
composition during a text owner handoff.

**Observable behavior:** When strict cancellation is required, the native
cancellation result is `Unsupported`. The ownership boundary remains
fail-closed until Unity reports a real empty-composition acknowledgement.

**Cause:** Milestro has not integrated Unity's actual `UITextInput` owner and
first responder, so it cannot safely command UIKit to discard marked text.

**Current behavior:** Milestro invalidates stale managed ownership before the
cancellation attempt and waits for the platform acknowledgement. Ordinary iOS
IME input is not classified as unsupported by this result.

**Resolution requirement:** Strict cancellation requires a verified owner seam
to Unity's active `UITextInput` first responder, or a mobile text-session model
with an equivalent ownership boundary.

### Android Strict Composition Control Is Unsupported

**Affected capability:** Android strict cancellation of the served editor's
active composition during a text owner handoff.

**Observable behavior:** Milestro cannot guarantee that pending marked text is
actively discarded through Android's served editor when ownership changes.
This limitation does not classify ordinary Android IME or Unity
`TouchScreenKeyboard` input as unsupported.

**Cause:** Milestro does not have a bridge to Unity's served `View` and live
`InputConnection` owner.

**Current behavior:** The current Android provider path does not issue a native
cancellation request (`IsRequired=false`). Managed session generations reject
stale callbacks so old-session input is not assigned to a new owner. Completion
or cancellation of pending platform composition otherwise follows Unity and
Android behavior.

**Resolution requirement:** Strict cancellation requires a verified served
`View`/`InputConnection` owner bridge, or a mobile text-session model with an
equivalent no-cross-owner boundary.
