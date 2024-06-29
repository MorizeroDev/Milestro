#ifndef MILESTRO_GAME_TYPES_H
#define MILESTRO_GAME_TYPES_H

#ifdef MILESTRO_BUILDING_ENV

#include <Milestro/common/milestro_export_macros.h>

#else

#include "milestro_export_macros.h"

#endif

namespace milestro {
    namespace skia {
        class Canvas;

        class Image;

        class Typeface;

        class Font;

        class Path;

        class VertexData;
        namespace textlayout {
            class Paragraph;

            class ParagraphBuilder;

            class ParagraphStyle;

            class TextStyle;

            class StrutStyle;
        }
    }

    namespace cdt {
        class MilestroTriangulation;
    }
}


typedef uint64_t (*MilestroSkiaTextlayoutParagraphSplitGlyphCallback)(
        const void *handler,
        uint16_t glyphId,
        milestro::skia::Font *font,

        float boundLeft, float boundTop,
        float boundRight, float boundBottom,

        float advanceWidth, float advanceHeight
);

#endif
