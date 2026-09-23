#ifndef MILESTRO_SKIA_TEXTLAYOUT_RUBYSTYLE_H
#define MILESTRO_SKIA_TEXTLAYOUT_RUBYSTYLE_H

#include "TextStyle.h"
#include "modules/skparagraph/include/Ruby.h"

namespace milestro::skia::textlayout {

enum class RubyPosition { Above, Below };
enum class RubyAlignment { Start, Center, End, SpaceAround, SpaceBetween };
enum class RubyOverhang { None, Allow };
enum class RubyOverflow { PreserveBase, Inline };

struct RubyStyle {
    TextStyle* textStyle = nullptr;
    SkScalar fontSizeMultiplier = 0.5f;
    SkScalar gap = 2;
    RubyPosition position = RubyPosition::Above;
    RubyAlignment alignment = RubyAlignment::Center;
    RubyOverhang overhang = RubyOverhang::None;
    RubyOverflow overflow = RubyOverflow::PreserveBase;

    ::skia::textlayout::RubyStyle spawn() const {
        ::skia::textlayout::RubyStyle result;
        if (textStyle != nullptr)
            result.textStyle = textStyle->spawn();
        result.fontSizeMultiplier = fontSizeMultiplier;
        result.gap = gap;
        result.position = static_cast<::skia::textlayout::RubyPosition>(position);
        result.alignment = static_cast<::skia::textlayout::RubyAlignment>(alignment);
        result.overhang = static_cast<::skia::textlayout::RubyOverhang>(overhang);
        result.overflow = static_cast<::skia::textlayout::RubyOverflow>(overflow);
        return result;
    }
};

} // namespace milestro::skia::textlayout

#endif
