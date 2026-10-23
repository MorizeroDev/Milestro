# Agent Instructions

## Public Repository Hygiene

Milestro is a public repository. Treat every tracked file and every file that
may be committed as publicly visible, including documentation, README content,
comments, examples, scripts, test fixtures, logs, and screenshots. This applies
especially to `docs/`.

- Write paths in repository-relative form, such as `apps/unity-plugins/` or
  `build/unity-plugin/`.
- Use portable placeholders such as `<build-dir>` or `<output-dir>` for paths
  outside the repository. Do not replace them with values copied from the
  current workstation.
- Keep current-device details out of the repository: absolute paths,
  home-directory or user names, hostnames, device names or serial numbers,
  local IP or MAC addresses, license or account identifiers, and local Unity,
  JDK, SDK, or toolchain installation paths.
- Before finishing, inspect changed files and the diff for accidental local
  environment details.

## Generated BindingC Files

Do not edit these H2CS-generated files by hand:

- `apps/unity-plugins/Milestro/Binding/BindingC.cs`
- `apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp`

Regenerate both files from the exported C API in
`include/Milestro/game/milestro_game_interface.h` by running this from the
repository root:

```sh
./gradlew h2cs
```

The configured CMake target runs the same Gradle task:

```sh
cmake --build <build-dir> --target H2CS
```

To change the generated bindings, update the source header or the H2CS
configuration in `build.gradle.kts`, then rerun `h2cs`. Do not patch, reformat,
or otherwise correct the generated outputs directly, including for
formatting-only changes.
