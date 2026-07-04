#ifndef MILESTRO_SKIA_TEXTLAYOUT_NOWRAPLAYOUT_H
#define MILESTRO_SKIA_TEXTLAYOUT_NOWRAPLAYOUT_H

#include "Milestro/common/milestro_export_macros.h"

#include "include/core/SkScalar.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skparagraph/include/TextStyle.h"

#include <cstddef>
#include <string>

namespace milestro::skia::textlayout {

MILESTRO_API SkScalar ResolveNoWrapContentWidth(::skia::textlayout::Paragraph* paragraph,
                                                const char* text,
                                                size_t length);

MILESTRO_API SkScalar ResolveNoWrapContentWidth(::skia::textlayout::Paragraph* paragraph,
                                                const std::string& text);

MILESTRO_API SkScalar ResolveNoWrapProbeLayoutWidth(const std::string& text,
                                                    const ::skia::textlayout::TextStyle& textStyle,
                                                    SkScalar viewportWidth);

MILESTRO_API SkScalar ResolveNoWrapProbeLayoutWidth(size_t textByteLength,
                                                    SkScalar fontSize,
                                                    SkScalar viewportWidth);

MILESTRO_API SkScalar ResolveNoWrapLayoutWidth(SkScalar viewportWidth, SkScalar contentWidth);

} // namespace milestro::skia::textlayout

#endif // MILESTRO_SKIA_TEXTLAYOUT_NOWRAPLAYOUT_H
