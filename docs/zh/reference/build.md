# 源码构建、测试与打包

[English](../../en/reference/build.md) | [简体中文](build.md)

只使用 Unity 插件时优先 [安装预编译包](../getting-started/install.md)。本页面向修改原生库、绑定或打包流程的贡献者。

## 构建定义

事实依据是根目录 `CMakeLists.txt`、`cmake/`、`build.gradle.kts`。
发布组装由 [workflow-formal.yml](../../../.github/workflows/workflow-formal.yml) 定义。
各平台的工具链、SDK/NDK、安装目录与符号打包参数以对应工作流为准：

| 平台 | 发布工作流 |
| --- | --- |
| Windows x86-64 | [build-windows.yml](../../../.github/workflows/build-windows.yml) |
| Linux x86-64 | [build-linux.yml](../../../.github/workflows/build-linux.yml) |
| macOS arm64 | [build-macos.yml](../../../.github/workflows/build-macos.yml) |
| iOS arm64 | [build-ios.yml](../../../.github/workflows/build-ios.yml) |
| Android arm64-v8a | [build-android.yml](../../../.github/workflows/build-android.yml) |
| Markdig netstandard2.1 | [build-markdig.yml](../../../.github/workflows/build-markdig.yml) |

## 本地构建

准备 CMake 3.26+、C++20 编译器、Ninja、Python 3、Git，以及生成绑定/打包所需的 Java 和 Gradle wrapper。
发布 CI 使用 Java 21。Apple 目标还需 Swift 5.9+。先初始化 submodule：

```sh
git submodule update --init --recursive
cmake -S . -B cmake-build-debug -G Ninja \
  -DCMAKE_BUILD_TYPE=Debug \
  -DMILESTRO_ENABLE_CLI=OFF
cmake --build cmake-build-debug --target Milestro
```

CMake 正常流程准备所需原生依赖，无需单独先构建 Skia。
Windows 使用已加载 Visual Studio x64 开发环境的终端，准备 clang-cl、lld-link、Windows SDK、CMake、Ninja；
精确编译配置见 Windows 工作流。不要设置全局 BUILD_SHARED_LIBS，项目显式控制动态/静态库。

| 目标/选项 | 作用 |
| --- | --- |
| Milestro | 原生库 |
| MilestroCli | MILESTRO_ENABLE_CLI=ON 时的命令行目标 |
| H2CS | 生成 C# 与 iOS 桥接 |
| milestro_unity_plugin | Unity 包组装 |
| MILESTRO_ENABLE_TESTS | 原生测试开关，默认 ON |

其他选项与默认值直接查阅 CMakeLists.txt。

<a id="markdig"></a>
## 构建 Markdig

Markdown 模块要求 Unity 项目使用 **.NET Standard 2.1** API Compatibility Level。
构建依赖需安装 `ext/markdig/src/global.json` 指定的 .NET SDK（10.0.100，允许 latest-minor
roll-forward）。首次克隆或更新 Markdig submodule 后运行：

```sh
./tools/build-markdig.sh
```

脚本以 `netstandard2.1` 构建 Markdig，将 `Markdig.dll` 复制到
`apps/unity-plugins/Milestro.RichText.Markdown/Plugins/`。DLL 被 Git 忽略，Unity `.meta` 文件受版本控制。
源码中的 Markdown 模块在运行此步骤之前无法在 Unity 中编译。

## 测试

```sh
cmake --build cmake-build-debug
ctest --test-dir cmake-build-debug --output-on-failure
```

Android 设备验证独立于宿主测试，见 [Android Unity 验证](../../../tests/android_unity/README.md)。
无需 Unity 的轻量平台契约检查：

```sh
cmake -S tests/platform_contracts -B build_contracts -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build build_contracts
ctest --test-dir build_contracts --output-on-failure
```

## 绑定生成

修改 `include/Milestro/game/milestro_game_interface.h` 或 H2CS 配置后运行：

```sh
./gradlew h2cs
```

也可使用 `cmake --build "<build-dir>" --target H2CS`，生成：

- `apps/unity-plugins/Milestro/Binding/BindingC.cs`
- `apps/unity-plugins/Milestro/Plugins/iOS/FrameworkBinding.cpp`

不要手改或手动格式化生成输出。`./gradlew format` 用于 Unity C# 源码格式化。
跨托管/原生边界的变更必须同时升级两侧，二者来自同一修订，不支持混用版本。

<a id="package-the-unity-plugin"></a>
## 组装 Unity 插件

```sh
./gradlew packageUnityPlugin
```

默认输出到 `build/unity-plugin/`。覆盖输出路径时，替换占位符并保留引号：

```sh
./gradlew packageUnityPlugin "-PmilestroUnityPluginOutputDir=<output-dir>"
```

任务会重建专用输出目录，按 allowlist 复制 Unity 资源根，生成 ICU bytes，并校验文件集合和 meta GUID。
它不会编译原生插件；先将对应二进制放到 `apps/unity-plugins/Milestro/Plugins/` 的匹配平台路径。
它也不会构建 Markdig；本地打包前先运行 `./tools/build-markdig.sh`，使包内包含 DLL。
分发 Markdig 时，将 `ext/markdig/license.txt` 放入包输出的 `Milestro.RichText.Markdown/license.txt`。
正式工作流收集各平台产物与 `markdig-netstandard2.1` 产物，将 `Markdig.dll` 和许可证放入
`Milestro.RichText.Markdown/`，生成 iOS 桥接并打包 `milestro-unity-plugins.tar.gz`，tag 构建发布到 Release。

## Android 与实验后端

Android 默认启用 GLES 和 Vulkan，Vulkan 要求 API 24+ 和 NDK Vulkan 库。
API 23 GLES-only 构建需显式 `-DMILESTRO_ENABLE_ANDROID_VULKAN_RENDER=OFF`。
桌面 OpenGL 和 Vulkan 分别由 MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER、
MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER 控制，默认关闭。
编译存在不等于已完成设备认证，运行边界见 [渲染](rendering.md)。
