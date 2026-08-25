#ifndef MILESTRO_UNITY_RENDER_TRANSFORM_H
#define MILESTRO_UNITY_RENDER_TRANSFORM_H

#include <utility>

namespace milestro::unity_render {

template <typename TCanvas>
class MilestroUnityRenderCanvasRestore final {
public:
    explicit MilestroUnityRenderCanvasRestore(TCanvas* canvas) noexcept : canvas_(canvas) {
    }

    MilestroUnityRenderCanvasRestore(const MilestroUnityRenderCanvasRestore&) = delete;
    MilestroUnityRenderCanvasRestore& operator=(const MilestroUnityRenderCanvasRestore&) = delete;

    ~MilestroUnityRenderCanvasRestore() {
        canvas_->restore();
    }

private:
    TCanvas* canvas_;
};

template <typename TCanvas, typename TDraw>
void MilestroUnityRenderWithEffectiveScale(TCanvas* canvas, float effectiveScale, TDraw&& draw) {
    canvas->save();
    const MilestroUnityRenderCanvasRestore<TCanvas> restore(canvas);
    canvas->scale(effectiveScale, effectiveScale);
    std::forward<TDraw>(draw)();
}

} // namespace milestro::unity_render

#endif // MILESTRO_UNITY_RENDER_TRANSFORM_H
