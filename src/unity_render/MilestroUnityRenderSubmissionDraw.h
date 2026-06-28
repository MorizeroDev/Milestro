#ifndef MILESTRO_UNITY_RENDER_SUBMISSION_DRAW_H
#define MILESTRO_UNITY_RENDER_SUBMISSION_DRAW_H

#include "unity_render/MilestroUnityRenderSubmission.h"

class SkCanvas;

namespace milestro::unity_render {

void DrawSubmission(SkCanvas* canvas, const MilestroUnityRenderSubmission& submission);

} // namespace milestro::unity_render

#endif // MILESTRO_UNITY_RENDER_SUBMISSION_DRAW_H
