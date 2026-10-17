#include "unity_render/MilestroUnityRenderSubmissionDraw.h"

#include "include/core/SkCanvas.h"
#include "include/core/SkSurface.h"
#include "include/core/SkPixmap.h"
#include <gtest/gtest.h>

namespace {

TEST(UnityRenderAtlas, ClearsOnlyAllocatedRegionIncludingPadding) {
    auto surface = SkSurfaces::Raster(SkImageInfo::MakeN32Premul(16, 16));
    ASSERT_NE(surface, nullptr);
    auto* canvas = surface->getCanvas();
    canvas->clear(SK_ColorRED);
    MilestroUnityDrawCommand commands[2];
    commands[0].kind = static_cast<int32_t>(MilestroUnityDrawCommandKind::BeginRegion);
    commands[0].x = 2;
    commands[0].y = 4;
    commands[0].width = 6;
    commands[0].height = 8;
    commands[0].clipX = 3;
    commands[0].clipY = 5;
    commands[0].clipWidth = 4;
    commands[0].clipHeight = 6;
    commands[0].visualOffsetX = 2;
    commands[1].kind = static_cast<int32_t>(MilestroUnityDrawCommandKind::EndRegion);
    MilestroUnityRenderSubmission submission;
    submission.target.clearBeforeDraw = 0;
    submission.commands = commands;
    submission.commandCount = 2;

    milestro::unity_render::DrawSubmission(canvas, submission);

    SkPixmap pixels;
    ASSERT_TRUE(surface->peekPixels(&pixels));
    for (int row = 0; row < 16; ++row) {
        for (int column = 0; column < 16; ++column) {
            const bool inside = column >= 2 && column < 8 && row >= 4 && row < 12;
            EXPECT_EQ(pixels.getColor(column, row), inside ? SK_ColorTRANSPARENT : SK_ColorRED);
        }
    }
    EXPECT_EQ(canvas->getSaveCount(), 1);
    EXPECT_TRUE(canvas->getTotalMatrix().isIdentity());
}

TEST(UnityRenderAtlas, IndependentRegionsRestoreClippingAndTransform) {
    auto surface = SkSurfaces::Raster(SkImageInfo::MakeN32Premul(16, 16));
    ASSERT_NE(surface, nullptr);
    auto* canvas = surface->getCanvas();
    canvas->clear(SK_ColorBLUE);
    MilestroUnityDrawCommand commands[3];
    commands[0].kind = static_cast<int32_t>(MilestroUnityDrawCommandKind::BeginRegion);
    commands[0].width = 4;
    commands[0].height = 4;
    commands[0].clipWidth = 4;
    commands[0].clipHeight = 4;
    commands[0].visualOffsetX = 2;
    commands[1] = commands[0];
    commands[1].x = 12;
    commands[1].y = 12;
    commands[1].clipX = 12;
    commands[1].clipY = 12;
    commands[1].visualOffsetX = 0.5f;
    commands[2].kind = static_cast<int32_t>(MilestroUnityDrawCommandKind::EndRegion);
    MilestroUnityRenderSubmission submission;
    submission.target.clearBeforeDraw = 0;
    submission.commands = commands;
    submission.commandCount = 3;

    milestro::unity_render::DrawSubmission(canvas, submission);

    SkPixmap pixels;
    ASSERT_TRUE(surface->peekPixels(&pixels));
    EXPECT_EQ(pixels.getColor(0, 0), SK_ColorTRANSPARENT);
    EXPECT_EQ(pixels.getColor(15, 15), SK_ColorTRANSPARENT);
    EXPECT_EQ(pixels.getColor(8, 8), SK_ColorBLUE);
    EXPECT_EQ(canvas->getSaveCount(), 1);
    EXPECT_TRUE(canvas->getTotalMatrix().isIdentity());
}

}
