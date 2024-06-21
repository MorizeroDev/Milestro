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
class TypeFace;
namespace textlayout {
class Paragraph;
class ParagraphBuilder;
class ParagraphStyle;
class TextStyle;
class StrutStyle;
}
}
}

#endif
