#ifndef MILESTRO_GAME_TYPES_H
#define MILESTRO_GAME_TYPES_H

#include <stdint.h>

#ifdef MILESTRO_BUILDING_ENV

#include <Milestro/common/milestro_export_macros.h>

#else

#include "milestro_export_macros.h"

#endif

namespace milestro {

namespace game {
namespace model {
class DataEnvelop;
class NumberWrapper;
class BytesWrapper;
} // namespace model
} // namespace game

namespace skia {
class Canvas;

class Image;

class Typeface;

class Font;

class Path;

class Svg;

class VertexData;

class MilestroFontFamilyInfo;

class MilestroFontFamilyList;

class MilestroFontFaceInfo;

class MilestroFontFaceList;

namespace textlayout {
class Paragraph;

class InputBox;

class InputBoxDrawSnapshot;

class ParagraphBuilder;

class ParagraphStyle;

class TextStyle;

class StrutStyle;
} // namespace textlayout
} // namespace skia

namespace icu {
class IcuUCollator;
}

namespace cdt {
class MilestroTriangulation;
}

namespace unicode {
class StringComparator;

class Normalizer;

class Segmenter;

class Transliterator;
} // namespace unicode
} // namespace milestro


typedef uint64_t (*MilestroSkiaTextlayoutParagraphSplitGlyphCallback)(const void* handler,
                                                                      uint16_t glyphId,
                                                                      milestro::skia::Font* font,

                                                                      float boundLeft,
                                                                      float boundTop,
                                                                      float boundRight,
                                                                      float boundBottom,

                                                                      float advanceWidth,
                                                                      float advanceHeight);

#endif
