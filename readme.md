# Milestro

Milestro (Milthm Maestro), a Skia integration for Unity games.

## 开发

### 环境配置

windows: 支持MSVC和Clang

windows 安卓交叉编译: 使用`unity 2022.3.8f1`自带的`NDK r23`

注意，请确保工具链路径中没有空格和中文

cmake参考：
```
-GNinja
-DCMAKE_TOOLCHAIN_FILE="C:\Unity\Editor\2022.3.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK\build\cmake\android.toolchain.cmake"
-DCMAKE_SYSTEM_NAME=Android
-DANDROID_ABI=arm64-v8a
-DCMAKE_ANDROID_NDK="C:\Unity\Editor\2022.3.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK"
-DANDROID_PLATFORM=android-24
-DCMAKE_ANDROID_NDK_TOOLCHAIN_VERSION=clang
```

## skia

安卓参考命令：
```
gn gen out/android-arm64 --args='ndk="C:\Unity\Editor\2022.3.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK" target_cpu="arm64" is_official_build=false is_debug=true skia_enable_skparagraph=true skia_enable_skshaper=true skia_enable_skunicode=true skia_use_harfbuzz=true skia_enable_fontmgr_custom_empty=true skia_use_freetype=true skia_use_system_freetype2=false'

ninja -C out/android-arm64
```
