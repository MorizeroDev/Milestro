#ifndef MILESTRO_GAME_INTERFACE_H
#define MILESTRO_GAME_INTERFACE_H

#include <cstdint>
#include <cstdlib>
#include <string>
#include <vector>

#ifdef MILESTRO_BUILDING_ENV

#include "milestro_game_types.h"
#include <Milestro/common/milestro_export_macros.h>

#else

#include "milestro_export_macros.h"
#include "milestro_game_types.h"

#endif

#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"
#endif

extern "C" {

// 返回值为大版本号 major
MILESTRO_API int64_t MilestroGetVersion(int32_t &major, int32_t &minor, int32_t &patch);

[[milize::CSharpType("IntPtr")]] MILESTRO_API void *MilestroUnityRenderGetRenderEventAndDataFunc();
MILESTRO_API int64_t MilestroUnityRenderGetMetalRenderEventId(int32_t &eventId);
MILESTRO_API int64_t MilestroUnityRenderGetRenderTextureEventId(int32_t graphicsBackend, int32_t &eventId);
MILESTRO_API int64_t MilestroUnityRenderCreateD3D12ExternalTexture(int32_t width,
                                                                   int32_t height,
                                                                   int32_t storageSrgb,
                                                                   int32_t preferredFormat,
                                                                   [[milize::CSharpType("IntPtr")]] void *&texture);
MILESTRO_API int64_t MilestroUnityRenderDestroyD3D12ExternalTexture(
        [[milize::RefType("ref")]] [[milize::CSharpType("IntPtr")]] void *&texture);


MILESTRO_API int64_t MilestroGameModelDataEnvelopDestroy([[milize::RefType("ref")]]
                                                     milestro::game::model::DataEnvelop *&ret);
MILESTRO_API int64_t MilestroGameModelBytesWrapperCreate(milestro::game::model::BytesWrapper *&ret,
                                                     [[milize::CSharpType("byte*")]] uint8_t *ptr,
                                                     uint64_t size);
MILESTRO_API int64_t MilestroGameModelBytesWrapperCStr(milestro::game::model::BytesWrapper* ret,
                                                    [[milize::CSharpType("IntPtr")]] uint8_t*& ptr,
                                                    uint64_t& size);
MILESTRO_API int64_t MilestroGameModelNumberWrapperCreate(milestro::game::model::NumberWrapper *&ret,
                                                      double number
);
MILESTRO_API int64_t MilestroGameModelNumberWrapperValue(milestro::game::model::NumberWrapper* ret,
                                                     double& value);

MILESTRO_API int64_t MilestroSkiaFontRegistryRegisterFontFromFile(uint8_t *path);
MILESTRO_API int64_t MilestroSkiaFontRegistryGetRegisteredFontFamilyList(milestro::skia::MilestroFontFamilyList *&ret);
MILESTRO_API int64_t MilestroSkiaFontFamilyListDestroy(
        [[milize::RefType("ref")]] milestro::skia::MilestroFontFamilyList *&ret);
MILESTRO_API int64_t MilestroSkiaFontFamilyListGetSize(milestro::skia::MilestroFontFamilyList *list,
                                                       uint64_t &size);
MILESTRO_API int64_t MilestroSkiaFontFamilyListRefElementAt(milestro::skia::MilestroFontFamilyList *list,
                                                            milestro::skia::MilestroFontFamilyInfo *&ret,
                                                            uint64_t index);
MILESTRO_API int64_t MilestroSkiaFontFamilyListGetElementAt(milestro::skia::MilestroFontFamilyList *list,
                                                            milestro::skia::MilestroFontFamilyInfo *&ret,
                                                            uint64_t index);
MILESTRO_API int64_t MilestroSkiaFontFamilyInfoDestroy(
        [[milize::RefType("ref")]] milestro::skia::MilestroFontFamilyInfo *&ret);
// Font registry string accessors intentionally return borrowed buffers instead of BytesWrapper.
// The caller must read them before the owning list/info is destroyed; this avoids an extra native copy.
MILESTRO_API int64_t MilestroSkiaFontFamilyInfoGetName(milestro::skia::MilestroFontFamilyInfo *ret,
                                                       [[milize::CSharpType("IntPtr")]] uint8_t *&ptr,
                                                       uint64_t &size);

MILESTRO_API int64_t MilestroSkiaFontRegistryGetRegisteredFontFaceList(milestro::skia::MilestroFontFaceList *&ret);
MILESTRO_API int64_t MilestroSkiaFontFaceListDestroy(
        [[milize::RefType("ref")]] milestro::skia::MilestroFontFaceList *&ret);
MILESTRO_API int64_t MilestroSkiaFontFaceListGetSize(milestro::skia::MilestroFontFaceList *list,
                                                     uint64_t &size);
MILESTRO_API int64_t MilestroSkiaFontFaceListRefElementAt(milestro::skia::MilestroFontFaceList *list,
                                                          milestro::skia::MilestroFontFaceInfo *&ret,
                                                          uint64_t index);
MILESTRO_API int64_t MilestroSkiaFontFaceListGetElementAt(milestro::skia::MilestroFontFaceList *list,
                                                          milestro::skia::MilestroFontFaceInfo *&ret,
                                                          uint64_t index);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoDestroy(
        [[milize::RefType("ref")]] milestro::skia::MilestroFontFaceInfo *&ret);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetSourcePath(milestro::skia::MilestroFontFaceInfo *ret,
                                                           [[milize::CSharpType("IntPtr")]] uint8_t *&ptr,
                                                           uint64_t &size);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetFamilyName(milestro::skia::MilestroFontFaceInfo *ret,
                                                           [[milize::CSharpType("IntPtr")]] uint8_t *&ptr,
                                                           uint64_t &size);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetFaceIndex(milestro::skia::MilestroFontFaceInfo *ret,
                                                          int32_t &value);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetInstanceIndex(milestro::skia::MilestroFontFaceInfo *ret,
                                                              int32_t &value);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetPackedIndex(milestro::skia::MilestroFontFaceInfo *ret,
                                                            int32_t &value);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetWeight(milestro::skia::MilestroFontFaceInfo *ret,
                                                       int32_t &value);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetWidth(milestro::skia::MilestroFontFaceInfo *ret,
                                                      int32_t &value);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetSlant(milestro::skia::MilestroFontFaceInfo *ret,
                                                      int32_t &value);
MILESTRO_API int64_t MilestroSkiaFontFaceInfoGetFixedPitch(milestro::skia::MilestroFontFaceInfo *ret,
                                                           int32_t &value);

MILESTRO_API int64_t MilestroSkiaTypefaceDestroy(milestro::skia::Typeface *&ret);
MILESTRO_API int64_t MilestroSkiaTypefaceGetFamilyNames(milestro::skia::Typeface *typeFace,
                                                        uint8_t *buffer,
                                                        uint64_t bufferSize,
                                                        uint64_t &needed);

MILESTRO_API int64_t MilestroSkiaFontGetPath(milestro::skia::Font *font,
                                             milestro::skia::Path *&path,
                                             uint16_t glyphId);

MILESTRO_API int64_t MilestroSkiaPathDestroy(milestro::skia::Path *&ret);
MILESTRO_API int64_t MilestroSkiaPathToAATriangles(milestro::skia::Path *p,
                                                   milestro::skia::VertexData *&vertexData, float tolerance);

MILESTRO_API int64_t MilestroSkiaSvgCreate(milestro::skia::Svg *&ret,
                                           [[milize::CSharpType("void*")]] void *data, uint64_t size);
MILESTRO_API int64_t MilestroSkiaSvgDestroy(milestro::skia::Svg *&ret);
MILESTRO_API int64_t MilestroSkiaSvgRender(milestro::skia::Svg *svg, milestro::skia::Canvas *canvas);

MILESTRO_API int64_t MilestroSkiaVertexDataDestroy(milestro::skia::VertexData *&ret);
MILESTRO_API int64_t MilestroSkiaVertexDataGetVertexCount(milestro::skia::VertexData *d,
                                                          uint64_t &numVertices);
MILESTRO_API int64_t MilestroSkiaVertexDataGetVertexSize(milestro::skia::VertexData *d,
                                                         uint64_t &vertexSize);
MILESTRO_API int64_t MilestroSkiaVertexDataFillData(milestro::skia::VertexData *d,
                                                    [[milize::CSharpType("void*")]] void *dst);

MILESTRO_API int64_t MilestroSkiaFontCollectionClearCaches();
MILESTRO_API int64_t MilestroSkiaFontCollectionIsFontFallbackEnabled(int32_t &enabled);
MILESTRO_API int64_t MilestroSkiaFontCollectionSetFontFallbackEnabled(int32_t enabled);

MILESTRO_API int64_t MilestroSkiaCanvasCreate(milestro::skia::Canvas *&ret,
                                              int32_t width,
                                              int32_t height);
MILESTRO_API int64_t MilestroSkiaCanvasCreateWithMemory(milestro::skia::Canvas *&ret,
                                                        int32_t width,
                                                        int32_t height,
                                                        [[milize::CSharpType("void*")]] void *pixels,
                                                        int64_t verticalFlip,
                                                        int64_t clearPixels);
MILESTRO_API int64_t MilestroSkiaCanvasDestroy(milestro::skia::Canvas *&ret);
MILESTRO_API int64_t MilestroSkiaCanvasGetTexture(milestro::skia::Canvas *ret,
                                                  [[milize::CSharpType("void*")]] void *targetSpace);
MILESTRO_API int64_t MilestroSkiaCanvasDrawImageSimple(milestro::skia::Canvas *ret,
                                                       milestro::skia::Image *image,
                                                       float x,
                                                       float y);
MILESTRO_API int64_t MilestroSkiaCanvasDrawImage(milestro::skia::Canvas *ret,
                                                 milestro::skia::Image *image,
                                                 float srcLeft, float srcTop, float srcRight, float srcBottom,
                                                 float dstLeft, float dstTop, float dstRight, float dstBottom);

MILESTRO_API int64_t MilestroSkiaImageCreate(milestro::skia::Image *&ret,
                                             [[milize::CSharpType("void*")]] void *data,
                                             uint64_t size);
MILESTRO_API int64_t MilestroSkiaImageSetColorType(milestro::skia::Image *img, int32_t targetColorType);
MILESTRO_API int64_t MilestroSkiaImageGetWidth(milestro::skia::Image *img, int32_t &width);
MILESTRO_API int64_t MilestroSkiaImageGetHeight(milestro::skia::Image *img, int32_t &height);
MILESTRO_API int64_t MilestroSkiaImageDestroy(milestro::skia::Image *&ret);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphDestroy(milestro::skia::textlayout::Paragraph *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphLayout(milestro::skia::textlayout::Paragraph *p, float width);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphGetHeight(milestro::skia::textlayout::Paragraph *p,
                                                              float &height);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphGetLongestLine(milestro::skia::textlayout::Paragraph *p,
                                                                   float &longestLine);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphGetMaxIntrinsicWidth(milestro::skia::textlayout::Paragraph *p,
                                                                         float &maxIntrinsicWidth);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphPaint(milestro::skia::textlayout::Paragraph *p,
                                                          milestro::skia::Canvas *canvas,
                                                          float x, float y);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphSplitGlyph(milestro::skia::textlayout::Paragraph *p,
                                                               [[milize::CSharpType("IntPtr")]] void *context,
                                                               float x, float y,
                                                               [[milize::CSharpType("MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback")]] MilestroSkiaTextlayoutParagraphSplitGlyphCallback callback);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphToSDF(milestro::skia::textlayout::Paragraph *p,
                                                          int32_t sdfWidth, int32_t sdfHeight, float sdfScale,
                                                          float x, float y,
                                                          [[milize::CSharpType("void*")]]  void *distanceField);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphToPath(milestro::skia::textlayout::Paragraph *p,
                                                           milestro::skia::Path *&path,
                                                           float x, float y);

MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxCreate(milestro::skia::textlayout::InputBox *&ret,
                                                          milestro::skia::textlayout::ParagraphStyle *paragraphStyle,
                                                          milestro::skia::textlayout::TextStyle *textStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxDestroy(
        [[milize::RefType("ref")]] milestro::skia::textlayout::InputBox *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxCreateDrawSnapshot(
        milestro::skia::textlayout::InputBox *inputBox,
        milestro::skia::textlayout::InputBoxDrawSnapshot *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxDrawSnapshotDestroy(
        [[milize::RefType("ref")]] milestro::skia::textlayout::InputBoxDrawSnapshot *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetText(milestro::skia::textlayout::InputBox *inputBox,
                                                           [[milize::CSharpType("void*")]] void *text,
                                                           uint64_t size);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetText(milestro::skia::textlayout::InputBox *inputBox,
                                                           milestro::game::model::BytesWrapper *&value);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetViewport(milestro::skia::textlayout::InputBox *inputBox,
                                                               float width,
                                                               float height);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetSoftWrap(milestro::skia::textlayout::InputBox *inputBox,
                                                               int32_t softWrap);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetSoftWrap(milestro::skia::textlayout::InputBox *inputBox,
                                                               int32_t &softWrap);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetMaskInput(milestro::skia::textlayout::InputBox *inputBox,
                                                                int32_t maskInput);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetMaskInput(milestro::skia::textlayout::InputBox *inputBox,
                                                                int32_t &maskInput);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetCaretColor(milestro::skia::textlayout::InputBox *inputBox,
                                                                 int32_t r,
                                                                 int32_t g,
                                                                 int32_t b,
                                                                 int32_t a);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetSelectionColor(milestro::skia::textlayout::InputBox *inputBox,
                                                                     int32_t r,
                                                                     int32_t g,
                                                                     int32_t b,
                                                                     int32_t a);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetCaretWidth(milestro::skia::textlayout::InputBox *inputBox,
                                                                 float width);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetCaretVisible(milestro::skia::textlayout::InputBox *inputBox,
                                                                   int32_t visible);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxInsertText(milestro::skia::textlayout::InputBox *inputBox,
                                                              [[milize::CSharpType("void*")]] void *text,
                                                              uint64_t size);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetComposition(milestro::skia::textlayout::InputBox *inputBox,
                                                                   [[milize::CSharpType("void*")]] void *text,
                                                                   uint64_t size,
                                                                   int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxCommitComposition(milestro::skia::textlayout::InputBox *inputBox,
                                                                      [[milize::CSharpType("void*")]] void *text,
                                                                      uint64_t size,
                                                                      int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxClearComposition(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxDeleteBackward(milestro::skia::textlayout::InputBox *inputBox,
                                                                  int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxDeleteForward(milestro::skia::textlayout::InputBox *inputBox,
                                                                 int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxUndo(milestro::skia::textlayout::InputBox *inputBox,
                                                        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxRedo(milestro::skia::textlayout::InputBox *inputBox,
                                                        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxBreakUndoGroup(
        milestro::skia::textlayout::InputBox *inputBox);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMovePrevious(milestro::skia::textlayout::InputBox *inputBox,
                                                                int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveNext(milestro::skia::textlayout::InputBox *inputBox,
                                                            int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMovePreviousExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveNextExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveUpExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveDownExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveLineStartExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveLineEndExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveDocumentStartExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxMoveDocumentEndExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxHitTest(milestro::skia::textlayout::InputBox *inputBox,
                                                           float x,
                                                           float y,
                                                           int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxHitTestExtendingSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        float x,
        float y,
        int32_t extendSelection,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxEnsureCaretVisible(
        milestro::skia::textlayout::InputBox *inputBox);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxScrollByX(
        milestro::skia::textlayout::InputBox *inputBox,
        float delta,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxScrollByY(
        milestro::skia::textlayout::InputBox *inputBox,
        float delta,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetCursor(milestro::skia::textlayout::InputBox *inputBox,
                                                             uint64_t &utf8Offset,
                                                             uint64_t &utf16Offset,
                                                             int32_t &affinity);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetCursorUtf8(milestro::skia::textlayout::InputBox *inputBox,
                                                                 uint64_t utf8Offset,
                                                                 int32_t affinity);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetSelection(milestro::skia::textlayout::InputBox *inputBox,
                                                                uint64_t &anchorUtf8,
                                                                uint64_t &focusUtf8,
                                                                uint64_t &startUtf8,
                                                                uint64_t &endUtf8,
                                                                int32_t &anchorAffinity,
                                                                int32_t &focusAffinity,
                                                                int32_t &hasSelection);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetSelectedText(
        milestro::skia::textlayout::InputBox *inputBox,
        milestro::game::model::BytesWrapper *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSetSelectionUtf8(
        milestro::skia::textlayout::InputBox *inputBox,
        uint64_t anchorUtf8,
        uint64_t focusUtf8,
        int32_t anchorAffinity,
        int32_t focusAffinity,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxClearSelection(
        milestro::skia::textlayout::InputBox *inputBox,
        int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSelectAll(milestro::skia::textlayout::InputBox *inputBox,
                                                             int32_t &changed);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxUtf8ToUtf16(milestro::skia::textlayout::InputBox *inputBox,
                                                               uint64_t utf8Offset,
                                                               uint64_t &utf16Offset);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxUtf16ToUtf8(milestro::skia::textlayout::InputBox *inputBox,
                                                               uint64_t utf16Offset,
                                                               uint64_t &utf8Offset);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxSnapUtf8(milestro::skia::textlayout::InputBox *inputBox,
                                                            uint64_t utf8Offset,
                                                            int32_t mode,
                                                            uint64_t &snappedUtf8Offset);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetCaretRect(milestro::skia::textlayout::InputBox *inputBox,
                                                                float &left,
                                                                float &top,
                                                                float &right,
                                                                float &bottom);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetCompositionRect(milestro::skia::textlayout::InputBox *inputBox,
                                                                      float &left,
                                                                      float &top,
                                                                      float &right,
                                                                      float &bottom);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetMetrics(milestro::skia::textlayout::InputBox *inputBox,
                                                              float &height,
                                                              float &longestLine,
                                                              float &minIntrinsicWidth,
                                                              float &maxIntrinsicWidth,
                                                              float &contentWidth,
                                                              float &scrollX,
                                                              float &scrollY,
                                                              float &viewportWidth,
                                                              float &viewportHeight);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetLineCount(milestro::skia::textlayout::InputBox *inputBox,
                                                                uint64_t &lineCount);
MILESTRO_API int64_t MilestroSkiaTextlayoutInputBoxGetLineMetrics(milestro::skia::textlayout::InputBox *inputBox,
                                                                  uint64_t lineNumber,
                                                                  uint64_t &startUtf8,
                                                                  uint64_t &endUtf8,
                                                                  float &ascent,
                                                                  float &descent,
                                                                  float &unscaledAscent,
                                                                  float &height,
                                                                  float &width,
                                                                  float &left,
                                                                  float &baseline);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderCreate(milestro::skia::textlayout::ParagraphBuilder *&ret,
                                                                  milestro::skia::textlayout::ParagraphStyle *style);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderDestroy(milestro::skia::textlayout::ParagraphBuilder *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderPushStyle(milestro::skia::textlayout::ParagraphBuilder *b,
                                                                     milestro::skia::textlayout::TextStyle *style);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderPop(milestro::skia::textlayout::ParagraphBuilder *b);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderBuild(milestro::skia::textlayout::ParagraphBuilder *b,
                                                                 milestro::skia::textlayout::Paragraph *&paragraph);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderAddText(milestro::skia::textlayout::ParagraphBuilder *b,
                                                                   uint8_t *text, size_t len);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleCreate(milestro::skia::textlayout::ParagraphStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleDestroy(milestro::skia::textlayout::ParagraphStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetStrutStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                       milestro::skia::textlayout::StrutStyle *&strutStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetStrutStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                       milestro::skia::textlayout::StrutStyle *strutStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetTextStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      milestro::skia::textlayout::TextStyle *&textStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetTextStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      milestro::skia::textlayout::TextStyle *textStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetTextDirection(milestro::skia::textlayout::ParagraphStyle *s,
                                                                          int32_t &direction);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetTextDirection(milestro::skia::textlayout::ParagraphStyle *s,
                                                                          int32_t direction);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetTextAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      int32_t &align);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetTextAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      int32_t align);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetMaxLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     uint64_t &maxLines);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetMaxLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     uint64_t maxLines);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleClearMaxLines(milestro::skia::textlayout::ParagraphStyle *s);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetEllipsis(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     uint8_t *ellipsis);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetHeight(milestro::skia::textlayout::ParagraphStyle *s,
                                                                   float &height);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetHeight(milestro::skia::textlayout::ParagraphStyle *s,
                                                                   float height);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle *s,
                                                          int32_t &behavior);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleSetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle *s,
                                                          int32_t behavior);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleIsUnlimitedLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                                          int32_t &unlimitedLines);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleIsEllipsized(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      int32_t &isEllipsized);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetEffectiveAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                      int32_t &effectiveAlign);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleIsHintingOn(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     int32_t &hintingIsOn);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleTurnHintingOff(milestro::skia::textlayout::ParagraphStyle *s);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetReplaceTabCharacters(milestro::skia::textlayout::ParagraphStyle *s,
                                                            int32_t &v);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleSetReplaceTabCharacters(milestro::skia::textlayout::ParagraphStyle *s,
                                                            int32_t v);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle *s,
                                                         int32_t &v);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleSetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle *s,
                                                         int32_t v);

MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleCreate(milestro::skia::textlayout::StrutStyle *&ret);

MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleDestroy(milestro::skia::textlayout::StrutStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetFontFamilies(milestro::skia::textlayout::StrutStyle *s,
                                                                     [[milize::CSharpType("void**")]] uint8_t **families,
                                                                     uint32_t size);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetFontStyle(milestro::skia::textlayout::StrutStyle *s,
                                                                  int32_t weight, int32_t width, int32_t slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetFontStyle(milestro::skia::textlayout::StrutStyle *s,
                                                                  int32_t &weight, int32_t &width, int32_t &slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetFontSize(milestro::skia::textlayout::StrutStyle *s,
                                                                 float size);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetFontSize(milestro::skia::textlayout::StrutStyle *s,
                                                                 float &size);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetHeight(milestro::skia::textlayout::StrutStyle *s,
                                                               float height);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetHeight(milestro::skia::textlayout::StrutStyle *s,
                                                               float &height);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                float leading);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                float &leading);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetStrutEnabled(milestro::skia::textlayout::StrutStyle *s,
                                                                     int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetStrutEnabled(milestro::skia::textlayout::StrutStyle *s,
                                                                     int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetForceStrutHeight(milestro::skia::textlayout::StrutStyle *s,
                                                                         int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetForceStrutHeight(milestro::skia::textlayout::StrutStyle *s,
                                                                         int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetHeightOverride(milestro::skia::textlayout::StrutStyle *s,
                                                                       int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetHeightOverride(milestro::skia::textlayout::StrutStyle *s,
                                                                       int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetHalfLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                    int32_t halfLeading);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetHalfLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                    int32_t &halfLeading);

MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleCreate(milestro::skia::textlayout::TextStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleDestroy(milestro::skia::textlayout::TextStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetColor(milestro::skia::textlayout::TextStyle *s,
                                                             int32_t r,
                                                             int32_t g,
                                                             int32_t b,
                                                             int32_t a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetColor(milestro::skia::textlayout::TextStyle *s,
                                                             int32_t &r,
                                                             int32_t &g,
                                                             int32_t &b,
                                                             int32_t &a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecoration(milestro::skia::textlayout::TextStyle *s,
                                                                  int32_t &decoration);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecoration(milestro::skia::textlayout::TextStyle *s,
                                                                  int32_t decoration);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecorationMode(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t &decoration);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecorationMode(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t decorationMode);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecorationColor(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t &r,
                                                                       int32_t &g,
                                                                       int32_t &b,
                                                                       int32_t &a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecorationColor(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t r,
                                                                       int32_t g,
                                                                       int32_t b,
                                                                       int32_t a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecorationStyle(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t &decorationStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecorationStyle(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t decorationStyle);
MILESTRO_API int64_t
MilestroSkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(milestro::skia::textlayout::TextStyle *s,
                                                                float &multiplier);
MILESTRO_API int64_t
MilestroSkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(milestro::skia::textlayout::TextStyle *s,
                                                                float multiplier);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetFontStyle(milestro::skia::textlayout::TextStyle *s,
                                                                 int32_t weight, int32_t width, int32_t slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetFontStyle(milestro::skia::textlayout::TextStyle *s,
                                                                 int32_t &weight, int32_t &width, int32_t &slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleAddShadow(milestro::skia::textlayout::TextStyle *s,
                                                              int32_t colorR,
                                                              int32_t colorG,
                                                              int32_t colorB,
                                                              int32_t colorA,
                                                              float offsetX,
                                                              float offsetY,
                                                              double blurSigma);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleResetShadow(milestro::skia::textlayout::TextStyle *s);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetFontFeatureNumber(milestro::skia::textlayout::TextStyle *s,
                                                                         uint64_t &num);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleAddFontFeature(milestro::skia::textlayout::TextStyle *s,
                                                                   uint8_t *fontFeature,
                                                                   int32_t value);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleResetFontFeatures(milestro::skia::textlayout::TextStyle *s);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetFontSize(milestro::skia::textlayout::TextStyle *s,
                                                                float size);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetFontSize(milestro::skia::textlayout::TextStyle *s,
                                                                float &size);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetFontFamilies(milestro::skia::textlayout::TextStyle *s,
                                                                    [[milize::CSharpType("void**")]] uint8_t **families,
                                                                    uint32_t size);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetBaselineShift(milestro::skia::textlayout::TextStyle *s,
                                                                     float baselineShift);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetBaselineShift(milestro::skia::textlayout::TextStyle *s,
                                                                     float &baselineShift);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetHeight(milestro::skia::textlayout::TextStyle *s,
                                                              float height);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetHeight(milestro::skia::textlayout::TextStyle *s,
                                                              float &height);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetHeightOverride(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetHeightOverride(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetHalfLeading(milestro::skia::textlayout::TextStyle *s,
                                                                   int32_t halfLeading);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetHalfLeading(milestro::skia::textlayout::TextStyle *s,
                                                                   int32_t &halfLeading);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetLetterSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                     float letterSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetLetterSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                     float &letterSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetWordSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                   float wordSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetWordSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                   float &wordSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetTypeface(milestro::skia::textlayout::TextStyle *s,
                                                                milestro::skia::Typeface *typeFace);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetLocale(milestro::skia::textlayout::TextStyle *s,
                                                              uint8_t *locale);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetTextBaseline(milestro::skia::textlayout::TextStyle *s,
                                                                    int32_t textBaseline);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetTextBaseline(milestro::skia::textlayout::TextStyle *s,
                                                                    int32_t &textBaseline);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetPlaceholder(milestro::skia::textlayout::TextStyle *s);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleIsPlaceholder(milestro::skia::textlayout::TextStyle *s,
                                                                  int32_t &isPlaceholder);

MILESTRO_API int64_t MilestroStringComparatorCreate(milestro::unicode::StringComparator*& ret, uint8_t* collation);
MILESTRO_API int64_t
MilestroStringComparatorDestroy([[milize::RefType("ref")]] milestro::unicode::StringComparator*& ret);
MILESTRO_API int64_t MilestroStringComparatorCompare(milestro::unicode::StringComparator* cmp,
                                                   int32_t& result,
                                                   uint8_t* a,
                                                   uint8_t* b);

MILESTRO_API int64_t MilestroCopyAndLoadICU(
        [[milize::CSharpType("byte*")]] uint8_t *ptr, uint64_t size,
        [[milize::CSharpType("byte*")]] uint8_t *dir
);
MILESTRO_API int64_t MilestroLoadICU(
        [[milize::CSharpType("byte*")]] uint8_t *ptr,
        [[milize::CSharpType("byte*")]] uint8_t *dir
);
MILESTRO_API int64_t MilestroIsICULoaded(int32_t &loaded);

MILESTRO_API int64_t MilestroUnicodeNormalizerCreate(milestro::unicode::Normalizer*& ret,
                                                   [[milize::CSharpType("byte*")]] uint8_t* name,
                                                   int32_t mode);
MILESTRO_API int64_t MilestroUnicodeNormalizerDestroy([[milize::RefType("ref")]] milestro::unicode::Normalizer*& ret);
MILESTRO_API int64_t MilestroUnicodeNormalizerNormalize(milestro::unicode::Normalizer* seg,
                                                      milestro::game::model::BytesWrapper*& ret,
                                                      [[milize::CSharpType("byte*")]] uint8_t* text);


MILESTRO_API int64_t MilestroUnicodeSegmenterCreate(milestro::unicode::Segmenter*& ret,
                                                  [[milize::CSharpType("byte*")]] uint8_t* locale,
                                                  [[milize::CSharpType("byte*")]] uint8_t* text);
MILESTRO_API int64_t MilestroUnicodeSegmenterDestroy([[milize::RefType("ref")]] milestro::unicode::Segmenter*& ret);
MILESTRO_API int64_t MilestroUnicodeSegmenterFirst(milestro::unicode::Segmenter* seg, int32_t& ret);
MILESTRO_API int64_t MilestroUnicodeSegmenterNext(milestro::unicode::Segmenter* seg, int32_t& ret);
MILESTRO_API int64_t MilestroUnicodeSegmenterCurrent(milestro::unicode::Segmenter* seg, int32_t& ret);
MILESTRO_API int64_t MilestroUnicodeSegmenterPrevious(milestro::unicode::Segmenter* seg, int32_t& ret);
MILESTRO_API int64_t MilestroUnicodeSegmenterSubString(milestro::unicode::Segmenter* seg,
                                                     milestro::game::model::BytesWrapper*& ret,
                                                     int32_t start,
                                                     int32_t len);

MILESTRO_API int64_t MilestroUnicodeTransliteratorCreate(milestro::unicode::Transliterator*& ret,
                                                       [[milize::CSharpType("byte*")]] uint8_t* id,
                                                       int32_t direction);
MILESTRO_API int64_t
MilestroUnicodeTransliteratorDestroy([[milize::RefType("ref")]] milestro::unicode::Transliterator*& ret);
MILESTRO_API int64_t MilestroUnicodeTransliteratorTransliterate(milestro::unicode::Transliterator* t,
                                                              milestro::game::model::BytesWrapper*& output,
                                                              [[milize::CSharpType("byte*")]] uint8_t* input);

MILESTRO_API int64_t MilestroUnicodeCaseMapToUpper(milestro::game::model::BytesWrapper*& ret,
                                                 [[milize::CSharpType("byte*")]] uint8_t* locale,
                                                 [[milize::CSharpType("byte*")]] uint8_t* text);
MILESTRO_API int64_t MilestroUnicodeCaseMapToLower(milestro::game::model::BytesWrapper*& ret,
                                                 [[milize::CSharpType("byte*")]] uint8_t* locale,
                                                 [[milize::CSharpType("byte*")]] uint8_t* text);

}
#ifdef __clang__
#pragma clang diagnostic pop
#endif

#endif // MILESTRO_GAME_INTERFACE_H
