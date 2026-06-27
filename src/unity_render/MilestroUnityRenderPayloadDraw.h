#ifndef MILESTRO_UNITY_RENDER_PAYLOAD_DRAW_H
#define MILESTRO_UNITY_RENDER_PAYLOAD_DRAW_H

#include "unity_render/MilestroUnityRenderTargetPayload.h"

class SkCanvas;

namespace milestro::unity_render {

void DrawPayload(SkCanvas *canvas, const MilestroUnityRenderTargetPayload &payload);

} // namespace milestro::unity_render

#endif // MILESTRO_UNITY_RENDER_PAYLOAD_DRAW_H
