#ifndef MILESTRO_SKIA_REUSABLE_TEXT_DRAW_SNAPSHOT_H
#define MILESTRO_SKIA_REUSABLE_TEXT_DRAW_SNAPSHOT_H

#include "Font.h"
#include "SlimTextDrawSnapshot.h"
#include <include/core/SkColor.h>
#include <include/core/SkFont.h>
#include <include/core/SkPaint.h>
#include <cstring>
#include <memory>

namespace milestro::skia {

class MILESTRO_API ReusableTextDrawSnapshot final : public SlimTextDrawSnapshot {
public:
    ReusableTextDrawSnapshot(const Font& sourceFont, size_t capacity, SkColor color)
        : font(sourceFont.unwrap()), capacity(capacity),
          text(capacity > 0 ? std::make_unique<char[]>(capacity) : nullptr) {
        paint.setColor(color);
    }

    MILESTRO_DECLARE_NON_COPYABLE(ReusableTextDrawSnapshot)

    bool updateText(const char* source, size_t size) override {
        if (size > capacity || (source == nullptr && size > 0)) {
            return false;
        }

        if (size > 0) {
            std::memcpy(text.get(), source, size);
        }
        textLength = size;
        return true;
    }

    SkScalar measureText(SkRect* bounds) const override {
        if (textLength == 0 || text == nullptr) {
            if (bounds != nullptr) {
                bounds->setEmpty();
            }
            return 0.0f;
        }

        return font.measureText(text.get(), textLength, SkTextEncoding::kUTF8, bounds);
    }

    void paintText(SkCanvas* canvas, SkScalar x, SkScalar baselineY) const override {
        if (canvas == nullptr || textLength == 0 || text == nullptr) {
            return;
        }

        canvas->drawSimpleText(text.get(), textLength, SkTextEncoding::kUTF8, x, baselineY, font, paint);
    }

    const char* textData() const override {
        return text.get();
    }

    size_t textSize() const override {
        return textLength;
    }

private:
    SkFont font;
    SkPaint paint;
    size_t capacity = 0;
    std::unique_ptr<char[]> text;
    size_t textLength = 0;
};

} // namespace milestro::skia

#endif // MILESTRO_SKIA_REUSABLE_TEXT_DRAW_SNAPSHOT_H
