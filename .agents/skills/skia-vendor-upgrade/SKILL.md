---
name: skia-vendor-upgrade
description: Upgrades Milestro's Skia core and vendored text modules together. Use when changing the Skia revision, merging SkParagraph or SkUnicode upstream changes, or validating a Skia vendor migration.
---

# Skia vendor upgrade

Read `ext/skmodules/README.md` for the version contract, overlay policy, update
commands, and validation targets. Use the existing vendor script rather than
recreating its synchronization logic. Resolve paths from the repository root.

## Establish the comparison

Record the worktree state, requested Skia commit, old `vendor-meta`, and Skia HEAD.
Inspect the Skia checkout's status before changing it. Preserve user edits.
If dependencies were already synchronized, verify relevant DEPS revisions rather
than resynchronizing them. Compare upstream changes across both modules, exported
headers, GN flags, and the core APIs called by Milestro.

Retain a reproducible baseline for affected tests when practical. An old executable
may contain older Milestro code or load another dynamic library: record its
provenance and the library it actually loads.

Completion: every changed upstream interface used by Milestro has a migration
decision, and the old and new dependency revisions are identified.

## Merge and synchronize

Read `overlay_paths` in `scripts/vendor-skia-modules.sh`. Merge the old upstream
source, current vendor tree, and new upstream source for each changed overlay.
Review clean automatic merges as well as conflicts. Keep coupled headers and
implementations together; update the overlay list if a new header needs preserving.
Pass `--merged-overlays` only after completing that review.

For Run storage changes, inspect callers that construct runs, mutate glyph data,
copy cached runs, or index offsets. Preserve zero-glyph runs, sentinel positions
and cluster indexes, optional offsets, Ruby's detached writable data, and vertical
run allocation. Verify relocation traits against actual members and containers.
Cached unresolved codepoints must remain immutable when another paragraph reshapes.

Synchronize non-overlay files with the vendor script. Update the core pin,
metadata, and README to describe the resulting implementation. Rewrite stale
README sections instead of accumulating upgrade-history paragraphs.

Completion: source and metadata agree, non-overlay files match upstream, and a
dry run preserves every overlay. Explain remaining dry-run differences.

## Validate native behavior

Reconfigure and build against the new checkout. Start with Ruby, vertical
paragraphs, and affected APIs, then run the remaining native suites. Add focused
regressions for changed behavior not covered by existing tests.

For a failure, minimize it and compare equivalent builds with the old vendor/core
and new vendor/core. Keep Milestro sources, test data, working directory, and
runtime library selection constant. Distinguish a failed fixture precondition
from the behavior the assertion was meant to test. Fix migration regressions;
report unrelated baseline failures without weakening their assertions.

Completion: the build succeeds, affected regressions pass, and every failing
suite has evidence distinguishing an upgrade regression from a baseline issue.

## Validate Unity when in scope

Use the test project supplied by the user; discover its Editor version from
`ProjectSettings/ProjectVersion.txt`. Respect host execution and filesystem
permissions. Keep workstation paths in local commands and ignored artifacts.

Synchronize managed sources, tests, and the matching native library from this
revision. Preserve project-specific assets and required managed dependencies.
Verify the native library hash at the project destination before running tests.
Run EditMode and PlayMode; Metal rendering tests need a windowed Editor with a
graphics context. A batch-mode rendering failure is not a sufficient regression
signal. Capture result XML and logs, including failed case names.

For native baseline comparisons, keep managed sources and the Editor constant,
use a baseline with the same exported ABI, and restore the upgraded library even
if the comparison fails. Recheck its hash after restoration.

Completion: test counts and baseline differences are recorded, and the project
contains the upgraded matching library. State platforms or suites not exercised.

## Finish

Inspect the diff for local paths, account/device details, conflict markers,
temporary instrumentation, and generated-binding edits. Regenerate bindings via
the documented task only if the exported C API changed. Report the target Skia
revision, merged extensions, validation results, and remaining baseline failures.
Keep test artifacts outside tracked documentation.
