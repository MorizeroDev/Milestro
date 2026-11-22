#ifndef MILESTRO_SKIA_TEXT_DRAW_SNAPSHOT_H
#define MILESTRO_SKIA_TEXT_DRAW_SNAPSHOT_H

#include "Font.h"
#include "GlyphLayoutSnapshot.h"
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
        layout.updateSimpleText(font, this->text.data(), this->text.size(), color);
    }

    MILESTRO_DECLARE_NON_COPYABLE(TextDrawSnapshot)

    void paintText(SkCanvas* canvas, SkScalar x, SkScalar baselineY) const override {
        layout.paint(canvas, x, baselineY);
    }

    const GlyphLayoutSnapshot* glyphLayout() const override { return &layout; }

    SkScalar measureText(SkRect* bounds) const override {
        return layout.measure(bounds);
    }

    const char* textData() const override {
        return text.data();
    }

    size_t textSize() const override {
        return text.size();
    }

private:
    GlyphLayoutSnapshot layout;
    SkFont font;
    SkPaint paint;
    std::string text;
};

} // namespace milestro::skia

#endif // MILESTRO_SKIA_TEXT_DRAW_SNAPSHOT_H
