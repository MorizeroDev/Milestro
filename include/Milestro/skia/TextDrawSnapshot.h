#ifndef MILESTRO_SKIA_TEXT_DRAW_SNAPSHOT_H
#define MILESTRO_SKIA_TEXT_DRAW_SNAPSHOT_H

#include "Font.h"
#include "SlimTextDrawSnapshot.h"
#include <include/core/SkColor.h>
#include <include/core/SkFont.h>
#include <include/core/SkPaint.h>
#include <string>
#include <utility>

namespace milestro::skia {

class MILESTRO_API TextDrawSnapshot final : public SlimTextDrawSnapshot {
public:
    TextDrawSnapshot(const Font& sourceFont, std::string text, SkColor color)
        : font(sourceFont.unwrap()), text(std::move(text)) {
        paint.setColor(color);
    }

    MILESTRO_DECLARE_NON_COPYABLE(TextDrawSnapshot)

    void paintText(SkCanvas* canvas, SkScalar x, SkScalar baselineY) const override {
        if (canvas == nullptr || text.empty()) {
            return;
        }

        canvas->drawSimpleText(text.data(), text.size(), SkTextEncoding::kUTF8, x, baselineY, font, paint);
    }

    SkScalar measureText(SkRect* bounds) const override {
        if (text.empty()) {
            if (bounds != nullptr) {
                bounds->setEmpty();
            }

            return 0.0f;
        }

        return font.measureText(text.data(), text.size(), SkTextEncoding::kUTF8, bounds);
    }

    const char* textData() const override {
        return text.data();
    }

    size_t textSize() const override {
        return text.size();
    }

private:
    SkFont font;
    SkPaint paint;
    std::string text;
};

} // namespace milestro::skia

#endif // MILESTRO_SKIA_TEXT_DRAW_SNAPSHOT_H
