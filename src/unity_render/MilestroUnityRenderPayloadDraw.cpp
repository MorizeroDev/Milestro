#include "unity_render/MilestroUnityRenderPayloadDraw.h"

#include <Milestro/skia/Image.h>
#include <Milestro/skia/textlayout/Paragraph.h>

#include "include/core/SkCanvas.h"
#include "include/core/SkColor.h"
#include "include/core/SkImage.h"
#include "include/core/SkSamplingOptions.h"

namespace milestro::unity_render {

void DrawPayload(SkCanvas *canvas, const MilestroUnityRenderTargetPayload &payload) {
    if (payload.clearBeforeDraw != 0) {
        canvas->clear(SK_ColorTRANSPARENT);
    }

    if (payload.image != nullptr) {
        sk_sp<SkImage> image = payload.image->unwrap();
        if (image != nullptr) {
            const SkSamplingOptions sampling(SkFilterMode::kLinear);
            if (payload.imageWidth > 0.0f && payload.imageHeight > 0.0f) {
                SkRect dst = SkRect::MakeXYWH(payload.imageX, payload.imageY,
                                              payload.imageWidth, payload.imageHeight);
                canvas->drawImageRect(image, dst, sampling, nullptr);
            } else {
                canvas->drawImage(image, payload.imageX, payload.imageY, sampling);
            }
        }
    }

    if (payload.paragraph != nullptr) {
        payload.paragraph->paint(canvas, payload.paragraphX, payload.paragraphY);
    }
}

} // namespace milestro::unity_render
