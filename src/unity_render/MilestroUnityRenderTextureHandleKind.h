#ifndef MILESTRO_UNITY_RENDER_TEXTURE_HANDLE_KIND_H
#define MILESTRO_UNITY_RENDER_TEXTURE_HANDLE_KIND_H

#include <cstdint>

enum class MilestroUnityRenderTextureHandleKind : int32_t {
    RenderBuffer = 1,
    NativeTexture = 2,
};

#endif // MILESTRO_UNITY_RENDER_TEXTURE_HANDLE_KIND_H
