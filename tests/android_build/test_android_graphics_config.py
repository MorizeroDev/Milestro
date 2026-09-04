"""Dependency-free CMake policy tests; --ndk also checks the real NDK and ABI headers.

Run: python tests/android_build/test_android_graphics_config.py [--ndk /path/to/ndk]
No device, Skia download, or initialized submodules are needed.
"""
import argparse
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
NDK = None


class AndroidGraphicsConfigTests(unittest.TestCase):
    def configure(self, api, enabled, abi="arm64-v8a", ndk=None, missing=None, legacy=True):
        with tempfile.TemporaryDirectory(prefix="milestro-android-config-") as directory:
            root = Path(directory)
            setup = ""
            if ndk is None:
                libs = root / "libs"
                libs.mkdir()
                for name in ("GLESv3", "EGL", "log", "android", "jnigraphics", "vulkan"):
                    if name != missing:
                        (libs / ("lib" + name + ".so")).touch()
                setup = f"""
set(ANDROID TRUE)
set(CMAKE_SYSTEM_VERSION {api})
set(CMAKE_FIND_LIBRARY_PREFIXES lib)
set(CMAKE_FIND_LIBRARY_SUFFIXES .so)
set(CMAKE_LIBRARY_PATH "{libs.as_posix()}")
set(CMAKE_FIND_USE_CMAKE_SYSTEM_PATH FALSE)
set(CMAKE_FIND_USE_SYSTEM_ENVIRONMENT_PATH FALSE)
"""
            (root / "CMakeLists.txt").write_text(f"""
cmake_minimum_required(VERSION 3.26)
project(AndroidGraphicsContract LANGUAGES {"CXX" if ndk else "NONE"})
{setup}
set(MILESTRO_ENABLE_ANDROID_VULKAN_RENDER {"ON" if enabled else "OFF"})
include("{(ROOT / 'cmake/MilestroAndroidGraphics.cmake').as_posix()}")
file(WRITE "${{CMAKE_BINARY_DIR}}/backends.txt"
     "GLES=${{MILESTRO_ENABLE_UNITY_GL_RENDER}}\\nVulkan=${{MILESTRO_ENABLE_UNITY_VULKAN_RENDER}}\\n")
""", encoding="utf-8")
            args = ["cmake", "-S", str(root), "-B", str(root / "build"), "-G", "Ninja"]
            if ndk:
                args += [f"-DCMAKE_TOOLCHAIN_FILE={ndk}/build/cmake/android.toolchain.cmake",
                         f"-DANDROID_ABI={abi}", f"-DANDROID_PLATFORM=android-{api}",
                         f"-DANDROID_USE_LEGACY_TOOLCHAIN_FILE={'ON' if legacy else 'OFF'}"]
                # Compile the actual pinned native ABI assertions and GLES guard
                # against the NDK headers, without linking the full plugin.
                (root / "contract.cpp").write_text(
                    '#include "unity_render/MilestroUnityRenderSubmission.h"\n'
                    '#include <GLES3/gl3.h>\n'
                    '#include "unity_render/MilestroUnityRenderAtomic.h"\n'
                    'void atomic_contract(int32_t& word) {\n'
                    '  milestro::unity_render::AtomicStoreRelease(word, 1);\n'
                    '  int32_t expected = 1;\n'
                    '  milestro::unity_render::AtomicCompareExchangeAcquireRelease(word, expected, 2);\n'
                    '}\n'
                    '#include "unity_render/MilestroUnityRenderGLState.h"\n', encoding="utf-8")
                with (root / "CMakeLists.txt").open("a", encoding="utf-8") as cmake:
                    cmake.write(f"""
add_library(AndroidAbiContract OBJECT contract.cpp)
target_compile_features(AndroidAbiContract PRIVATE cxx_std_20)
target_compile_options(AndroidAbiContract PRIVATE -Wall -Wextra -Werror)
target_compile_definitions(AndroidAbiContract PRIVATE MILESTRO_BUILDING_ENV)
target_include_directories(AndroidAbiContract PRIVATE "{ROOT.as_posix()}/src" "{ROOT.as_posix()}/include")
""")
            result = subprocess.run(args, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            if (int(api) < 24 and enabled) or missing:
                self.assertNotEqual(result.returncode, 0, result.stdout)
                self.assertIn("ANDROID_PLATFORM=android-24" if not missing else "Could not find", result.stdout)
                return
            self.assertEqual(result.returncode, 0, result.stdout)
            self.assertEqual((root / "build/backends.txt").read_text(),
                             "GLES=ON\nVulkan=" + ("ON" if enabled else "OFF") + "\n")
            if ndk:
                result = subprocess.run(["cmake", "--build", str(root / "build")], text=True,
                                        stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                self.assertEqual(result.returncode, 0, result.stdout)

    def test_api23_requires_explicit_gles_only(self):
        self.configure(23, True)
        self.configure(23, False)

    def test_api24_can_enable_or_disable_vulkan(self):
        self.configure(24, True)
        self.configure(24, False)

    def test_requested_libraries_are_required(self):
        for library in ("GLESv3", "EGL", "log", "android", "jnigraphics", "vulkan"):
            with self.subTest(library=library):
                self.configure(24, True, missing=library)

    def test_real_ndk_and_payload_abi(self):
        if not NDK:
            self.skipTest("pass --ndk to exercise the real toolchain")
        for abi in ("arm64-v8a", "armeabi-v7a"):
            for legacy in (True, False):
                for api, enabled in ((23, True), (23, False), (24, True), (24, False), (26, True), (30, True)):
                    with self.subTest(abi=abi, api=api, vulkan=enabled, legacy=legacy):
                        self.configure(api, enabled, abi=abi, ndk=NDK, legacy=legacy)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ndk", help="Android NDK root (optional)")
    options, remaining = parser.parse_known_args()
    NDK = Path(options.ndk).resolve().as_posix() if options.ndk else None
    unittest.main(argv=[__file__] + remaining, verbosity=2)
