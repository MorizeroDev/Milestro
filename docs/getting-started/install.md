# Install into a Unity project

The prebuilt Unity package already contains the managed assemblies, native
plugins, and required resource files. You do not need to build Milestro or
generate its bindings. The package targets Unity 6.

## 1. Download the package

Choose either source:

- **GitHub Releases**: download `milestro-unity-plugins.tar.gz`.
- **GitHub Actions**: open **Actions** → **Formal Build and Release** → the run
  you want → **Artifacts** → `milestro-unity-plugins`. The Actions download is a
  `.zip`; extract it once to get `milestro-unity-plugins.tar.gz`.

## 2. Extract it into `Assets/`

Extract the whole archive into the consuming Unity project's `Assets/`
directory. Keep the directory structure and the `.meta` files.

From the Unity project root, you can use:

```sh
tar -xzf milestro-unity-plugins.tar.gz -C Assets
```

This creates `Assets/unity-plugins/`. If you extract the archive with another
tool, drag everything inside it into `Assets/`; the exact nesting level does not
matter.

The archive includes `Milestro/`, `Milestro.Editor/`,
`Milestro.Experimental/`, `Milestro.InputSystem/`, and `Resources/`. Extract all
of them. `Resources/` contains the ICU data used to start the native text
pipeline. Do not rename that folder: Unity treats any directory named
`Resources` under `Assets/` as a resource root.

## 3. Add the required packages

Add these entries to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "party.para.util.colors": "https://github.com/ParaParty/ParaPartyUtil.git?path=Colors",
    "party.para.util.unitynative": "https://github.com/ParaParty/ParaPartyUtil.git?path=UnityNative"
  }
}
```

Wait for Unity's Package Manager to finish resolving them. These packages are
required by the Milestro assemblies.

## 4. Start using text

Continue with [Render your first text](first-text.md).

For editable `TextInput`, you also need `com.unity.inputsystem` in the range
`[1.16.0,2.0.0-0)` and an `InputSystemUIInputModule`. The setup is covered in
[Add your first input field](first-input.md).

The prebuilt archive includes native binaries for Windows x86-64, Linux x86-64,
macOS arm64, Android arm64-v8a, and iOS arm64. If Unity cannot load the native
library for your target, see [Troubleshooting](troubleshoot.md).
