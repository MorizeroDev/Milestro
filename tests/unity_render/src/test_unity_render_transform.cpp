#include "unity_render/MilestroUnityRenderTransform.h"

#include <gtest/gtest.h>

#include <stdexcept>
#include <string>
#include <vector>

namespace {

class RecordingCanvas {
public:
    void save() {
        calls.emplace_back("save");
    }

    void scale(float x, float y) {
        scaleX = x;
        scaleY = y;
        calls.emplace_back("scale");
    }

    void restore() {
        calls.emplace_back("restore");
    }

    std::vector<std::string> calls;
    float scaleX = 0.0f;
    float scaleY = 0.0f;
};

TEST(UnityRenderTransform, AppliesTopLevelScaleBetweenSaveAndRestore) {
    RecordingCanvas canvas;

    milestro::unity_render::MilestroUnityRenderWithEffectiveScale(&canvas, 1.5f, [&]() {
        canvas.calls.emplace_back("draw");
    });

    EXPECT_EQ(canvas.calls, (std::vector<std::string>{"save", "scale", "draw", "restore"}));
    EXPECT_FLOAT_EQ(canvas.scaleX, 1.5f);
    EXPECT_FLOAT_EQ(canvas.scaleY, 1.5f);
}

TEST(UnityRenderTransform, RestoresCanvasWhenDrawingThrows) {
    RecordingCanvas canvas;

    EXPECT_THROW(milestro::unity_render::MilestroUnityRenderWithEffectiveScale(&canvas,
                                                                               2.0f,
                                                                               [&]() {
                                                                                   canvas.calls.emplace_back("draw");
                                                                                   throw std::runtime_error(
                                                                                           "draw failed");
                                                                               }),
                 std::runtime_error);

    EXPECT_EQ(canvas.calls, (std::vector<std::string>{"save", "scale", "draw", "restore"}));
}

} // namespace
