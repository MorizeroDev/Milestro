#ifndef MILESTRO_EXPORT_MACROS_H
#define MILESTRO_EXPORT_MACROS_H

// https://github.com/AOMediaCodec/libavif/blob/397f74c8e289386eb7d309b2f8041d8a190db29a/include/avif/avif.h#L15
// ---------------------------------------------------------------------------
// Export macros

// Here's what MILESTRO_API will be defined as in shared build:
// |       |        Windows        |                  Unix                  |
// | Build | __declspec(dllexport) | __attribute__((visibility("default"))) |
// |  Use  | __declspec(dllimport) |                                        |
//
// For static build, MILESTRO_API is always defined as nothing.

#if defined(_WIN32)
#define MILESTRO_HELPER_EXPORT __declspec(dllexport)
#define MILESTRO_HELPER_IMPORT __declspec(dllimport)
#elif defined(__GNUC__) && __GNUC__ >= 4
#define MILESTRO_HELPER_EXPORT __attribute__((visibility("default")))
#define MILESTRO_HELPER_IMPORT
#else
#define MILESTRO_HELPER_EXPORT
#define MILESTRO_HELPER_IMPORT
#endif


// 辅助说明
// dllexport和dllimport是一对，dllimport和dllexport是对动态库生效，静态库保持为空就行。
// dllimport要在调用方定义，dllexport是库构建时候定义。
// 对于如何确定接口是否需要导出，一般可以用set_target_properties+DEFINE_SYMBOL，对库的构建目标设置一个标志位，
// 以avif为例，是AVIF_DLL+AVIF_BUILDING_SHARED_LIBS，这是因为avif优先被当做静态库使用，需要AVIF_DLL来确定构建或链接的是动态库。
// AVIF_BUILDING_SHARED_LIBS是确定这个库目标当前应该是构建，还是被链接。
// 也可以使用target_compile_definitions，avif就是用这个，效果相同
#if defined(MILESTRO_DLL)
#if defined(MILESTRO_BUILDING_SHARED_LIBS)
#define MILESTRO_API MILESTRO_HELPER_EXPORT
#else
#define MILESTRO_API MILESTRO_HELPER_IMPORT
#endif // defined(MILESTRO_BUILDING_SHARED_LIBS)
#else
#define MILESTRO_API
#endif // defined(MILESTRO_DLL)

#if MILESTRO_USE_CLI
#define MILESTROCLI_API MILESTRO_API
#else
#define MILESTROCLI_API
#endif

#endif