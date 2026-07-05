#ifndef MILESTRO_SKIA_FONT_H
#define MILESTRO_SKIA_FONT_H

#include "Milestro/log/log.h"
#include "Milestro/util/milestro_class.h"
#include "Milestro/util/milestro_serializerable.h"
#include "Path.h"
#include <include/core/SkCanvas.h>
#include <include/core/SkColor.h>
#include <include/core/SkFont.h>
#include <include/core/SkFontMetrics.h>
#include <include/core/SkPaint.h>
#include <include/core/SkPath.h>
#include <include/core/SkTypeface.h>
#include <string>
#include <utility>

namespace milestro::skia {

class MILESTRO_API Font {
public:
    explicit Font(SkFont font) : font(std::move(font)) {}

    MILESTRO_DECLARE_NON_COPYABLE(Font)

    SkFont unwrap() const {
        return font;
    }

    Path* getPath(SkGlyphID glyphID) {
        auto path = font.getPath(glyphID);
        return path ? new Path(*path) : nullptr;
    }

    SkFontMetrics getMetrics() const {
        SkFontMetrics metrics;
        font.getMetrics(&metrics);
        return metrics;
    }

    SkScalar measureText(const char* text, size_t size, SkRect* bounds) const {
        if (text == nullptr || size == 0) {
            if (bounds != nullptr) {
                bounds->setEmpty();
            }
            return 0.0f;
        }

        return font.measureText(text, size, SkTextEncoding::kUTF8, bounds);
    }

private:
    SkFont font;
};

class MILESTRO_API TextDrawSnapshot {
public:
    TextDrawSnapshot(const Font& sourceFont, std::string text, SkColor color)
        : font(sourceFont.unwrap()), text(std::move(text)) {
        paint.setColor(color);
    }

    MILESTRO_DECLARE_NON_COPYABLE(TextDrawSnapshot)

    void paintText(SkCanvas* canvas, SkScalar x, SkScalar baselineY) const {
        if (canvas == nullptr || text.empty()) {
            return;
        }

        canvas->drawString(text.c_str(), x, baselineY, font, paint);
    }

private:
    SkFont font;
    SkPaint paint;
    std::string text;
};

}

#endif //MILESTRO_SKIA_FONT_H
