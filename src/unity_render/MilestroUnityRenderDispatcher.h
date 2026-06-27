#ifndef MILESTRO_UNITY_RENDER_DISPATCHER_H
#define MILESTRO_UNITY_RENDER_DISPATCHER_H

#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render {

void Load(IUnityInterfaces *unityInterfaces);
void Unload();

void *GetRenderEventFuncForExport();
int64_t GetMetalRenderEventIdForExport(int32_t &eventId);
int64_t GetRenderTextureEventIdForExport(int32_t graphicsBackend, int32_t &eventId);

} // namespace milestro::unity_render

#endif // MILESTRO_UNITY_RENDER_DISPATCHER_H
