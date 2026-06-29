#ifndef MILESTRO_UNITY_RENDER_GL_BACKEND_H
#define MILESTRO_UNITY_RENDER_GL_BACKEND_H

#include "unity_render/MilestroUnityRenderSubmission.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::gl {

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer);
int64_t Render(const MilestroUnityRenderSubmission& submission, UnityGfxRenderer renderer);

} // namespace milestro::unity_render::gl

#endif // MILESTRO_UNITY_RENDER_GL_BACKEND_H
