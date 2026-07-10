#include "Milestro/milestro_game_interface.h"

extern "C" {

#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"
#endif

int64_t FrameworkBindingMilestroGetVersion(int32_t& major, int32_t& minor, int32_t& patch) {
    return MilestroGetVersion(major, minor, patch);
}

void* FrameworkBindingMilestroUnityRenderGetRenderEventAndDataFunc() {
    return MilestroUnityRenderGetRenderEventAndDataFunc();
}

int64_t FrameworkBindingMilestroUnityRenderGetMetalRenderEventId(int32_t& eventId) {
    return MilestroUnityRenderGetMetalRenderEventId(eventId);
}

int64_t FrameworkBindingMilestroUnityRenderGetRenderTextureEventId(int32_t graphicsBackend, int32_t& eventId) {
    return MilestroUnityRenderGetRenderTextureEventId(graphicsBackend, eventId);
}

int64_t FrameworkBindingMilestroUnityRenderEnqueueSubmission(int32_t graphicsBackend,
                                                             [[milize::CSharpType("IntPtr")]] void* submission) {
    return MilestroUnityRenderEnqueueSubmission(graphicsBackend, submission);
}

int64_t FrameworkBindingMilestroUnityRenderCreateD3D12ExternalTexture(int32_t width,
                                                                      int32_t height,
                                                                      int32_t storageSrgb,
                                                                      int32_t preferredFormat,
                                                                      [[milize::CSharpType("IntPtr")]] void*& texture) {
    return MilestroUnityRenderCreateD3D12ExternalTexture(width, height, storageSrgb, preferredFormat, texture);
}

int64_t FrameworkBindingMilestroUnityRenderDestroyD3D12ExternalTexture(
        [[milize::RefType("ref")]] [[milize::CSharpType("IntPtr")]] void*& texture) {
    return MilestroUnityRenderDestroyD3D12ExternalTexture(texture);
}

int64_t FrameworkBindingMilestroGameModelDataEnvelopDestroy(
        [[milize::RefType("ref")]] milestro::game::model::DataEnvelop*& ret) {
    return MilestroGameModelDataEnvelopDestroy(ret);
}

int64_t FrameworkBindingMilestroGameModelBytesWrapperCreate(milestro::game::model::BytesWrapper*& ret,
                                                            [[milize::CSharpType("byte*")]] uint8_t* ptr,
                                                            uint64_t size) {
    return MilestroGameModelBytesWrapperCreate(ret, ptr, size);
}

int64_t FrameworkBindingMilestroGameModelBytesWrapperCStr(milestro::game::model::BytesWrapper* ret,
                                                          [[milize::CSharpType("IntPtr")]] uint8_t*& ptr,
                                                          uint64_t& size) {
    return MilestroGameModelBytesWrapperCStr(ret, ptr, size);
}

int64_t FrameworkBindingMilestroGameModelNumberWrapperCreate(milestro::game::model::NumberWrapper*& ret,
                                                             double number) {
    return MilestroGameModelNumberWrapperCreate(ret, number);
}

int64_t FrameworkBindingMilestroGameModelNumberWrapperValue(milestro::game::model::NumberWrapper* ret, double& value) {
    return MilestroGameModelNumberWrapperValue(ret, value);
}

int64_t FrameworkBindingMilestroSkiaFontRegistryRegisterFontFromFile([[milize::CSharpType("byte*")]] uint8_t* path,
                                                                     uint64_t pathSize) {
    return MilestroSkiaFontRegistryRegisterFontFromFile(path, pathSize);
}

int64_t
FrameworkBindingMilestroSkiaFontRegistryGetRegisteredFontFamilyList(milestro::skia::MilestroFontFamilyList*& ret) {
    return MilestroSkiaFontRegistryGetRegisteredFontFamilyList(ret);
}

int64_t FrameworkBindingMilestroSkiaFontRegistryResolveTypeface(milestro::skia::Font*& ret,
                                                                [[milize::CSharpType("byte*")]] uint8_t* family,
                                                                uint64_t familySize,
                                                                int32_t weight,
                                                                float size,
                                                                int32_t fallbackToSystem) {
    return MilestroSkiaFontRegistryResolveTypeface(ret, family, familySize, weight, size, fallbackToSystem);
}

int64_t FrameworkBindingMilestroSkiaFontFamilyListDestroy(
        [[milize::RefType("ref")]] milestro::skia::MilestroFontFamilyList*& ret) {
    return MilestroSkiaFontFamilyListDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaFontFamilyListGetSize(milestro::skia::MilestroFontFamilyList* list,
                                                          uint64_t& size) {
    return MilestroSkiaFontFamilyListGetSize(list, size);
}

int64_t FrameworkBindingMilestroSkiaFontFamilyListRefElementAt(milestro::skia::MilestroFontFamilyList* list,
                                                               milestro::skia::MilestroFontFamilyInfo*& ret,
                                                               uint64_t index) {
    return MilestroSkiaFontFamilyListRefElementAt(list, ret, index);
}

int64_t FrameworkBindingMilestroSkiaFontFamilyListGetElementAt(milestro::skia::MilestroFontFamilyList* list,
                                                               milestro::skia::MilestroFontFamilyInfo*& ret,
                                                               uint64_t index) {
    return MilestroSkiaFontFamilyListGetElementAt(list, ret, index);
}

int64_t FrameworkBindingMilestroSkiaFontFamilyInfoDestroy(
        [[milize::RefType("ref")]] milestro::skia::MilestroFontFamilyInfo*& ret) {
    return MilestroSkiaFontFamilyInfoDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaFontFamilyInfoGetName(milestro::skia::MilestroFontFamilyInfo* ret,
                                                          [[milize::CSharpType("IntPtr")]] uint8_t*& ptr,
                                                          uint64_t& size) {
    return MilestroSkiaFontFamilyInfoGetName(ret, ptr, size);
}

int64_t FrameworkBindingMilestroSkiaFontRegistryGetRegisteredFontFaceList(milestro::skia::MilestroFontFaceList*& ret) {
    return MilestroSkiaFontRegistryGetRegisteredFontFaceList(ret);
}

int64_t
FrameworkBindingMilestroSkiaFontFaceListDestroy([[milize::RefType("ref")]] milestro::skia::MilestroFontFaceList*& ret) {
    return MilestroSkiaFontFaceListDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaFontFaceListGetSize(milestro::skia::MilestroFontFaceList* list, uint64_t& size) {
    return MilestroSkiaFontFaceListGetSize(list, size);
}

int64_t FrameworkBindingMilestroSkiaFontFaceListRefElementAt(milestro::skia::MilestroFontFaceList* list,
                                                             milestro::skia::MilestroFontFaceInfo*& ret,
                                                             uint64_t index) {
    return MilestroSkiaFontFaceListRefElementAt(list, ret, index);
}

int64_t FrameworkBindingMilestroSkiaFontFaceListGetElementAt(milestro::skia::MilestroFontFaceList* list,
                                                             milestro::skia::MilestroFontFaceInfo*& ret,
                                                             uint64_t index) {
    return MilestroSkiaFontFaceListGetElementAt(list, ret, index);
}

int64_t
FrameworkBindingMilestroSkiaFontFaceInfoDestroy([[milize::RefType("ref")]] milestro::skia::MilestroFontFaceInfo*& ret) {
    return MilestroSkiaFontFaceInfoDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetSourcePath(milestro::skia::MilestroFontFaceInfo* ret,
                                                              [[milize::CSharpType("IntPtr")]] uint8_t*& ptr,
                                                              uint64_t& size) {
    return MilestroSkiaFontFaceInfoGetSourcePath(ret, ptr, size);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetFamilyName(milestro::skia::MilestroFontFaceInfo* ret,
                                                              [[milize::CSharpType("IntPtr")]] uint8_t*& ptr,
                                                              uint64_t& size) {
    return MilestroSkiaFontFaceInfoGetFamilyName(ret, ptr, size);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetFaceIndex(milestro::skia::MilestroFontFaceInfo* ret,
                                                             int32_t& value) {
    return MilestroSkiaFontFaceInfoGetFaceIndex(ret, value);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetInstanceIndex(milestro::skia::MilestroFontFaceInfo* ret,
                                                                 int32_t& value) {
    return MilestroSkiaFontFaceInfoGetInstanceIndex(ret, value);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetPackedIndex(milestro::skia::MilestroFontFaceInfo* ret,
                                                               int32_t& value) {
    return MilestroSkiaFontFaceInfoGetPackedIndex(ret, value);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetWeight(milestro::skia::MilestroFontFaceInfo* ret, int32_t& value) {
    return MilestroSkiaFontFaceInfoGetWeight(ret, value);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetWidth(milestro::skia::MilestroFontFaceInfo* ret, int32_t& value) {
    return MilestroSkiaFontFaceInfoGetWidth(ret, value);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetSlant(milestro::skia::MilestroFontFaceInfo* ret, int32_t& value) {
    return MilestroSkiaFontFaceInfoGetSlant(ret, value);
}

int64_t FrameworkBindingMilestroSkiaFontFaceInfoGetFixedPitch(milestro::skia::MilestroFontFaceInfo* ret,
                                                              int32_t& value) {
    return MilestroSkiaFontFaceInfoGetFixedPitch(ret, value);
}

int64_t FrameworkBindingMilestroSkiaTypefaceDestroy([[milize::RefType("ref")]] milestro::skia::Typeface*& ret) {
    return MilestroSkiaTypefaceDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTypefaceGetFamilyNameList(milestro::skia::Typeface* typeFace,
                                                              milestro::skia::MilestroTypefaceFamilyNameList*& ret) {
    return MilestroSkiaTypefaceGetFamilyNameList(typeFace, ret);
}

int64_t FrameworkBindingMilestroSkiaTypefaceFamilyNameListDestroy(
        [[milize::RefType("ref")]] milestro::skia::MilestroTypefaceFamilyNameList*& ret) {
    return MilestroSkiaTypefaceFamilyNameListDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTypefaceFamilyNameListGetSize(milestro::skia::MilestroTypefaceFamilyNameList* list,
                                                                  uint64_t& size) {
    return MilestroSkiaTypefaceFamilyNameListGetSize(list, size);
}

int64_t
FrameworkBindingMilestroSkiaTypefaceFamilyNameListRefElementAt(milestro::skia::MilestroTypefaceFamilyNameList* list,
                                                               milestro::skia::FontFamilyName*& ret,
                                                               uint64_t index) {
    return MilestroSkiaTypefaceFamilyNameListRefElementAt(list, ret, index);
}

int64_t
FrameworkBindingMilestroSkiaTypefaceFamilyNameListGetElementAt(milestro::skia::MilestroTypefaceFamilyNameList* list,
                                                               milestro::skia::FontFamilyName*& ret,
                                                               uint64_t index) {
    return MilestroSkiaTypefaceFamilyNameListGetElementAt(list, ret, index);
}

int64_t
FrameworkBindingMilestroSkiaTypefaceFamilyNameDestroy([[milize::RefType("ref")]] milestro::skia::FontFamilyName*& ret) {
    return MilestroSkiaTypefaceFamilyNameDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTypefaceFamilyNameGetName(milestro::skia::FontFamilyName* ret,
                                                              [[milize::CSharpType("IntPtr")]] uint8_t*& ptr,
                                                              uint64_t& size) {
    return MilestroSkiaTypefaceFamilyNameGetName(ret, ptr, size);
}

int64_t FrameworkBindingMilestroSkiaTypefaceFamilyNameGetLanguage(milestro::skia::FontFamilyName* ret,
                                                                  [[milize::CSharpType("IntPtr")]] uint8_t*& ptr,
                                                                  uint64_t& size) {
    return MilestroSkiaTypefaceFamilyNameGetLanguage(ret, ptr, size);
}

int64_t FrameworkBindingMilestroSkiaFontDestroy([[milize::RefType("ref")]] milestro::skia::Font*& ret) {
    return MilestroSkiaFontDestroy(ret);
}

int64_t
FrameworkBindingMilestroSkiaFontGetPath(milestro::skia::Font* font, milestro::skia::Path*& path, uint16_t glyphId) {
    return MilestroSkiaFontGetPath(font, path, glyphId);
}

int64_t
FrameworkBindingMilestroSkiaFontGetMetrics(milestro::skia::Font* font, float& ascent, float& descent, float& leading) {
    return MilestroSkiaFontGetMetrics(font, ascent, descent, leading);
}

int64_t FrameworkBindingMilestroSkiaFontMeasureText(milestro::skia::Font* font,
                                                    [[milize::CSharpType("byte*")]] const uint8_t* text,
                                                    uint64_t textSize,
                                                    float& boundsLeft,
                                                    float& boundsTop,
                                                    float& boundsRight,
                                                    float& boundsBottom,
                                                    float& advanceX) {
    return MilestroSkiaFontMeasureText(font,
                                       text,
                                       textSize,
                                       boundsLeft,
                                       boundsTop,
                                       boundsRight,
                                       boundsBottom,
                                       advanceX);
}

int64_t FrameworkBindingMilestroSkiaTextDrawSnapshotCreate(milestro::skia::SlimTextDrawSnapshot*& ret,
                                                           milestro::skia::Font* font,
                                                           [[milize::CSharpType("byte*")]] const uint8_t* text,
                                                           uint64_t textSize,
                                                           int32_t r,
                                                           int32_t g,
                                                           int32_t b,
                                                           int32_t a) {
    return MilestroSkiaTextDrawSnapshotCreate(ret, font, text, textSize, r, g, b, a);
}

int64_t FrameworkBindingMilestroSkiaTextDrawSnapshotDestroy(
        [[milize::RefType("ref")]] milestro::skia::SlimTextDrawSnapshot*& ret) {
    return MilestroSkiaTextDrawSnapshotDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaReusableTextDrawSnapshotCreate(milestro::skia::SlimTextDrawSnapshot*& ret,
                                                                   milestro::skia::Font* font,
                                                                   uint64_t capacity,
                                                                   int32_t r,
                                                                   int32_t g,
                                                                   int32_t b,
                                                                   int32_t a) {
    return MilestroSkiaReusableTextDrawSnapshotCreate(ret, font, capacity, r, g, b, a);
}

int64_t
FrameworkBindingMilestroSkiaReusableTextDrawSnapshotUpdateText(milestro::skia::SlimTextDrawSnapshot* ret,
                                                               [[milize::CSharpType("byte*")]] const uint8_t* text,
                                                               uint64_t textSize) {
    return MilestroSkiaReusableTextDrawSnapshotUpdateText(ret, text, textSize);
}

int64_t FrameworkBindingMilestroSkiaReusableTextDrawSnapshotCopyTextFrom(milestro::skia::SlimTextDrawSnapshot* ret,
                                                                         milestro::skia::SlimTextDrawSnapshot* source) {
    return MilestroSkiaReusableTextDrawSnapshotCopyTextFrom(ret, source);
}

int64_t FrameworkBindingMilestroSkiaReusableTextDrawSnapshotMeasureText(milestro::skia::SlimTextDrawSnapshot* ret,
                                                                        float& boundsLeft,
                                                                        float& boundsTop,
                                                                        float& boundsRight,
                                                                        float& boundsBottom,
                                                                        float& advanceX) {
    return MilestroSkiaReusableTextDrawSnapshotMeasureText(ret,
                                                           boundsLeft,
                                                           boundsTop,
                                                           boundsRight,
                                                           boundsBottom,
                                                           advanceX);
}

int64_t FrameworkBindingMilestroSkiaReusableTextDrawSnapshotDestroy(
        [[milize::RefType("ref")]] milestro::skia::SlimTextDrawSnapshot*& ret) {
    return MilestroSkiaReusableTextDrawSnapshotDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaPathDestroy(milestro::skia::Path*& ret) {
    return MilestroSkiaPathDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaPathToAATriangles(milestro::skia::Path* p,
                                                      milestro::skia::VertexData*& vertexData,
                                                      float tolerance) {
    return MilestroSkiaPathToAATriangles(p, vertexData, tolerance);
}

int64_t FrameworkBindingMilestroSkiaSvgCreate(milestro::skia::Svg*& ret,
                                              [[milize::CSharpType("void*")]] void* data,
                                              uint64_t size) {
    return MilestroSkiaSvgCreate(ret, data, size);
}

int64_t FrameworkBindingMilestroSkiaSvgDestroy(milestro::skia::Svg*& ret) {
    return MilestroSkiaSvgDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaSvgRender(milestro::skia::Svg* svg, milestro::skia::Canvas* canvas) {
    return MilestroSkiaSvgRender(svg, canvas);
}

int64_t FrameworkBindingMilestroSkiaVertexDataDestroy(milestro::skia::VertexData*& ret) {
    return MilestroSkiaVertexDataDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaVertexDataGetVertexCount(milestro::skia::VertexData* d, uint64_t& numVertices) {
    return MilestroSkiaVertexDataGetVertexCount(d, numVertices);
}

int64_t FrameworkBindingMilestroSkiaVertexDataGetVertexSize(milestro::skia::VertexData* d, uint64_t& vertexSize) {
    return MilestroSkiaVertexDataGetVertexSize(d, vertexSize);
}

int64_t FrameworkBindingMilestroSkiaVertexDataFillData(milestro::skia::VertexData* d,
                                                       [[milize::CSharpType("void*")]] void* dst) {
    return MilestroSkiaVertexDataFillData(d, dst);
}

int64_t FrameworkBindingMilestroSkiaFontCollectionClearCaches() {
    return MilestroSkiaFontCollectionClearCaches();
}

int64_t FrameworkBindingMilestroSkiaFontCollectionIsFontFallbackEnabled(int32_t& enabled) {
    return MilestroSkiaFontCollectionIsFontFallbackEnabled(enabled);
}

int64_t FrameworkBindingMilestroSkiaFontCollectionSetFontFallbackEnabled(int32_t enabled) {
    return MilestroSkiaFontCollectionSetFontFallbackEnabled(enabled);
}

int64_t FrameworkBindingMilestroSkiaCanvasCreate(milestro::skia::Canvas*& ret, int32_t width, int32_t height) {
    return MilestroSkiaCanvasCreate(ret, width, height);
}

int64_t FrameworkBindingMilestroSkiaCanvasCreateWithMemory(milestro::skia::Canvas*& ret,
                                                           int32_t width,
                                                           int32_t height,
                                                           [[milize::CSharpType("void*")]] void* pixels,
                                                           int64_t verticalFlip,
                                                           int64_t clearPixels) {
    return MilestroSkiaCanvasCreateWithMemory(ret, width, height, pixels, verticalFlip, clearPixels);
}

int64_t FrameworkBindingMilestroSkiaCanvasDestroy(milestro::skia::Canvas*& ret) {
    return MilestroSkiaCanvasDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaCanvasGetTexture(milestro::skia::Canvas* ret,
                                                     [[milize::CSharpType("void*")]] void* targetSpace) {
    return MilestroSkiaCanvasGetTexture(ret, targetSpace);
}

int64_t FrameworkBindingMilestroSkiaCanvasDrawImageSimple(milestro::skia::Canvas* ret,
                                                          milestro::skia::Image* image,
                                                          float x,
                                                          float y) {
    return MilestroSkiaCanvasDrawImageSimple(ret, image, x, y);
}

int64_t FrameworkBindingMilestroSkiaCanvasDrawImage(milestro::skia::Canvas* ret,
                                                    milestro::skia::Image* image,
                                                    float srcLeft,
                                                    float srcTop,
                                                    float srcRight,
                                                    float srcBottom,
                                                    float dstLeft,
                                                    float dstTop,
                                                    float dstRight,
                                                    float dstBottom) {
    return MilestroSkiaCanvasDrawImage(ret,
                                       image,
                                       srcLeft,
                                       srcTop,
                                       srcRight,
                                       srcBottom,
                                       dstLeft,
                                       dstTop,
                                       dstRight,
                                       dstBottom);
}

int64_t FrameworkBindingMilestroSkiaImageCreate(milestro::skia::Image*& ret,
                                                [[milize::CSharpType("void*")]] void* data,
                                                uint64_t size) {
    return MilestroSkiaImageCreate(ret, data, size);
}

int64_t FrameworkBindingMilestroSkiaImageSetColorType(milestro::skia::Image* img, int32_t targetColorType) {
    return MilestroSkiaImageSetColorType(img, targetColorType);
}

int64_t FrameworkBindingMilestroSkiaImageGetWidth(milestro::skia::Image* img, int32_t& width) {
    return MilestroSkiaImageGetWidth(img, width);
}

int64_t FrameworkBindingMilestroSkiaImageGetHeight(milestro::skia::Image* img, int32_t& height) {
    return MilestroSkiaImageGetHeight(img, height);
}

int64_t FrameworkBindingMilestroSkiaImageDestroy(milestro::skia::Image*& ret) {
    return MilestroSkiaImageDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphDestroy(milestro::skia::textlayout::Paragraph*& ret) {
    return MilestroSkiaTextlayoutParagraphDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphLayout(milestro::skia::textlayout::Paragraph* p, float width) {
    return MilestroSkiaTextlayoutParagraphLayout(p, width);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphGetHeight(milestro::skia::textlayout::Paragraph* p,
                                                                 float& height) {
    return MilestroSkiaTextlayoutParagraphGetHeight(p, height);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphGetLongestLine(milestro::skia::textlayout::Paragraph* p,
                                                                      float& longestLine) {
    return MilestroSkiaTextlayoutParagraphGetLongestLine(p, longestLine);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphGetMaxIntrinsicWidth(milestro::skia::textlayout::Paragraph* p,
                                                                            float& maxIntrinsicWidth) {
    return MilestroSkiaTextlayoutParagraphGetMaxIntrinsicWidth(p, maxIntrinsicWidth);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphResolveNoWrapContentWidth(milestro::skia::textlayout::Paragraph* p,
                                                                         [[milize::CSharpType("void*")]] void* text,
                                                                         uint64_t size,
                                                                         float& width) {
    return MilestroSkiaTextlayoutParagraphResolveNoWrapContentWidth(p, text, size, width);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutResolveNoWrapProbeLayoutWidth(uint64_t textSize,
                                                                            float fontSize,
                                                                            float viewportWidth,
                                                                            float& width) {
    return MilestroSkiaTextlayoutResolveNoWrapProbeLayoutWidth(textSize, fontSize, viewportWidth, width);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutResolveNoWrapLayoutWidth(float viewportWidth, float contentWidth, float& width) {
    return MilestroSkiaTextlayoutResolveNoWrapLayoutWidth(viewportWidth, contentWidth, width);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphPaint(milestro::skia::textlayout::Paragraph* p,
                                                             milestro::skia::Canvas* canvas,
                                                             float x,
                                                             float y) {
    return MilestroSkiaTextlayoutParagraphPaint(p, canvas, x, y);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphSplitGlyph(milestro::skia::textlayout::Paragraph* p,
                                                          [[milize::CSharpType("IntPtr")]] void* context,
                                                          float x,
                                                          float y,
                                                          MilestroSkiaTextlayoutParagraphSplitGlyphCallback callback) {
    return MilestroSkiaTextlayoutParagraphSplitGlyph(p, context, x, y, callback);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphToSDF(milestro::skia::textlayout::Paragraph* p,
                                                             int32_t sdfWidth,
                                                             int32_t sdfHeight,
                                                             float sdfScale,
                                                             float x,
                                                             float y,
                                                             [[milize::CSharpType("void*")]] void* distanceField) {
    return MilestroSkiaTextlayoutParagraphToSDF(p, sdfWidth, sdfHeight, sdfScale, x, y, distanceField);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphToPath(milestro::skia::textlayout::Paragraph* p,
                                                              milestro::skia::Path*& path,
                                                              float x,
                                                              float y) {
    return MilestroSkiaTextlayoutParagraphToPath(p, path, x, y);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxCreate(milestro::skia::textlayout::InputBox*& ret,
                                                             milestro::skia::textlayout::ParagraphStyle* paragraphStyle,
                                                             milestro::skia::textlayout::TextStyle* textStyle) {
    return MilestroSkiaTextlayoutInputBoxCreate(ret, paragraphStyle, textStyle);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxDestroy(
        [[milize::RefType("ref")]] milestro::skia::textlayout::InputBox*& ret) {
    return MilestroSkiaTextlayoutInputBoxDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxCreateDrawSnapshot(
        milestro::skia::textlayout::InputBox* inputBox,
        milestro::skia::textlayout::InputBoxDrawSnapshot*& ret) {
    return MilestroSkiaTextlayoutInputBoxCreateDrawSnapshot(inputBox, ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxDrawSnapshotDestroy(
        [[milize::RefType("ref")]] milestro::skia::textlayout::InputBoxDrawSnapshot*& ret) {
    return MilestroSkiaTextlayoutInputBoxDrawSnapshotDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetText(milestro::skia::textlayout::InputBox* inputBox,
                                                              [[milize::CSharpType("void*")]] void* text,
                                                              uint64_t size) {
    return MilestroSkiaTextlayoutInputBoxSetText(inputBox, text, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetText(milestro::skia::textlayout::InputBox* inputBox,
                                                              milestro::game::model::BytesWrapper*& value) {
    return MilestroSkiaTextlayoutInputBoxGetText(inputBox, value);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetViewport(milestro::skia::textlayout::InputBox* inputBox,
                                                                  float width,
                                                                  float height) {
    return MilestroSkiaTextlayoutInputBoxSetViewport(inputBox, width, height);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetSoftWrap(milestro::skia::textlayout::InputBox* inputBox,
                                                                  int32_t softWrap) {
    return MilestroSkiaTextlayoutInputBoxSetSoftWrap(inputBox, softWrap);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetSoftWrap(milestro::skia::textlayout::InputBox* inputBox,
                                                                  int32_t& softWrap) {
    return MilestroSkiaTextlayoutInputBoxGetSoftWrap(inputBox, softWrap);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetFocused(milestro::skia::textlayout::InputBox* inputBox,
                                                                 int32_t focused) {
    return MilestroSkiaTextlayoutInputBoxSetFocused(inputBox, focused);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetFocused(milestro::skia::textlayout::InputBox* inputBox,
                                                                 int32_t& focused) {
    return MilestroSkiaTextlayoutInputBoxGetFocused(inputBox, focused);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetTextOverflow(milestro::skia::textlayout::InputBox* inputBox,
                                                                      int32_t textOverflow) {
    return MilestroSkiaTextlayoutInputBoxSetTextOverflow(inputBox, textOverflow);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetTextOverflow(milestro::skia::textlayout::InputBox* inputBox,
                                                                      int32_t& textOverflow) {
    return MilestroSkiaTextlayoutInputBoxGetTextOverflow(inputBox, textOverflow);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetEllipsis(milestro::skia::textlayout::InputBox* inputBox,
                                                                  [[milize::CSharpType("void*")]] void* text,
                                                                  uint64_t size) {
    return MilestroSkiaTextlayoutInputBoxSetEllipsis(inputBox, text, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetMaskInput(milestro::skia::textlayout::InputBox* inputBox,
                                                                   int32_t maskInput) {
    return MilestroSkiaTextlayoutInputBoxSetMaskInput(inputBox, maskInput);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetMaskInput(milestro::skia::textlayout::InputBox* inputBox,
                                                                   int32_t& maskInput) {
    return MilestroSkiaTextlayoutInputBoxGetMaskInput(inputBox, maskInput);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetMaskChar(milestro::skia::textlayout::InputBox* inputBox,
                                                                  [[milize::CSharpType("void*")]] void* text,
                                                                  uint64_t size) {
    return MilestroSkiaTextlayoutInputBoxSetMaskChar(inputBox, text, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetCaretColor(milestro::skia::textlayout::InputBox* inputBox,
                                                                    int32_t r,
                                                                    int32_t g,
                                                                    int32_t b,
                                                                    int32_t a) {
    return MilestroSkiaTextlayoutInputBoxSetCaretColor(inputBox, r, g, b, a);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetSelectionColor(milestro::skia::textlayout::InputBox* inputBox,
                                                                        int32_t r,
                                                                        int32_t g,
                                                                        int32_t b,
                                                                        int32_t a) {
    return MilestroSkiaTextlayoutInputBoxSetSelectionColor(inputBox, r, g, b, a);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetCaretWidth(milestro::skia::textlayout::InputBox* inputBox,
                                                                    float width) {
    return MilestroSkiaTextlayoutInputBoxSetCaretWidth(inputBox, width);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetCaretVisible(milestro::skia::textlayout::InputBox* inputBox,
                                                                      int32_t visible) {
    return MilestroSkiaTextlayoutInputBoxSetCaretVisible(inputBox, visible);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetAutoMargin(milestro::skia::textlayout::InputBox* inputBox,
                                                                    int32_t left,
                                                                    int32_t top,
                                                                    int32_t right,
                                                                    int32_t bottom) {
    return MilestroSkiaTextlayoutInputBoxSetAutoMargin(inputBox, left, top, right, bottom);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetAutoMargin(milestro::skia::textlayout::InputBox* inputBox,
                                                                    int32_t& left,
                                                                    int32_t& top,
                                                                    int32_t& right,
                                                                    int32_t& bottom) {
    return MilestroSkiaTextlayoutInputBoxGetAutoMargin(inputBox, left, top, right, bottom);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxInsertText(milestro::skia::textlayout::InputBox* inputBox,
                                                                 [[milize::CSharpType("void*")]] void* text,
                                                                 uint64_t size) {
    return MilestroSkiaTextlayoutInputBoxInsertText(inputBox, text, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetComposition(milestro::skia::textlayout::InputBox* inputBox,
                                                                     [[milize::CSharpType("void*")]] void* text,
                                                                     uint64_t size,
                                                                     int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxSetComposition(inputBox, text, size, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxCommitComposition(milestro::skia::textlayout::InputBox* inputBox,
                                                                        [[milize::CSharpType("void*")]] void* text,
                                                                        uint64_t size,
                                                                        int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxCommitComposition(inputBox, text, size, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxClearComposition(milestro::skia::textlayout::InputBox* inputBox,
                                                                       int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxClearComposition(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxDeleteBackward(milestro::skia::textlayout::InputBox* inputBox,
                                                                     int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxDeleteBackward(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxDeleteForward(milestro::skia::textlayout::InputBox* inputBox,
                                                                    int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxDeleteForward(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxUndo(milestro::skia::textlayout::InputBox* inputBox,
                                                           int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxUndo(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxRedo(milestro::skia::textlayout::InputBox* inputBox,
                                                           int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxRedo(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxBreakUndoGroup(milestro::skia::textlayout::InputBox* inputBox) {
    return MilestroSkiaTextlayoutInputBoxBreakUndoGroup(inputBox);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxMovePrevious(milestro::skia::textlayout::InputBox* inputBox,
                                                                   int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMovePrevious(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveNext(milestro::skia::textlayout::InputBox* inputBox,
                                                               int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveNext(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxMovePreviousExtendingSelection(
        milestro::skia::textlayout::InputBox* inputBox,
        int32_t extendSelection,
        int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMovePreviousExtendingSelection(inputBox, extendSelection, changed);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveNextExtendingSelection(milestro::skia::textlayout::InputBox* inputBox,
                                                                         int32_t extendSelection,
                                                                         int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveNextExtendingSelection(inputBox, extendSelection, changed);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveUpExtendingSelection(milestro::skia::textlayout::InputBox* inputBox,
                                                                       int32_t extendSelection,
                                                                       int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveUpExtendingSelection(inputBox, extendSelection, changed);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveDownExtendingSelection(milestro::skia::textlayout::InputBox* inputBox,
                                                                         int32_t extendSelection,
                                                                         int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveDownExtendingSelection(inputBox, extendSelection, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveLineStartExtendingSelection(
        milestro::skia::textlayout::InputBox* inputBox,
        int32_t extendSelection,
        int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveLineStartExtendingSelection(inputBox, extendSelection, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveLineEndExtendingSelection(
        milestro::skia::textlayout::InputBox* inputBox,
        int32_t extendSelection,
        int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveLineEndExtendingSelection(inputBox, extendSelection, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveDocumentStartExtendingSelection(
        milestro::skia::textlayout::InputBox* inputBox,
        int32_t extendSelection,
        int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveDocumentStartExtendingSelection(inputBox, extendSelection, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxMoveDocumentEndExtendingSelection(
        milestro::skia::textlayout::InputBox* inputBox,
        int32_t extendSelection,
        int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxMoveDocumentEndExtendingSelection(inputBox, extendSelection, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxHitTest(milestro::skia::textlayout::InputBox* inputBox,
                                                              float x,
                                                              float y,
                                                              int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxHitTest(inputBox, x, y, changed);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutInputBoxHitTestExtendingSelection(milestro::skia::textlayout::InputBox* inputBox,
                                                                        float x,
                                                                        float y,
                                                                        int32_t extendSelection,
                                                                        int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxHitTestExtendingSelection(inputBox, x, y, extendSelection, changed);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutInputBoxEnsureCaretVisible(milestro::skia::textlayout::InputBox* inputBox) {
    return MilestroSkiaTextlayoutInputBoxEnsureCaretVisible(inputBox);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxScrollByX(milestro::skia::textlayout::InputBox* inputBox,
                                                                float delta,
                                                                int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxScrollByX(inputBox, delta, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxScrollByY(milestro::skia::textlayout::InputBox* inputBox,
                                                                float delta,
                                                                int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxScrollByY(inputBox, delta, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetCursor(milestro::skia::textlayout::InputBox* inputBox,
                                                                uint64_t& utf8Offset,
                                                                uint64_t& utf16Offset,
                                                                int32_t& affinity) {
    return MilestroSkiaTextlayoutInputBoxGetCursor(inputBox, utf8Offset, utf16Offset, affinity);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetCursorUtf8(milestro::skia::textlayout::InputBox* inputBox,
                                                                    uint64_t utf8Offset,
                                                                    int32_t affinity) {
    return MilestroSkiaTextlayoutInputBoxSetCursorUtf8(inputBox, utf8Offset, affinity);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetSelection(milestro::skia::textlayout::InputBox* inputBox,
                                                                   uint64_t& anchorUtf8,
                                                                   uint64_t& focusUtf8,
                                                                   uint64_t& startUtf8,
                                                                   uint64_t& endUtf8,
                                                                   int32_t& anchorAffinity,
                                                                   int32_t& focusAffinity,
                                                                   int32_t& hasSelection) {
    return MilestroSkiaTextlayoutInputBoxGetSelection(inputBox,
                                                      anchorUtf8,
                                                      focusUtf8,
                                                      startUtf8,
                                                      endUtf8,
                                                      anchorAffinity,
                                                      focusAffinity,
                                                      hasSelection);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetSelectedText(milestro::skia::textlayout::InputBox* inputBox,
                                                                      milestro::game::model::BytesWrapper*& ret) {
    return MilestroSkiaTextlayoutInputBoxGetSelectedText(inputBox, ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSetSelectionUtf8(milestro::skia::textlayout::InputBox* inputBox,
                                                                       uint64_t anchorUtf8,
                                                                       uint64_t focusUtf8,
                                                                       int32_t anchorAffinity,
                                                                       int32_t focusAffinity,
                                                                       int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxSetSelectionUtf8(inputBox,
                                                          anchorUtf8,
                                                          focusUtf8,
                                                          anchorAffinity,
                                                          focusAffinity,
                                                          changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxClearSelection(milestro::skia::textlayout::InputBox* inputBox,
                                                                     int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxClearSelection(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSelectAll(milestro::skia::textlayout::InputBox* inputBox,
                                                                int32_t& changed) {
    return MilestroSkiaTextlayoutInputBoxSelectAll(inputBox, changed);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxUtf8ToUtf16(milestro::skia::textlayout::InputBox* inputBox,
                                                                  uint64_t utf8Offset,
                                                                  uint64_t& utf16Offset) {
    return MilestroSkiaTextlayoutInputBoxUtf8ToUtf16(inputBox, utf8Offset, utf16Offset);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxUtf16ToUtf8(milestro::skia::textlayout::InputBox* inputBox,
                                                                  uint64_t utf16Offset,
                                                                  uint64_t& utf8Offset) {
    return MilestroSkiaTextlayoutInputBoxUtf16ToUtf8(inputBox, utf16Offset, utf8Offset);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxSnapUtf8(milestro::skia::textlayout::InputBox* inputBox,
                                                               uint64_t utf8Offset,
                                                               int32_t mode,
                                                               uint64_t& snappedUtf8Offset) {
    return MilestroSkiaTextlayoutInputBoxSnapUtf8(inputBox, utf8Offset, mode, snappedUtf8Offset);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetCaretRect(milestro::skia::textlayout::InputBox* inputBox,
                                                                   float& left,
                                                                   float& top,
                                                                   float& right,
                                                                   float& bottom) {
    return MilestroSkiaTextlayoutInputBoxGetCaretRect(inputBox, left, top, right, bottom);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetCompositionRect(milestro::skia::textlayout::InputBox* inputBox,
                                                                         float& left,
                                                                         float& top,
                                                                         float& right,
                                                                         float& bottom) {
    return MilestroSkiaTextlayoutInputBoxGetCompositionRect(inputBox, left, top, right, bottom);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetMetrics(milestro::skia::textlayout::InputBox* inputBox,
                                                                 float& height,
                                                                 float& longestLine,
                                                                 float& minIntrinsicWidth,
                                                                 float& maxIntrinsicWidth,
                                                                 float& contentWidth,
                                                                 float& scrollX,
                                                                 float& scrollY,
                                                                 float& viewportWidth,
                                                                 float& viewportHeight) {
    return MilestroSkiaTextlayoutInputBoxGetMetrics(inputBox,
                                                    height,
                                                    longestLine,
                                                    minIntrinsicWidth,
                                                    maxIntrinsicWidth,
                                                    contentWidth,
                                                    scrollX,
                                                    scrollY,
                                                    viewportWidth,
                                                    viewportHeight);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetLineCount(milestro::skia::textlayout::InputBox* inputBox,
                                                                   uint64_t& lineCount) {
    return MilestroSkiaTextlayoutInputBoxGetLineCount(inputBox, lineCount);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutInputBoxGetLineMetrics(milestro::skia::textlayout::InputBox* inputBox,
                                                                     uint64_t lineNumber,
                                                                     uint64_t& startUtf8,
                                                                     uint64_t& endUtf8,
                                                                     float& ascent,
                                                                     float& descent,
                                                                     float& unscaledAscent,
                                                                     float& height,
                                                                     float& width,
                                                                     float& left,
                                                                     float& baseline) {
    return MilestroSkiaTextlayoutInputBoxGetLineMetrics(inputBox,
                                                        lineNumber,
                                                        startUtf8,
                                                        endUtf8,
                                                        ascent,
                                                        descent,
                                                        unscaledAscent,
                                                        height,
                                                        width,
                                                        left,
                                                        baseline);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphBuilderCreate(milestro::skia::textlayout::ParagraphBuilder*& ret,
                                                             milestro::skia::textlayout::ParagraphStyle* style) {
    return MilestroSkiaTextlayoutParagraphBuilderCreate(ret, style);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphBuilderDestroy(milestro::skia::textlayout::ParagraphBuilder*& ret) {
    return MilestroSkiaTextlayoutParagraphBuilderDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphBuilderPushStyle(milestro::skia::textlayout::ParagraphBuilder* b,
                                                                        milestro::skia::textlayout::TextStyle* style) {
    return MilestroSkiaTextlayoutParagraphBuilderPushStyle(b, style);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphBuilderPop(milestro::skia::textlayout::ParagraphBuilder* b) {
    return MilestroSkiaTextlayoutParagraphBuilderPop(b);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphBuilderBuild(milestro::skia::textlayout::ParagraphBuilder* b,
                                                                    milestro::skia::textlayout::Paragraph*& paragraph) {
    return MilestroSkiaTextlayoutParagraphBuilderBuild(b, paragraph);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphBuilderAddText(milestro::skia::textlayout::ParagraphBuilder* b,
                                                                      uint8_t* text,
                                                                      size_t len) {
    return MilestroSkiaTextlayoutParagraphBuilderAddText(b, text, len);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleCreate(milestro::skia::textlayout::ParagraphStyle*& ret) {
    return MilestroSkiaTextlayoutParagraphStyleCreate(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleDestroy(milestro::skia::textlayout::ParagraphStyle*& ret) {
    return MilestroSkiaTextlayoutParagraphStyleDestroy(ret);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetStrutStyle(milestro::skia::textlayout::ParagraphStyle* s,
                                                                  milestro::skia::textlayout::StrutStyle*& strutStyle) {
    return MilestroSkiaTextlayoutParagraphStyleGetStrutStyle(s, strutStyle);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetStrutStyle(milestro::skia::textlayout::ParagraphStyle* s,
                                                                  milestro::skia::textlayout::StrutStyle* strutStyle) {
    return MilestroSkiaTextlayoutParagraphStyleSetStrutStyle(s, strutStyle);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetTextStyle(milestro::skia::textlayout::ParagraphStyle* s,
                                                                 milestro::skia::textlayout::TextStyle*& textStyle) {
    return MilestroSkiaTextlayoutParagraphStyleGetTextStyle(s, textStyle);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetTextStyle(milestro::skia::textlayout::ParagraphStyle* s,
                                                                 milestro::skia::textlayout::TextStyle* textStyle) {
    return MilestroSkiaTextlayoutParagraphStyleSetTextStyle(s, textStyle);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetTextDirection(milestro::skia::textlayout::ParagraphStyle* s,
                                                                     int32_t& direction) {
    return MilestroSkiaTextlayoutParagraphStyleGetTextDirection(s, direction);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetTextDirection(milestro::skia::textlayout::ParagraphStyle* s,
                                                                     int32_t direction) {
    return MilestroSkiaTextlayoutParagraphStyleSetTextDirection(s, direction);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetTextAlign(milestro::skia::textlayout::ParagraphStyle* s,
                                                                         int32_t& align) {
    return MilestroSkiaTextlayoutParagraphStyleGetTextAlign(s, align);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetTextAlign(milestro::skia::textlayout::ParagraphStyle* s,
                                                                         int32_t align) {
    return MilestroSkiaTextlayoutParagraphStyleSetTextAlign(s, align);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetMaxLines(milestro::skia::textlayout::ParagraphStyle* s,
                                                                        uint64_t& maxLines) {
    return MilestroSkiaTextlayoutParagraphStyleGetMaxLines(s, maxLines);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetMaxLines(milestro::skia::textlayout::ParagraphStyle* s,
                                                                        uint64_t maxLines) {
    return MilestroSkiaTextlayoutParagraphStyleSetMaxLines(s, maxLines);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleClearMaxLines(milestro::skia::textlayout::ParagraphStyle* s) {
    return MilestroSkiaTextlayoutParagraphStyleClearMaxLines(s);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetEllipsis(milestro::skia::textlayout::ParagraphStyle* s,
                                                                        uint8_t* ellipsis) {
    return MilestroSkiaTextlayoutParagraphStyleSetEllipsis(s, ellipsis);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetHeight(milestro::skia::textlayout::ParagraphStyle* s,
                                                                      float& height) {
    return MilestroSkiaTextlayoutParagraphStyleGetHeight(s, height);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetHeight(milestro::skia::textlayout::ParagraphStyle* s,
                                                                      float height) {
    return MilestroSkiaTextlayoutParagraphStyleSetHeight(s, height);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle* s,
                                                                          int32_t& behavior) {
    return MilestroSkiaTextlayoutParagraphStyleGetTextHeightBehavior(s, behavior);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle* s,
                                                                          int32_t behavior) {
    return MilestroSkiaTextlayoutParagraphStyleSetTextHeightBehavior(s, behavior);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleIsUnlimitedLines(milestro::skia::textlayout::ParagraphStyle* s,
                                                                     int32_t& unlimitedLines) {
    return MilestroSkiaTextlayoutParagraphStyleIsUnlimitedLines(s, unlimitedLines);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleIsEllipsized(milestro::skia::textlayout::ParagraphStyle* s,
                                                                         int32_t& isEllipsized) {
    return MilestroSkiaTextlayoutParagraphStyleIsEllipsized(s, isEllipsized);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetEffectiveAlign(milestro::skia::textlayout::ParagraphStyle* s,
                                                                      int32_t& effectiveAlign) {
    return MilestroSkiaTextlayoutParagraphStyleGetEffectiveAlign(s, effectiveAlign);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleIsHintingOn(milestro::skia::textlayout::ParagraphStyle* s,
                                                                        int32_t& hintingIsOn) {
    return MilestroSkiaTextlayoutParagraphStyleIsHintingOn(s, hintingIsOn);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleTurnHintingOff(milestro::skia::textlayout::ParagraphStyle* s) {
    return MilestroSkiaTextlayoutParagraphStyleTurnHintingOff(s);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetReplaceTabCharacters(
        milestro::skia::textlayout::ParagraphStyle* s,
        int32_t& v) {
    return MilestroSkiaTextlayoutParagraphStyleGetReplaceTabCharacters(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetReplaceTabCharacters(
        milestro::skia::textlayout::ParagraphStyle* s,
        int32_t v) {
    return MilestroSkiaTextlayoutParagraphStyleSetReplaceTabCharacters(s, v);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleGetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle* s,
                                                                         int32_t& v) {
    return MilestroSkiaTextlayoutParagraphStyleGetApplyRoundingHack(s, v);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutParagraphStyleSetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle* s,
                                                                         int32_t v) {
    return MilestroSkiaTextlayoutParagraphStyleSetApplyRoundingHack(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleCreate(milestro::skia::textlayout::StrutStyle*& ret) {
    return MilestroSkiaTextlayoutStrutStyleCreate(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleDestroy(milestro::skia::textlayout::StrutStyle*& ret) {
    return MilestroSkiaTextlayoutStrutStyleDestroy(ret);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetFontFamilies(milestro::skia::textlayout::StrutStyle* s,
                                                                [[milize::CSharpType("void**")]] uint8_t** families,
                                                                uint32_t size) {
    return MilestroSkiaTextlayoutStrutStyleSetFontFamilies(s, families, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetFontStyle(milestro::skia::textlayout::StrutStyle* s,
                                                                     int32_t weight,
                                                                     int32_t width,
                                                                     int32_t slant) {
    return MilestroSkiaTextlayoutStrutStyleSetFontStyle(s, weight, width, slant);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetFontStyle(milestro::skia::textlayout::StrutStyle* s,
                                                                     int32_t& weight,
                                                                     int32_t& width,
                                                                     int32_t& slant) {
    return MilestroSkiaTextlayoutStrutStyleGetFontStyle(s, weight, width, slant);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetFontSize(milestro::skia::textlayout::StrutStyle* s,
                                                                    float size) {
    return MilestroSkiaTextlayoutStrutStyleSetFontSize(s, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetFontSize(milestro::skia::textlayout::StrutStyle* s,
                                                                    float& size) {
    return MilestroSkiaTextlayoutStrutStyleGetFontSize(s, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetHeight(milestro::skia::textlayout::StrutStyle* s,
                                                                  float height) {
    return MilestroSkiaTextlayoutStrutStyleSetHeight(s, height);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetHeight(milestro::skia::textlayout::StrutStyle* s,
                                                                  float& height) {
    return MilestroSkiaTextlayoutStrutStyleGetHeight(s, height);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetLeading(milestro::skia::textlayout::StrutStyle* s,
                                                                   float leading) {
    return MilestroSkiaTextlayoutStrutStyleSetLeading(s, leading);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetLeading(milestro::skia::textlayout::StrutStyle* s,
                                                                   float& leading) {
    return MilestroSkiaTextlayoutStrutStyleGetLeading(s, leading);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetStrutEnabled(milestro::skia::textlayout::StrutStyle* s,
                                                                        int32_t v) {
    return MilestroSkiaTextlayoutStrutStyleSetStrutEnabled(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetStrutEnabled(milestro::skia::textlayout::StrutStyle* s,
                                                                        int32_t& v) {
    return MilestroSkiaTextlayoutStrutStyleGetStrutEnabled(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetForceStrutHeight(milestro::skia::textlayout::StrutStyle* s,
                                                                            int32_t v) {
    return MilestroSkiaTextlayoutStrutStyleSetForceStrutHeight(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetForceStrutHeight(milestro::skia::textlayout::StrutStyle* s,
                                                                            int32_t& v) {
    return MilestroSkiaTextlayoutStrutStyleGetForceStrutHeight(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetHeightOverride(milestro::skia::textlayout::StrutStyle* s,
                                                                          int32_t v) {
    return MilestroSkiaTextlayoutStrutStyleSetHeightOverride(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetHeightOverride(milestro::skia::textlayout::StrutStyle* s,
                                                                          int32_t& v) {
    return MilestroSkiaTextlayoutStrutStyleGetHeightOverride(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleSetHalfLeading(milestro::skia::textlayout::StrutStyle* s,
                                                                       int32_t halfLeading) {
    return MilestroSkiaTextlayoutStrutStyleSetHalfLeading(s, halfLeading);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutStrutStyleGetHalfLeading(milestro::skia::textlayout::StrutStyle* s,
                                                                       int32_t& halfLeading) {
    return MilestroSkiaTextlayoutStrutStyleGetHalfLeading(s, halfLeading);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleCreate(milestro::skia::textlayout::TextStyle*& ret) {
    return MilestroSkiaTextlayoutTextStyleCreate(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleDestroy(milestro::skia::textlayout::TextStyle*& ret) {
    return MilestroSkiaTextlayoutTextStyleDestroy(ret);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetColor(milestro::skia::textlayout::TextStyle* s,
                                                                int32_t r,
                                                                int32_t g,
                                                                int32_t b,
                                                                int32_t a) {
    return MilestroSkiaTextlayoutTextStyleSetColor(s, r, g, b, a);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetColor(milestro::skia::textlayout::TextStyle* s,
                                                                int32_t& r,
                                                                int32_t& g,
                                                                int32_t& b,
                                                                int32_t& a) {
    return MilestroSkiaTextlayoutTextStyleGetColor(s, r, g, b, a);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetDecoration(milestro::skia::textlayout::TextStyle* s,
                                                                     int32_t& decoration) {
    return MilestroSkiaTextlayoutTextStyleGetDecoration(s, decoration);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetDecoration(milestro::skia::textlayout::TextStyle* s,
                                                                     int32_t decoration) {
    return MilestroSkiaTextlayoutTextStyleSetDecoration(s, decoration);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetDecorationMode(milestro::skia::textlayout::TextStyle* s,
                                                                         int32_t& decoration) {
    return MilestroSkiaTextlayoutTextStyleGetDecorationMode(s, decoration);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetDecorationMode(milestro::skia::textlayout::TextStyle* s,
                                                                         int32_t decorationMode) {
    return MilestroSkiaTextlayoutTextStyleSetDecorationMode(s, decorationMode);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetDecorationColor(milestro::skia::textlayout::TextStyle* s,
                                                                          int32_t& r,
                                                                          int32_t& g,
                                                                          int32_t& b,
                                                                          int32_t& a) {
    return MilestroSkiaTextlayoutTextStyleGetDecorationColor(s, r, g, b, a);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetDecorationColor(milestro::skia::textlayout::TextStyle* s,
                                                                          int32_t r,
                                                                          int32_t g,
                                                                          int32_t b,
                                                                          int32_t a) {
    return MilestroSkiaTextlayoutTextStyleSetDecorationColor(s, r, g, b, a);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetDecorationStyle(milestro::skia::textlayout::TextStyle* s,
                                                                          int32_t& decorationStyle) {
    return MilestroSkiaTextlayoutTextStyleGetDecorationStyle(s, decorationStyle);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetDecorationStyle(milestro::skia::textlayout::TextStyle* s,
                                                                          int32_t decorationStyle) {
    return MilestroSkiaTextlayoutTextStyleSetDecorationStyle(s, decorationStyle);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(
        milestro::skia::textlayout::TextStyle* s,
        float& multiplier) {
    return MilestroSkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(s, multiplier);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(
        milestro::skia::textlayout::TextStyle* s,
        float multiplier) {
    return MilestroSkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(s, multiplier);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetFontStyle(milestro::skia::textlayout::TextStyle* s,
                                                                    int32_t weight,
                                                                    int32_t width,
                                                                    int32_t slant) {
    return MilestroSkiaTextlayoutTextStyleSetFontStyle(s, weight, width, slant);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetFontStyle(milestro::skia::textlayout::TextStyle* s,
                                                                    int32_t& weight,
                                                                    int32_t& width,
                                                                    int32_t& slant) {
    return MilestroSkiaTextlayoutTextStyleGetFontStyle(s, weight, width, slant);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleAddShadow(milestro::skia::textlayout::TextStyle* s,
                                                                 int32_t colorR,
                                                                 int32_t colorG,
                                                                 int32_t colorB,
                                                                 int32_t colorA,
                                                                 float offsetX,
                                                                 float offsetY,
                                                                 double blurSigma) {
    return MilestroSkiaTextlayoutTextStyleAddShadow(s, colorR, colorG, colorB, colorA, offsetX, offsetY, blurSigma);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleResetShadow(milestro::skia::textlayout::TextStyle* s) {
    return MilestroSkiaTextlayoutTextStyleResetShadow(s);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetFontFeatureNumber(milestro::skia::textlayout::TextStyle* s,
                                                                            uint64_t& num) {
    return MilestroSkiaTextlayoutTextStyleGetFontFeatureNumber(s, num);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleAddFontFeature(milestro::skia::textlayout::TextStyle* s,
                                                                      uint8_t* fontFeature,
                                                                      int32_t value) {
    return MilestroSkiaTextlayoutTextStyleAddFontFeature(s, fontFeature, value);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleResetFontFeatures(milestro::skia::textlayout::TextStyle* s) {
    return MilestroSkiaTextlayoutTextStyleResetFontFeatures(s);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetFontSize(milestro::skia::textlayout::TextStyle* s,
                                                                   float size) {
    return MilestroSkiaTextlayoutTextStyleSetFontSize(s, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetFontSize(milestro::skia::textlayout::TextStyle* s,
                                                                   float& size) {
    return MilestroSkiaTextlayoutTextStyleGetFontSize(s, size);
}

int64_t
FrameworkBindingMilestroSkiaTextlayoutTextStyleSetFontFamilies(milestro::skia::textlayout::TextStyle* s,
                                                               [[milize::CSharpType("void**")]] uint8_t** families,
                                                               uint32_t size) {
    return MilestroSkiaTextlayoutTextStyleSetFontFamilies(s, families, size);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetBaselineShift(milestro::skia::textlayout::TextStyle* s,
                                                                        float baselineShift) {
    return MilestroSkiaTextlayoutTextStyleSetBaselineShift(s, baselineShift);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetBaselineShift(milestro::skia::textlayout::TextStyle* s,
                                                                        float& baselineShift) {
    return MilestroSkiaTextlayoutTextStyleGetBaselineShift(s, baselineShift);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetHeight(milestro::skia::textlayout::TextStyle* s,
                                                                 float height) {
    return MilestroSkiaTextlayoutTextStyleSetHeight(s, height);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetHeight(milestro::skia::textlayout::TextStyle* s,
                                                                 float& height) {
    return MilestroSkiaTextlayoutTextStyleGetHeight(s, height);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetHeightOverride(milestro::skia::textlayout::TextStyle* s,
                                                                         int32_t v) {
    return MilestroSkiaTextlayoutTextStyleSetHeightOverride(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetHeightOverride(milestro::skia::textlayout::TextStyle* s,
                                                                         int32_t& v) {
    return MilestroSkiaTextlayoutTextStyleGetHeightOverride(s, v);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetHalfLeading(milestro::skia::textlayout::TextStyle* s,
                                                                      int32_t halfLeading) {
    return MilestroSkiaTextlayoutTextStyleSetHalfLeading(s, halfLeading);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetHalfLeading(milestro::skia::textlayout::TextStyle* s,
                                                                      int32_t& halfLeading) {
    return MilestroSkiaTextlayoutTextStyleGetHalfLeading(s, halfLeading);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetLetterSpacing(milestro::skia::textlayout::TextStyle* s,
                                                                        float letterSpacing) {
    return MilestroSkiaTextlayoutTextStyleSetLetterSpacing(s, letterSpacing);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetLetterSpacing(milestro::skia::textlayout::TextStyle* s,
                                                                        float& letterSpacing) {
    return MilestroSkiaTextlayoutTextStyleGetLetterSpacing(s, letterSpacing);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetWordSpacing(milestro::skia::textlayout::TextStyle* s,
                                                                      float wordSpacing) {
    return MilestroSkiaTextlayoutTextStyleSetWordSpacing(s, wordSpacing);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetWordSpacing(milestro::skia::textlayout::TextStyle* s,
                                                                      float& wordSpacing) {
    return MilestroSkiaTextlayoutTextStyleGetWordSpacing(s, wordSpacing);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetTypeface(milestro::skia::textlayout::TextStyle* s,
                                                                   milestro::skia::Typeface* typeFace) {
    return MilestroSkiaTextlayoutTextStyleSetTypeface(s, typeFace);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetLocale(milestro::skia::textlayout::TextStyle* s,
                                                                 uint8_t* locale) {
    return MilestroSkiaTextlayoutTextStyleSetLocale(s, locale);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetTextBaseline(milestro::skia::textlayout::TextStyle* s,
                                                                       int32_t textBaseline) {
    return MilestroSkiaTextlayoutTextStyleSetTextBaseline(s, textBaseline);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleGetTextBaseline(milestro::skia::textlayout::TextStyle* s,
                                                                       int32_t& textBaseline) {
    return MilestroSkiaTextlayoutTextStyleGetTextBaseline(s, textBaseline);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleSetPlaceholder(milestro::skia::textlayout::TextStyle* s) {
    return MilestroSkiaTextlayoutTextStyleSetPlaceholder(s);
}

int64_t FrameworkBindingMilestroSkiaTextlayoutTextStyleIsPlaceholder(milestro::skia::textlayout::TextStyle* s,
                                                                     int32_t& isPlaceholder) {
    return MilestroSkiaTextlayoutTextStyleIsPlaceholder(s, isPlaceholder);
}

int64_t FrameworkBindingMilestroStringComparatorCreate(milestro::unicode::StringComparator*& ret, uint8_t* collation) {
    return MilestroStringComparatorCreate(ret, collation);
}

int64_t
FrameworkBindingMilestroStringComparatorDestroy([[milize::RefType("ref")]] milestro::unicode::StringComparator*& ret) {
    return MilestroStringComparatorDestroy(ret);
}

int64_t FrameworkBindingMilestroStringComparatorCompare(milestro::unicode::StringComparator* cmp,
                                                        int32_t& result,
                                                        uint8_t* a,
                                                        uint8_t* b) {
    return MilestroStringComparatorCompare(cmp, result, a, b);
}

int64_t FrameworkBindingMilestroCopyAndLoadICU([[milize::CSharpType("byte*")]] uint8_t* ptr,
                                               uint64_t size,
                                               [[milize::CSharpType("byte*")]] uint8_t* dir) {
    return MilestroCopyAndLoadICU(ptr, size, dir);
}

int64_t FrameworkBindingMilestroLoadICU([[milize::CSharpType("byte*")]] uint8_t* ptr,
                                        [[milize::CSharpType("byte*")]] uint8_t* dir) {
    return MilestroLoadICU(ptr, dir);
}

int64_t FrameworkBindingMilestroIsICULoaded(int32_t& loaded) {
    return MilestroIsICULoaded(loaded);
}

int64_t FrameworkBindingMilestroUnicodeNormalizerCreate(milestro::unicode::Normalizer*& ret,
                                                        [[milize::CSharpType("byte*")]] uint8_t* name,
                                                        int32_t mode) {
    return MilestroUnicodeNormalizerCreate(ret, name, mode);
}

int64_t
FrameworkBindingMilestroUnicodeNormalizerDestroy([[milize::RefType("ref")]] milestro::unicode::Normalizer*& ret) {
    return MilestroUnicodeNormalizerDestroy(ret);
}

int64_t FrameworkBindingMilestroUnicodeNormalizerNormalize(milestro::unicode::Normalizer* seg,
                                                           milestro::game::model::BytesWrapper*& ret,
                                                           [[milize::CSharpType("byte*")]] uint8_t* text) {
    return MilestroUnicodeNormalizerNormalize(seg, ret, text);
}

int64_t FrameworkBindingMilestroUnicodeSegmenterCreate(milestro::unicode::Segmenter*& ret,
                                                       [[milize::CSharpType("byte*")]] uint8_t* locale,
                                                       [[milize::CSharpType("byte*")]] uint8_t* text) {
    return MilestroUnicodeSegmenterCreate(ret, locale, text);
}

int64_t FrameworkBindingMilestroUnicodeSegmenterDestroy([[milize::RefType("ref")]] milestro::unicode::Segmenter*& ret) {
    return MilestroUnicodeSegmenterDestroy(ret);
}

int64_t FrameworkBindingMilestroUnicodeSegmenterFirst(milestro::unicode::Segmenter* seg, int32_t& ret) {
    return MilestroUnicodeSegmenterFirst(seg, ret);
}

int64_t FrameworkBindingMilestroUnicodeSegmenterNext(milestro::unicode::Segmenter* seg, int32_t& ret) {
    return MilestroUnicodeSegmenterNext(seg, ret);
}

int64_t FrameworkBindingMilestroUnicodeSegmenterCurrent(milestro::unicode::Segmenter* seg, int32_t& ret) {
    return MilestroUnicodeSegmenterCurrent(seg, ret);
}

int64_t FrameworkBindingMilestroUnicodeSegmenterPrevious(milestro::unicode::Segmenter* seg, int32_t& ret) {
    return MilestroUnicodeSegmenterPrevious(seg, ret);
}

int64_t FrameworkBindingMilestroUnicodeSegmenterSubString(milestro::unicode::Segmenter* seg,
                                                          milestro::game::model::BytesWrapper*& ret,
                                                          int32_t start,
                                                          int32_t len) {
    return MilestroUnicodeSegmenterSubString(seg, ret, start, len);
}

int64_t FrameworkBindingMilestroUnicodeTransliteratorCreate(milestro::unicode::Transliterator*& ret,
                                                            [[milize::CSharpType("byte*")]] uint8_t* id,
                                                            int32_t direction) {
    return MilestroUnicodeTransliteratorCreate(ret, id, direction);
}

int64_t FrameworkBindingMilestroUnicodeTransliteratorDestroy(
        [[milize::RefType("ref")]] milestro::unicode::Transliterator*& ret) {
    return MilestroUnicodeTransliteratorDestroy(ret);
}

int64_t FrameworkBindingMilestroUnicodeTransliteratorTransliterate(milestro::unicode::Transliterator* t,
                                                                   milestro::game::model::BytesWrapper*& output,
                                                                   [[milize::CSharpType("byte*")]] uint8_t* input) {
    return MilestroUnicodeTransliteratorTransliterate(t, output, input);
}

int64_t FrameworkBindingMilestroUnicodeCaseMapToUpper(milestro::game::model::BytesWrapper*& ret,
                                                      [[milize::CSharpType("byte*")]] uint8_t* locale,
                                                      [[milize::CSharpType("byte*")]] uint8_t* text) {
    return MilestroUnicodeCaseMapToUpper(ret, locale, text);
}

int64_t FrameworkBindingMilestroUnicodeCaseMapToLower(milestro::game::model::BytesWrapper*& ret,
                                                      [[milize::CSharpType("byte*")]] uint8_t* locale,
                                                      [[milize::CSharpType("byte*")]] uint8_t* text) {
    return MilestroUnicodeCaseMapToLower(ret, locale, text);
}

#ifdef __clang__
#pragma clang diagnostic pop
#endif
}
