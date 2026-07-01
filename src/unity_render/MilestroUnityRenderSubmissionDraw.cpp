#include "unity_render/MilestroUnityRenderSubmissionDraw.h"

#include <Milestro/skia/Image.h>
#include <Milestro/skia/textlayout/InputBox.h>
#include <Milestro/skia/textlayout/Paragraph.h>

#include "include/core/SkCanvas.h"
#include "include/core/SkColor.h"
#include "include/core/SkImage.h"
#include "include/core/SkSamplingOptions.h"

namespace milestro::unity_render {

namespace {

void DrawImageCommand(SkCanvas* canvas, const MilestroUnityDrawCommand& command) {
    auto* milestroImage = static_cast<milestro::skia::Image*>(command.resource);
    if (milestroImage == nullptr) {
        return;
    }

    sk_sp<SkImage> image = milestroImage->unwrap();
    if (image == nullptr) {
        return;
    }

    const SkSamplingOptions sampling(SkFilterMode::kLinear);
    if (command.width > 0.0f && command.height > 0.0f) {
        SkRect dst = SkRect::MakeXYWH(command.x, command.y, command.width, command.height);
        canvas->drawImageRect(image, dst, sampling, nullptr);
        return;
    }

    canvas->drawImage(image, command.x, command.y, sampling);
}

void DrawParagraphCommand(SkCanvas* canvas, const MilestroUnityDrawCommand& command) {
    auto* paragraph = static_cast<milestro::skia::textlayout::Paragraph*>(command.resource);
    if (paragraph == nullptr) {
        return;
    }

    paragraph->paint(canvas, command.x, command.y);
}

void DrawInputBoxCommand(SkCanvas* canvas, const MilestroUnityDrawCommand& command) {
    auto* inputBox = static_cast<milestro::skia::textlayout::InputBox*>(command.resource);
    if (inputBox == nullptr) {
        return;
    }

    inputBox->paint(canvas, command.x, command.y, command.width, command.height);
}

void DrawCommand(SkCanvas* canvas, const MilestroUnityDrawCommand& command) {
    switch (static_cast<MilestroUnityDrawCommandKind>(command.kind)) {
        case MilestroUnityDrawCommandKind::Image:
            DrawImageCommand(canvas, command);
            return;
        case MilestroUnityDrawCommandKind::Paragraph:
            DrawParagraphCommand(canvas, command);
            return;
        case MilestroUnityDrawCommandKind::InputBox:
            DrawInputBoxCommand(canvas, command);
            return;
        default:
            return;
    }
}

} // namespace

void DrawSubmission(SkCanvas* canvas, const MilestroUnityRenderSubmission& submission) {
    if (submission.target.clearBeforeDraw != 0) {
        canvas->clear(SK_ColorTRANSPARENT);
    }

    if (submission.commands == nullptr || submission.commandCount <= 0) {
        return;
    }

    for (int32_t i = 0; i < submission.commandCount; ++i) {
        DrawCommand(canvas, submission.commands[i]);
    }
}

} // namespace milestro::unity_render
