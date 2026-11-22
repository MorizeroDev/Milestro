#ifndef MILESTRO_SKIA_REUSABLE_TEXT_DRAW_SNAPSHOT_H
#define MILESTRO_SKIA_REUSABLE_TEXT_DRAW_SNAPSHOT_H

#include "Font.h"
#include "GlyphLayoutSnapshot.h"
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
        layout.updateSimpleText(font, text.get(), textLength, paint.getColor());
        return true;
    }

    bool copyTextFrom(const SlimTextDrawSnapshot& source) override {
        if (&source == this) { return true; }
        const size_t size = source.textSize();
        if (size > capacity || (source.textData() == nullptr && size > 0)) { return false; }
        const auto* sourceLayout = source.glyphLayout();
        if (sourceLayout == nullptr || !layout.copySimpleText(*sourceLayout, font, paint.getColor())) {
            return updateText(source.textData(), size);
        }
        if (size > 0) { std::memcpy(text.get(), source.textData(), size); }
        textLength = size;
        return true;
    }

    SkScalar measureText(SkRect* bounds) const override {
        return layout.measure(bounds);
    }

    void paintText(SkCanvas* canvas, SkScalar x, SkScalar baselineY) const override {
        layout.paint(canvas, x, baselineY);
    }

    const GlyphLayoutSnapshot* glyphLayout() const override { return &layout; }

    const char* textData() const override {
        return text.get();
    }

    size_t textSize() const override {
        return textLength;
    }

private:
    GlyphLayoutSnapshot layout;
    SkFont font;
    SkPaint paint;
    size_t capacity = 0;
    std::unique_ptr<char[]> text;
    size_t textLength = 0;
};

} // namespace milestro::skia

#endif // MILESTRO_SKIA_REUSABLE_TEXT_DRAW_SNAPSHOT_H
