# Android Unity APK smoke fixture

These files are **test-only**, not part of the distributed Unity plugin. They
build the same Demo scene three ways, with ARM64 / IL2CPP / min SDK 26 / target
SDK 35. A successful APK build is not evidence that GPU rendering works.

## Prepare an isolated project

1. Build matching native, managed and ICU artifacts from this checkout:

   ```powershell
   ./scripts/build-android-unity.ps1 `
     -UnityRoot 'D:\UnityHub\Editor\6000.3.7f1' -RunContractTests -PackageUnity
   ```

2. Clone the reference Demo into a **new** ignored directory. Use a clean Demo
   commit: this copies committed files, not uncommitted work. Skip its obsolete
   native LFS binary since the package from step 1 replaces it.

   ```powershell
   # Restore GIT_LFS_SKIP_SMUDGE afterwards if it was already set in your shell.
   $savedLfs = $env:GIT_LFS_SKIP_SMUDGE
   try {
     $env:GIT_LFS_SKIP_SMUDGE = '1'
     git clone --no-hardlinks E:/Code/MilestroDemo build/unity6000-demo-validation
     if ($LASTEXITCODE -ne 0) { throw 'Demo clone failed' }
   } finally {
     $env:GIT_LFS_SKIP_SMUDGE = $savedLfs
   }
   ```

3. **In the clone only**, move the old package roots and their root `.meta` files
   out of `Assets`, for example into `BuildSupport/original-package`. Replace
   `Milestro`, `Milestro.Editor`, `Milestro.Experimental`, `Milestro.InputSystem`
   and `Resources` with the matching roots/metas from
   `build/android-unity-27.2.12479018-arm64-v8a-api26-vulkan/unity-plugin`.
   Move the old `Milestro.Tests` out as well: this is a player smoke fixture,
   not an execution of the Demo's obsolete managed tests. Preserve the Demo's
   own `Scenes`, `Scripts`, `Editor`, `Settings` and `ProjectSettings`.
   Do not merge different binding/native revisions or retain duplicate plugins.

4. For Demo commit `1822274`, remove the obsolete
   `milestroProducer.backend = UnitySkiaGraphicsBackend.Vulkan;` assignment and
   its now-unused enum alias in `Assets/Scripts/AndroidMilestroRuntimeTest.cs`.
   The current producer selects the active Unity graphics API automatically;
   the fixture configures Direct/StagingCopy before the test creates surfaces.

5. Copy `Editor/BuildAndroidValidation.cs` to the clone's `Assets/Editor/` and
   `AndroidValidationBootstrap.cs` to its `Assets/Scripts/`. Ensure
   `Assets/Resources/Milestro/icudtl.dat.bytes` and
   `Assets/Milestro/Plugins/Android/arm64-v8a/libMilestro.so` exist; an LFS pointer
   is not a native binary.

## Build

Run from the Milestro repository. Change `VulkanDirect` to `VulkanStagingCopy` or
`GLES3` for each additional run (one editor process at a time):

```powershell
& 'D:\UnityHub\Editor\6000.3.7f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath "$PWD/build/unity6000-demo-validation" -buildTarget Android `
  -executeMethod Milestro.AndroidValidation.Editor.BuildAndroidValidation.PerformBuild `
  -milestroBackend VulkanDirect `
  -logFile "$PWD/build/unity6000-VulkanDirect.log" | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; inspect the log' }
```

The fixture temporarily selects that editor installation's SDK/NDK/JDK and
restores the previous external-tool paths in `finally`. It intentionally changes
**the clone's** player settings and plugin importer. It creates a temporary
Resources mode asset for the build and deletes it afterwards. Existing mode
assets are rejected, not overwritten. A failed/interrupted editor process may
leave the temporary asset behind; inspect it before manually clearing it.

Output: `build/unity6000-demo-validation/Builds/MilestroDemo-<mode>.apk`.
The GLES3 APK uses the dual-backend `.so`, not an API 23 GLES-only artifact.
The bootstrap logs `[AndroidValidation] mode=... api=... device=... version=...`
on a real device and rejects an unexpected graphics API.

## Device acceptance

Use the same Unity SDK's `platform-tools/adb.exe` to install one APK at a time
(the Demo application ID is shared between variants). Capture logcat, screenshots,
and device/OS/GPU/driver details. Complete the matrix in `docs/android.md`,
especially mixed Unity/Skia draws, Vulkan validation, recreation, pause/resume,
rotation, input and memory stability. Do not equate a `SUCCESS` build log with
correct pixels, a successful device load, or a performance pass.
