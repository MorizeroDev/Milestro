#include "Milestro/skia/textlayout/NoWrapLayout.h"

#include "Milestro/skia/textlayout/InputBox.h"

#include <algorithm>
#include <cmath>

namespace milestro::skia::textlayout {

namespace {

constexpr SkScalar kNoWrapProbeLayoutWidth = 1048576.0f;
constexpr SkScalar kNoWrapMaxProbeLayoutWidth = 1073741824.0f;
constexpr SkScalar kNoWrapLayoutPadding = 64.0f;

bool IsFinitePositive(SkScalar value) {
    return std::isfinite(value) && value > 0.0f;
}

std::string SafeTextCopy(const char* text, size_t length) {
    if (text == nullptr || length == 0) {
        return std::string();
    }

    return std::string(text, length);
}

} // namespace

SkScalar ResolveNoWrapContentWidth(::skia::textlayout::Paragraph* paragraph, const char* text, size_t length) {
    return ResolveNoWrapContentWidth(paragraph, SafeTextCopy(text, length));
}

SkScalar ResolveNoWrapContentWidth(::skia::textlayout::Paragraph* paragraph, const std::string& text) {
    if (paragraph == nullptr) {
        return 0.0f;
    }

    auto width =
            static_cast<double>(std::max<SkScalar>(paragraph->getLongestLine(), paragraph->getMaxIntrinsicWidth()));

    const auto lineCount = static_cast<int>(paragraph->lineNumber());
    for (int line = 0; line < lineCount; ++line) {
        ::skia::textlayout::LineMetrics lineMetrics;
        if (!paragraph->getLineMetricsAt(line, &lineMetrics)) {
            continue;
        }

        width = std::max(width, static_cast<double>(lineMetrics.fWidth));
    }

    const auto displayMap = TextBoundaryMap(text);
    const auto utf16Length = displayMap.utf16Length();
    if (utf16Length > 0) {
        auto boxes = paragraph->getRectsForRange(0,
                                                 static_cast<unsigned>(utf16Length),
                                                 ::skia::textlayout::RectHeightStyle::kTight,
                                                 ::skia::textlayout::RectWidthStyle::kTight);
        for (const auto& box: boxes) {
            width = std::max(width, static_cast<double>(box.rect.width()));
        }

        ::skia::textlayout::Paragraph::GlyphInfo glyphInfo;
        if (paragraph->getGlyphInfoAtUTF16Offset(utf16Length - 1, &glyphInfo)) {
            width = std::max(width, static_cast<double>(glyphInfo.fGraphemeLayoutBounds.width()));
        }
    }

    return std::isfinite(width) && width > 0.0 ? static_cast<float>(width) : 0.0f;
}

SkScalar ResolveNoWrapProbeLayoutWidth(const std::string& text,
                                       const ::skia::textlayout::TextStyle& textStyle,
                                       SkScalar viewportWidth) {
    return ResolveNoWrapProbeLayoutWidth(text.size(), textStyle.getFontSize(), viewportWidth);
}

SkScalar ResolveNoWrapProbeLayoutWidth(size_t textByteLength, SkScalar fontSize, SkScalar viewportWidth) {
    fontSize = IsFinitePositive(fontSize) ? fontSize : 16.0f;
    auto width = std::max<double>(kNoWrapProbeLayoutWidth, viewportWidth);
    width = std::max(width, (static_cast<double>(textByteLength) + 1.0) * fontSize * 2.0 + kNoWrapLayoutPadding);
    width = std::min<double>(width, kNoWrapMaxProbeLayoutWidth);
    return static_cast<SkScalar>(width);
}

SkScalar ResolveNoWrapLayoutWidth(SkScalar viewportWidth, SkScalar contentWidth) {
    const auto paddedContentWidth = IsFinitePositive(contentWidth) ? contentWidth + kNoWrapLayoutPadding : 0.0f;
    return std::max(viewportWidth, static_cast<SkScalar>(std::ceil(paddedContentWidth)));
}

} // namespace milestro::skia::textlayout
