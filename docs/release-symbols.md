# Release Symbols

Milestro release packaging should build stripped, path-remapped runtime artifacts and separate internal symbol artifacts.

Runtime artifacts are safe to publish for game/client use. Symbol artifacts contain debug information and should be treated as internal unless Eric explicitly approves publishing them.

## Skia Inputs

Milestro does not vendor Skia. The release runner must prepare Skia before CMake configure. Provide these values through the chosen local release script or CI environment:

- `MILESTRO_SKIA_INCLUDE_PATH`
- `MILESTRO_SKIA_LIB_PATH`
- `MILESTRO_SKIA_CMAKE`

Platform-specific environments may use override variables:

- `MILESTRO_SKIA_INCLUDE_PATH_ANDROID`, `MILESTRO_SKIA_LIB_PATH_ANDROID`, `MILESTRO_SKIA_CMAKE_ANDROID`
- `MILESTRO_SKIA_INCLUDE_PATH_IOS`, `MILESTRO_SKIA_LIB_PATH_IOS`, `MILESTRO_SKIA_CMAKE_IOS`
- `MILESTRO_SKIA_INCLUDE_PATH_MACOS`, `MILESTRO_SKIA_LIB_PATH_MACOS`, `MILESTRO_SKIA_CMAKE_MACOS`
- `MILESTRO_SKIA_INCLUDE_PATH_WINDOWS`, `MILESTRO_SKIA_LIB_PATH_WINDOWS`, `MILESTRO_SKIA_CMAKE_WINDOWS`

Each platform must provide an include path plus either a library path or a CMake path. The release entrypoint should fail before CMake configure if those inputs are missing.

## Symbol Matching

`scripts/milestro_symbols.py` prepares one manifest entry per runtime binary:

- Linux/Android use ELF Build ID plus `.gnu_debuglink`.
- macOS/iOS use Mach-O UUID and dSYM UUID matching.
- Windows currently uses the MSYS2 Clang/MinGW route and validates PE/COFF debug split with LLVM `llvm-objcopy` / `llvm-readobj`. If Milestro later switches to MSVC, the release path must switch to CodeView/PDB GUID+age matching instead.

The manifest records repo, commit, tag, platform, arch, variant, runtime hash, symbol hash, and platform debug ID.
