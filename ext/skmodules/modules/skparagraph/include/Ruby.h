#pragma once

#include "modules/skparagraph/include/TextStyle.h"
#include <optional>
#include <string>
#include <utility>

namespace skia::textlayout {

enum class RubyOverflow { PreserveBase, Inline };

enum class RubyPosition { Above, Below };

enum class RubyAlignment { Start, Center, End, SpaceAround, SpaceBetween };

enum class RubyOverhang { None, Allow };

struct RubyStyle {
    std::optional<TextStyle> textStyle;
    SkScalar fontSizeMultiplier = 0.5f;
    SkScalar gap = 2;
    RubyPosition position = RubyPosition::Above;
    RubyAlignment alignment = RubyAlignment::Center;
    RubyOverhang overhang = RubyOverhang::None;
    RubyOverflow overflow = RubyOverflow::PreserveBase;
};

struct RubyAnnotation {
    TextRange baseRange;
    std::string text;
    RubyStyle style;

    RubyAnnotation(TextRange range, std::string annotation) : baseRange(range), text(std::move(annotation)) {
    }

    RubyAnnotation(TextRange range, std::string annotation, RubyOverflow overflow)
        : baseRange(range), text(std::move(annotation)) {
        style.overflow = overflow;
    }

    RubyAnnotation(TextRange range, std::string annotation, RubyStyle rubyStyle)
        : baseRange(range), text(std::move(annotation)), style(std::move(rubyStyle)) {
    }
};

}
