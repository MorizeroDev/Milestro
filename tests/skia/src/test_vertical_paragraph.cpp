#include "include/core/SkBitmap.h"
#include "include/core/SkBlurTypes.h"
#include "include/core/SkCanvas.h"
#include "include/core/SkColor.h"
#include "include/core/SkFontMgr.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkMaskFilter.h"
#include "include/core/SkPaint.h"
#include "include/core/SkPath.h"
#include "include/core/SkPathBuilder.h"
#include "include/core/SkShader.h"
#include "include/core/SkString.h"
#include "include/core/SkTextBlob.h"
#include "include/encode/SkPngEncoder.h"
#include "include/ports/SkFontMgr_empty.h"
#include "modules/skparagraph/include/FontCollection.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skparagraph/include/ParagraphBuilder.h"
#include "modules/skparagraph/include/ParagraphCache.h"
#include "modules/skparagraph/include/ParagraphPainter.h"
#include "modules/skparagraph/include/ParagraphStyle.h"
#include "modules/skparagraph/include/TextStyle.h"
#include "modules/skparagraph/include/TypefaceFontProvider.h"
#include "modules/skparagraph/src/ParagraphImpl.h"
#include "modules/skunicode/include/SkUnicode.h"
#include "modules/skunicode/include/SkUnicode_icu.h"
#include <gtest/gtest.h>
#include <unicode/uchar.h>
#include <unicode/udata.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <fstream>
#include <functional>
#include <iostream>
#include <limits>
#include <memory>
#include <numeric>
#include <optional>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace skia_text = skia::textlayout;

namespace {

constexpr char kPrimaryFamily[] = "Source Han Sans VF";
constexpr char kLatinFamily[] = "Liberation Sans";
constexpr char kEmojiFamily[] = "Noto Color Emoji";

void LoadIcuData() {
    static bool loaded = false;
    static std::vector<uint8_t> data;
    if (loaded) {
        return;
    }
    std::ifstream input(MILESTRO_TEST_ICUDTL_PATH, std::ios::binary);
    ASSERT_TRUE(input.is_open()) << MILESTRO_TEST_ICUDTL_PATH;
    data.assign(std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>());
    ASSERT_FALSE(data.empty());
    UErrorCode status = U_ZERO_ERROR;
    udata_setCommonData(data.data(), &status);
    ASSERT_EQ(status, U_ZERO_ERROR) << u_errorName(status);
    loaded = true;
}

class TestFontMgr final : public SkFontMgr {
public:
    TestFontMgr() : loader(SkFontMgr_New_Custom_Empty()) {
    }

    bool addFontFile(const char* path) {
        auto typeface = loader->makeFromFile(path);
        if (!typeface) {
            return false;
        }
        provider->registerTypeface(typeface);
        typefaces.push_back(std::move(typeface));
        return true;
    }

protected:
    int onCountFamilies() const override {
        return provider->countFamilies();
    }

    void onGetFamilyName(int index, SkString* familyName) const override {
        provider->getFamilyName(index, familyName);
    }

    sk_sp<SkFontStyleSet> onCreateStyleSet(int index) const override {
        return provider->createStyleSet(index);
    }

    sk_sp<SkFontStyleSet> onMatchFamily(const char familyName[]) const override {
        return provider->matchFamily(familyName);
    }

    sk_sp<SkTypeface> onMatchFamilyStyle(const char familyName[], const SkFontStyle& style) const override {
        return provider->matchFamilyStyle(familyName, style);
    }

    sk_sp<SkTypeface> onMatchFamilyStyleCharacter(const char familyName[],
                                                  const SkFontStyle& style,
                                                  const char* bcp47[],
                                                  int bcp47Count,
                                                  SkUnichar character) const override {
        auto exact = provider->matchFamilyStyleCharacter(familyName, style, bcp47, bcp47Count, character);
        if (exact) {
            return exact;
        }
        for (const auto& typeface: typefaces) {
            if (typeface->unicharToGlyph(character) != 0) {
                return typeface;
            }
        }
        return nullptr;
    }

    sk_sp<SkTypeface> onMakeFromData(sk_sp<SkData> data, int ttcIndex) const override {
        return loader->makeFromData(std::move(data), ttcIndex);
    }

    sk_sp<SkTypeface> onMakeFromStreamIndex(std::unique_ptr<SkStreamAsset> stream, int ttcIndex) const override {
        return loader->makeFromStream(std::move(stream), ttcIndex);
    }

    sk_sp<SkTypeface> onMakeFromStreamArgs(std::unique_ptr<SkStreamAsset> stream,
                                           const SkFontArguments& args) const override {
        return loader->makeFromStream(std::move(stream), args);
    }

    sk_sp<SkTypeface> onMakeFromFile(const char path[], int ttcIndex) const override {
        return loader->makeFromFile(path, ttcIndex);
    }

    sk_sp<SkTypeface> onLegacyMakeTypeface(const char familyName[], SkFontStyle style) const override {
        if (familyName != nullptr) {
            return provider->matchFamilyStyle(familyName, style);
        }
        return typefaces.empty() ? nullptr : typefaces.front();
    }

private:
    sk_sp<SkFontMgr> loader;
    sk_sp<skia_text::TypefaceFontProvider> provider = sk_make_sp<skia_text::TypefaceFontProvider>();
    std::vector<sk_sp<SkTypeface>> typefaces;
};

sk_sp<skia_text::FontCollection> TestFontCollection() {
    static sk_sp<skia_text::FontCollection> collection = [] {
        auto fontMgr = sk_make_sp<TestFontMgr>();
        EXPECT_TRUE(fontMgr->addFontFile(MILESTRO_TEST_FONT_PATH));
        EXPECT_TRUE(fontMgr->addFontFile(MILESTRO_TEST_FALLBACK_FONT_PATH));
        EXPECT_TRUE(fontMgr->addFontFile(MILESTRO_TEST_EMOJI_FONT_PATH));
        EXPECT_TRUE(fontMgr->addFontFile(MILESTRO_TEST_CHWS_FONT_PATH));
        auto result = sk_make_sp<skia_text::FontCollection>();
        result->setDefaultFontManager(std::move(fontMgr));
        return result;
    }();
    return collection;
}

sk_sp<SkUnicode> TestUnicodeProvider() {
    static sk_sp<SkUnicode> provider = SkUnicodes::ICU::Make();
    return provider;
}

skia_text::TextStyle PrimaryStyle(SkScalar size = 48.0f) {
    skia_text::TextStyle style;
    style.setFontFamilies({SkString(kPrimaryFamily)});
    style.setFontSize(size);
    style.setLocale(SkString("ja"));
    style.setColor(SK_ColorBLACK);
    return style;
}

struct BuildOptions {
    skia_text::WritingMode writingMode = skia_text::WritingMode::kVerticalRl;
    skia_text::TextAlign align = skia_text::TextAlign::kStart;
    skia_text::TextDirection direction = skia_text::TextDirection::kLtr;
    size_t maxLines = std::numeric_limits<size_t>::max();
    const char* ellipsis = nullptr;
    bool fallback = true;
    bool replaceTabCharacters = false;
    SkScalar indent = 0;
};

std::unique_ptr<skia_text::Paragraph>
BuildCoreParagraph(const std::string& text,
                   const skia_text::TextStyle& textStyle,
                   const BuildOptions& options = {},
                   const std::function<void(skia_text::ParagraphBuilder*)>& extra = {}) {
    const auto collection = TestFontCollection();
    const bool oldFallback = collection->fontFallbackEnabled();
    options.fallback ? collection->enableFontFallback() : collection->disableFontFallback();
    skia_text::ParagraphStyle style;
    style.setTextStyle(textStyle);
    style.setWritingMode(options.writingMode);
    style.setTextAlign(options.align);
    style.setTextDirection(options.direction);
    style.setMaxLines(options.maxLines);
    if (options.ellipsis != nullptr) {
        style.setEllipsis(SkString(options.ellipsis));
    }
    style.setReplaceTabCharacters(options.replaceTabCharacters);
    if (options.indent != 0) {
        style.setTextIndent(options.indent, false);
    }
    auto builder = skia_text::ParagraphBuilder::make(style, collection, TestUnicodeProvider());
    builder->pushStyle(textStyle);
    builder->addText(text.c_str(), text.size());
    if (extra) {
        extra(builder.get());
    }
    auto paragraph = builder->Build();
    oldFallback ? collection->enableFontFallback() : collection->disableFontFallback();
    return paragraph;
}

size_t Utf16Length(const std::string& text) {
    return SkUnicode::convertUtf8ToUtf16(text.c_str(), static_cast<int>(text.size())).size();
}

struct VisitorRun {
    int line = 0;
    bool sideways = false;
    unsigned flags = 0;
    SkMatrix transform = SkMatrix::I();
    SkFont font;
    SkPoint origin;
    SkSize advance;
    std::vector<SkGlyphID> glyphs;
    std::vector<SkPoint> positions;
    std::vector<SkRect> bounds;
    std::vector<uint32_t> starts;
};

std::vector<VisitorRun> SnapshotRuns(skia_text::Paragraph* paragraph) {
    std::vector<VisitorRun> runs;
    paragraph->extendedVisit([&](int line, const skia_text::Paragraph::ExtendedVisitorInfo* info) {
        if (info == nullptr) {
            return;
        }
        VisitorRun run;
        run.line = line;
        run.flags = info->flags;
        run.transform = info->glyphTransform;
        run.font = info->font;
        run.sideways = !SkScalarNearlyZero(info->glyphTransform.getSkewX());
        run.origin = info->origin;
        run.advance = info->advance;
        run.glyphs.assign(info->glyphs, info->glyphs + info->count);
        run.positions.assign(info->positions, info->positions + info->count);
        run.bounds.assign(info->bounds, info->bounds + info->count);
        run.starts.assign(info->utf8Starts, info->utf8Starts + info->count + 1);
        runs.push_back(std::move(run));
    });
    return runs;
}

std::vector<SkPoint> AbsoluteGlyphOrigins(const VisitorRun& run) {
    std::vector<SkPoint> points;
    points.reserve(run.glyphs.size());
    for (size_t index = 0; index < run.glyphs.size(); ++index) {
        points.push_back(run.origin + run.positions[index]);
    }
    return points;
}

SkRect GlyphInkBounds(const VisitorRun& run, size_t index) {
    SkRect bounds = run.bounds[index];
    if (run.sideways) {
        bounds = SkRect::MakeLTRB(-bounds.bottom(), bounds.left(), -bounds.top(), bounds.right());
    }
    bounds.offset(run.origin.x() + run.positions[index].x(),
                  run.origin.y() + run.positions[index].y());
    return bounds;
}

std::optional<SkRect> VisitorInkBounds(const std::vector<VisitorRun>& runs) {
    std::optional<SkRect> result;
    for (const auto& run: runs) {
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            const SkRect bounds = GlyphInkBounds(run, index);
            if (bounds.isEmpty()) {
                continue;
            }
            if (!result.has_value()) {
                result = bounds;
            } else {
                result->join(bounds);
            }
        }
    }
    return result;
}

SkBitmap RasterizeText(skia_text::Paragraph* paragraph,
                       SkScalar x,
                       SkScalar y,
                       int width,
                       int height,
                       const SkRect* clip = nullptr) {
    SkBitmap bitmap;
    bitmap.allocN32Pixels(width, height);
    bitmap.eraseColor(SK_ColorWHITE);
    SkCanvas canvas(bitmap);
    if (clip != nullptr) {
        canvas.clipRect(*clip);
    }
    paragraph->paint(&canvas, x, y);
    return bitmap;
}

void SavePng(const SkBitmap& bitmap, const char* path) {
    const auto encoded = SkPngEncoder::Encode(bitmap.pixmap(), SkPngEncoder::Options{});
    ASSERT_TRUE(encoded != nullptr) << path;
    std::ofstream output(path, std::ios::binary);
    ASSERT_TRUE(output.is_open()) << path;
    output.write(static_cast<const char*>(encoded->data()), static_cast<std::streamsize>(encoded->size()));
    EXPECT_TRUE(output.good()) << path;
}

class CanvasPainter final : public skia_text::ParagraphPainter {
public:
    explicit CanvasPainter(SkCanvas* canvas) : canvas_(canvas) {
    }

    void drawTextBlob(const sk_sp<SkTextBlob>& blob, SkScalar x, SkScalar y, const SkPaintOrID& paint) override {
        canvas_->drawTextBlob(blob, x, y, std::get<SkPaint>(paint));
    }
    void
    drawTextShadow(const sk_sp<SkTextBlob>& blob, SkScalar x, SkScalar y, SkColor color, SkScalar blurSigma) override {
        SkPaint paint;
        paint.setColor(color);
        if (blurSigma != 0) {
            paint.setMaskFilter(SkMaskFilter::MakeBlur(kNormal_SkBlurStyle, blurSigma, false));
        }
        canvas_->drawTextBlob(blob, x, y, paint);
    }
    void drawRect(const SkRect& rect, const SkPaintOrID& paint) override {
        canvas_->drawRect(rect, std::get<SkPaint>(paint));
    }
    void drawFilledRect(const SkRect& rect, const DecorationStyle& decorStyle) override {
        SkPaint paint(decorStyle.skPaint());
        paint.setStroke(false);
        canvas_->drawRect(rect, paint);
    }
    void drawPath(const SkPath& path, const DecorationStyle& decorStyle) override {
        canvas_->drawPath(path, decorStyle.skPaint());
    }
    void drawLine(SkScalar x0, SkScalar y0, SkScalar x1, SkScalar y1, const DecorationStyle& decorStyle) override {
        canvas_->drawLine(x0, y0, x1, y1, decorStyle.skPaint());
    }
    void clipRect(const SkRect& rect) override {
        canvas_->clipRect(rect);
    }
    void translate(SkScalar dx, SkScalar dy) override {
        canvas_->translate(dx, dy);
    }
    void scale(SkScalar sx, SkScalar sy) override {
        canvas_->scale(sx, sy);
    }
    void rotate(SkScalar degrees) override {
        canvas_->rotate(degrees);
    }
    void save() override {
        canvas_->save();
    }
    void restore() override {
        canvas_->restore();
    }

private:
    SkCanvas* canvas_;
};

struct RecordedDecoration {
    SkRect rect;
    SkColor color;
    SkScalar strokeWidth;
};

struct RecordedBackground {
    SkRect rect;
    skia_text::ParagraphPainter::SkPaintOrID paint;
};

enum class RecordedPaintKind {
    Background,
    Shadow,
    Text,
    Decoration,
};

struct RecordedGlyphPaint {
    sk_sp<SkTextBlob> blob;
    SkPoint origin;
    SkMatrix transform;
    SkColor color;
    SkScalar blurSigma;
};

class RecordingPainter final : public skia_text::ParagraphPainter {
public:
    void drawTextBlob(const sk_sp<SkTextBlob>& blob, SkScalar originX, SkScalar originY, const SkPaintOrID&) override {
        ++textBlobCount;
        paintOrder.push_back(RecordedPaintKind::Text);
        texts.push_back({blob, transform.mapPoint({originX, originY}), transform, SK_ColorBLACK, 0});
    }

    void drawTextShadow(const sk_sp<SkTextBlob>& blob, SkScalar originX, SkScalar originY,
                        SkColor color, SkScalar blurSigma) override {
        shadows.push_back({blob, transform.mapPoint({originX, originY}), transform, color, blurSigma});
        paintOrder.push_back(RecordedPaintKind::Shadow);
    }

    void drawRect(const SkRect& rect, const SkPaintOrID& paint) override {
        backgrounds.push_back({rect, paint});
        paintOrder.push_back(RecordedPaintKind::Background);
    }

    void drawFilledRect(const SkRect& rect, const DecorationStyle& decorStyle) override {
        decorations.push_back({rect, decorStyle.getColor(), decorStyle.getStrokeWidth()});
        paintOrder.push_back(RecordedPaintKind::Decoration);
    }

    void drawPath(const SkPath&, const DecorationStyle&) override {
    }

    void drawLine(SkScalar, SkScalar, SkScalar, SkScalar, const DecorationStyle&) override {
    }

    void clipRect(const SkRect&) override {
    }

    void translate(SkScalar offsetX, SkScalar offsetY) override {
        transform.preTranslate(offsetX, offsetY);
    }

    void scale(SkScalar scaleX, SkScalar scaleY) override {
        transform.preScale(scaleX, scaleY);
    }

    void rotate(SkScalar degrees) override {
        transform.preRotate(degrees);
    }

    void save() override {
        transforms.push_back(transform);
    }

    void restore() override {
        transform = transforms.back();
        transforms.pop_back();
    }

    int textBlobCount = 0;
    std::vector<RecordedBackground> backgrounds;
    std::vector<RecordedDecoration> decorations;
    std::vector<RecordedPaintKind> paintOrder;
    std::vector<RecordedGlyphPaint> texts;
    std::vector<RecordedGlyphPaint> shadows;
    SkMatrix transform = SkMatrix::I();
    std::vector<SkMatrix> transforms;
};


} // namespace

namespace {

constexpr SkScalar kTolerance = 0.05f;

skia_text::TextStyle DecorationStyleFor(const skia_text::TextStyle& style,
                                        skia_text::TextDecoration decoration,
                                        SkColor color = SK_ColorTRANSPARENT,
                                        SkScalar thicknessMultiplier = 1.0f) {
    auto result = style;
    result.setDecoration(decoration);
    result.setDecorationColor(color);
    result.setDecorationThicknessMultiplier(thicknessMultiplier);
    return result;
}

skia_text::TextStyle LatinStyle(SkScalar size = 48.0f) {
    skia_text::TextStyle style;
    style.setFontFamilies({SkString(kLatinFamily)});
    style.setFontSize(size);
    style.setLocale(SkString("en"));
    style.setColor(SK_ColorBLACK);
    return style;
}

skia_text::TextStyle FamilyStyle(const char* family, SkScalar size) {
    skia_text::TextStyle style;
    style.setFontFamilies({SkString(family)});
    style.setFontSize(size);
    style.setLocale(SkString("en"));
    style.setColor(SK_ColorBLACK);
    return style;
}

size_t Utf16Offset(const std::string& text, size_t utf8Offset) {
    return Utf16Length(text.substr(0, utf8Offset));
}

bool MetricsAreFinite(const skia_text::ParagraphLayoutMetrics& metrics) {
    if (!std::isfinite(metrics.physicalSize.width()) || !std::isfinite(metrics.physicalSize.height()) ||
        !std::isfinite(metrics.longestInlineExtent) || !std::isfinite(metrics.minIntrinsicInlineExtent) ||
        !std::isfinite(metrics.maxIntrinsicInlineExtent)) {
        return false;
    }
    for (const auto& column: metrics.columns) {
        if (!std::isfinite(column.physicalBounds.left()) || !std::isfinite(column.physicalBounds.top()) ||
            !std::isfinite(column.physicalBounds.right()) || !std::isfinite(column.physicalBounds.bottom()) ||
            !std::isfinite(column.inlineExtent) || !std::isfinite(column.blockThickness) ||
            !std::isfinite(column.physicalBaseline)) {
            return false;
        }
    }
    if (!metrics.inkBounds.isEmpty() &&
        (!std::isfinite(metrics.inkBounds.left()) || !std::isfinite(metrics.inkBounds.top()) ||
         !std::isfinite(metrics.inkBounds.right()) || !std::isfinite(metrics.inkBounds.bottom()))) {
        return false;
    }
    return true;
}

SkPoint BoxCenter(const skia_text::TextBox& box) {
    return SkPoint::Make(box.rect.centerX(), box.rect.centerY());
}

std::unique_ptr<skia_text::Paragraph>
BuildCustomParagraph(const BuildOptions& options, const std::function<void(skia_text::ParagraphBuilder*)>& build) {
    const auto collection = TestFontCollection();
    const bool oldFallback = collection->fontFallbackEnabled();
    options.fallback ? collection->enableFontFallback() : collection->disableFontFallback();
    skia_text::ParagraphStyle style;
    style.setWritingMode(options.writingMode);
    style.setTextAlign(options.align);
    style.setTextDirection(options.direction);
    style.setMaxLines(options.maxLines);
    if (options.ellipsis != nullptr) {
        style.setEllipsis(SkString(options.ellipsis));
    }
    auto builder = skia_text::ParagraphBuilder::make(style, collection, TestUnicodeProvider());
    build(builder.get());
    auto paragraph = builder->Build();
    oldFallback ? collection->enableFontFallback() : collection->disableFontFallback();
    return paragraph;
}

std::unique_ptr<skia_text::Paragraph>
BuildParagraph(const std::string& text, const skia_text::TextStyle& style, const BuildOptions& options = {}) {
    return BuildCoreParagraph(text, style, options);
}

std::unique_ptr<skia_text::Paragraph>
BuildConfiguredParagraph(const std::string& text,
                         const skia_text::TextStyle& textStyle,
                         const std::function<void(skia_text::ParagraphStyle&)>& configure,
                         const std::function<void(skia_text::ParagraphBuilder*)>& extra = {}) {
    const auto collection = TestFontCollection();
    collection->enableFontFallback();
    skia_text::ParagraphStyle style;
    style.setTextStyle(textStyle);
    style.setWritingMode(skia_text::WritingMode::kVerticalRl);
    configure(style);
    auto builder = skia_text::ParagraphBuilder::make(style, collection, TestUnicodeProvider());
    builder->pushStyle(textStyle);
    builder->addText(text.c_str(), text.size());
    if (extra) {
        extra(builder.get());
    }
    return builder->Build();
}

std::unique_ptr<skia_text::Paragraph>
BuildEllipsisParagraph(
        const std::string& text,
        const skia_text::TextStyle& textStyle,
        size_t maxLines,
        const std::string& ellipsis = "\xE2\x80\xA6",
        skia_text::TextAlign align = skia_text::TextAlign::kStart,
        const std::function<void(skia_text::ParagraphBuilder*)>& extra = {}) {
    return BuildConfiguredParagraph(
            text,
            textStyle,
            [&](skia_text::ParagraphStyle& style) {
                style.setTextAlign(align);
                style.setMaxLines(maxLines);
                style.setEllipsis(SkString(ellipsis.c_str(), ellipsis.size()));
            },
            extra);
}

bool IsSyntheticAffixRun(const VisitorRun& run) {
    if (run.starts.size() < 2) {
        return false;
    }
    const uint32_t anchor = run.starts.front();
    return std::all_of(run.starts.begin(), run.starts.end(), [anchor](uint32_t start) {
        return start == anchor;
    });
}

std::vector<VisitorRun> SyntheticRuns(const std::vector<VisitorRun>& runs) {
    std::vector<VisitorRun> synthetic;
    for (const auto& run: runs) {
        if (IsSyntheticAffixRun(run)) {
            synthetic.push_back(run);
        }
    }
    return synthetic;
}

size_t SourceGlyphCount(const std::vector<VisitorRun>& runs) {
    size_t count = 0;
    for (const auto& run: runs) {
        if (!IsSyntheticAffixRun(run)) {
            count += run.glyphs.size();
        }
    }
    return count;
}

size_t Utf16OffsetForUtf8(const std::string& text, size_t utf8Offset) {
    utf8Offset = std::min(utf8Offset, text.size());
    return SkUnicode::convertUtf8ToUtf16(text.c_str(), static_cast<int>(utf8Offset)).size();
}

skia_text::ParagraphImpl* ImplOf(skia_text::Paragraph* paragraph) {
    return static_cast<skia_text::ParagraphImpl*>(paragraph);
}

SkPoint VisitorOriginAtUtf8(const std::vector<VisitorRun>& runs, int line, uint32_t utf8Start) {
    for (const auto& run: runs) {
        if (run.line != line) {
            continue;
        }
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            if (run.starts[index] == utf8Start) {
                return run.origin + run.positions[index];
            }
        }
    }
    return SkPoint::Make(SK_ScalarNaN, SK_ScalarNaN);
}

SkScalar InfiniteInlineExtent(const std::string& text, const skia_text::TextStyle& style) {
    auto paragraph = BuildParagraph(text, style, {});
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    return paragraph->getLayoutMetrics().longestInlineExtent;
}

bool IsClockwise(const SkMatrix& matrix) {
    return std::abs(matrix.getScaleX()) <= kTolerance && std::abs(matrix.getScaleY()) <= kTolerance &&
           std::abs(matrix.getSkewX() + 1.0f) <= kTolerance && std::abs(matrix.getSkewY() - 1.0f) <= kTolerance;
}

bool IsIdentity(const SkMatrix& matrix) {
    return std::abs(matrix.getScaleX() - 1.0f) <= kTolerance && std::abs(matrix.getScaleY() - 1.0f) <= kTolerance &&
           std::abs(matrix.getSkewX()) <= kTolerance && std::abs(matrix.getSkewY()) <= kTolerance &&
           std::abs(matrix.getTranslateX()) <= kTolerance && std::abs(matrix.getTranslateY()) <= kTolerance;
}

size_t CountGlyphs(const std::vector<VisitorRun>& runs) {
    size_t count = 0;
    for (const auto& run: runs) {
        count += run.glyphs.size();
    }
    return count;
}

int ColumnForUtf16(const skia_text::ParagraphLayoutMetrics& metrics, size_t offset) {
    for (size_t index = 0; index < metrics.columns.size(); ++index) {
        const auto& column = metrics.columns[index];
        if (column.utf16Start <= offset && offset < column.utf16End) {
            return static_cast<int>(index);
        }
    }
    return -1;
}

void ExpectContiguousSourceRanges(const skia_text::ParagraphLayoutMetrics& metrics, size_t utf16Length) {
    ASSERT_FALSE(metrics.columns.empty());
    size_t cursor = 0;
    for (const auto& column: metrics.columns) {
        EXPECT_EQ(column.utf16Start, cursor);
        EXPECT_LE(column.utf16Start, column.utf16End);
        EXPECT_LE(column.utf16EndExcludingWhitespace, column.utf16End);
        cursor = column.utf16End;
    }
    EXPECT_EQ(cursor, utf16Length);
}

std::vector<skia_text::TextBox> TightBoxes(skia_text::Paragraph* paragraph, size_t start, size_t end) {
    return paragraph->getRectsForRange(static_cast<unsigned>(start),
                                       static_cast<unsigned>(end),
                                       skia_text::RectHeightStyle::kTight,
                                       skia_text::RectWidthStyle::kTight);
}

SkPath CorePath(skia_text::Paragraph* paragraph, SkScalar x = 0, SkScalar y = 0) {
    SkPathBuilder builder;
    for (size_t line = 0; line < paragraph->lineNumber(); ++line) {
        SkPath path;
        EXPECT_EQ(paragraph->getPath(static_cast<int>(line), &path), 0);
        builder.addPath(path.makeOffset(x, y));
    }
    return builder.detach();
}

SkBitmap RasterizePath(const SkPath& path, int width, int height, const SkRect* clip = nullptr) {
    SkBitmap bitmap;
    bitmap.allocN32Pixels(width, height);
    bitmap.eraseColor(SK_ColorWHITE);
    SkCanvas canvas(bitmap);
    if (clip != nullptr) {
        canvas.clipRect(*clip);
    }
    SkPaint paint;
    paint.setAntiAlias(true);
    paint.setColor(SK_ColorBLACK);
    canvas.drawPath(path, paint);
    return bitmap;
}

size_t DifferingPixels(const SkBitmap& a, const SkBitmap& b) {
    EXPECT_EQ(a.width(), b.width());
    EXPECT_EQ(a.height(), b.height());
    size_t different = 0;
    for (int y = 0; y < std::min(a.height(), b.height()); ++y) {
        for (int x = 0; x < std::min(a.width(), b.width()); ++x) {
            if (a.getColor(x, y) != b.getColor(x, y)) {
                ++different;
            }
        }
    }
    return different;
}

size_t NonWhitePixels(const SkBitmap& bitmap, const SkRect* area = nullptr) {
    size_t count = 0;
    const int left = area == nullptr ? 0 : std::max(0, SkScalarFloorToInt(area->left()));
    const int top = area == nullptr ? 0 : std::max(0, SkScalarFloorToInt(area->top()));
    const int right = area == nullptr ? bitmap.width() : std::min(bitmap.width(), SkScalarCeilToInt(area->right()));
    const int bottom = area == nullptr ? bitmap.height() : std::min(bitmap.height(), SkScalarCeilToInt(area->bottom()));
    for (int y = top; y < bottom; ++y) {
        for (int x = left; x < right; ++x) {
            if (bitmap.getColor(x, y) != SK_ColorWHITE) {
                ++count;
            }
        }
    }
    return count;
}

size_t PixelsMatching(const SkBitmap& bitmap,
                      const std::function<bool(SkColor)>& predicate) {
    size_t count = 0;
    for (int y = 0; y < bitmap.height(); ++y) {
        for (int x = 0; x < bitmap.width(); ++x) {
            if (predicate(bitmap.getColor(x, y))) {
                ++count;
            }
        }
    }
    return count;
}

SkIRect MatchingBounds(const SkBitmap& bitmap,
                       const std::function<bool(SkColor)>& predicate) {
    SkIRect bounds = SkIRect::MakeEmpty();
    for (int y = 0; y < bitmap.height(); ++y) {
        for (int x = 0; x < bitmap.width(); ++x) {
            if (!predicate(bitmap.getColor(x, y))) {
                continue;
            }
            if (bounds.isEmpty()) {
                bounds.setLTRB(x, y, x + 1, y + 1);
            } else {
                bounds.join(SkIRect::MakeLTRB(x, y, x + 1, y + 1));
            }
        }
    }
    return bounds;
}

bool IsRedLike(SkColor color) {
    return SkColorGetR(color) > 160 && SkColorGetG(color) < 100 && SkColorGetB(color) < 100;
}

bool IsGreenLike(SkColor color) {
    return SkColorGetG(color) > 120 && SkColorGetR(color) < 120 && SkColorGetB(color) < 120;
}

std::vector<RecordedDecoration> RecordDecorations(skia_text::Paragraph* paragraph,
                                                  SkScalar x = 0,
                                                  SkScalar y = 0) {
    RecordingPainter painter;
    paragraph->paint(&painter, x, y);
    return painter.decorations;
}

SkRect SourceClusterCell(skia_text::Paragraph* paragraph, uint32_t utf8Offset) {
    skia_text::Paragraph::GlyphClusterInfo info;
    EXPECT_TRUE(paragraph->getGlyphClusterAt(utf8Offset, &info)) << utf8Offset;
    return info.fBounds;
}

const VisitorRun* FindVisitorRun(const std::vector<VisitorRun>& runs,
                                 int line,
                                 uint32_t utf8Start) {
    for (const auto& run: runs) {
        if (run.line != line || run.starts.empty() ||
            run.starts.front() > utf8Start || run.starts.back() <= utf8Start) {
            continue;
        }
        return &run;
    }
    return nullptr;
}

SkScalar DecorationThickness(const VisitorRun& run,
                             skia_text::TextDecoration decoration,
                             SkScalar multiplier) {
    SkFontMetrics metrics;
    run.font.getMetrics(&metrics);
    SkScalar thickness = run.font.getSize() / 14.0f;
    if (decoration == skia_text::TextDecoration::kUnderline &&
        (metrics.fFlags & SkFontMetrics::kUnderlineThicknessIsValid_Flag) != 0 &&
        std::isfinite(metrics.fUnderlineThickness) && metrics.fUnderlineThickness > 0) {
        thickness = metrics.fUnderlineThickness;
    } else if (decoration == skia_text::TextDecoration::kLineThrough &&
               (metrics.fFlags & SkFontMetrics::kStrikeoutThicknessIsValid_Flag) != 0 &&
               std::isfinite(metrics.fStrikeoutThickness) && metrics.fStrikeoutThickness > 0) {
        thickness = metrics.fStrikeoutThickness;
    }
    return thickness * multiplier;
}

SkScalar DecorationBlockCenter(const VisitorRun& run) {
    if (!run.sideways) {
        return run.origin.x();
    }
    SkFontMetrics metrics;
    run.font.getMetrics(&metrics);
    return run.origin.x() - (metrics.fAscent + metrics.fDescent) * 0.5f;
}

SkScalar UnderlineCenterX(const VisitorRun& run, SkScalar thickness) {
    SkFontMetrics metrics;
    run.font.getMetrics(&metrics);
    SkScalar position = thickness;
    if ((metrics.fFlags & SkFontMetrics::kUnderlinePositionIsValid_Flag) != 0 &&
        std::isfinite(metrics.fUnderlinePosition) && metrics.fUnderlinePosition > 0) {
        position = metrics.fUnderlinePosition;
    }
    return DecorationBlockCenter(run) - run.font.getSize() * 0.5f - position;
}

SkScalar LineThroughCenterX(const VisitorRun& run) {
    return DecorationBlockCenter(run);
}

void ExpectRectNear(const SkRect& actual, const SkRect& expected, SkScalar tolerance = kTolerance) {
    EXPECT_NEAR(actual.left(), expected.left(), tolerance);
    EXPECT_NEAR(actual.top(), expected.top(), tolerance);
    EXPECT_NEAR(actual.right(), expected.right(), tolerance);
    EXPECT_NEAR(actual.bottom(), expected.bottom(), tolerance);
}

SkIRect NonWhiteBounds(const SkBitmap& bitmap) {
    SkIRect bounds = SkIRect::MakeEmpty();
    for (int y = 0; y < bitmap.height(); ++y) {
        for (int x = 0; x < bitmap.width(); ++x) {
            if (bitmap.getColor(x, y) == SK_ColorWHITE) {
                continue;
            }
            if (bounds.isEmpty()) {
                bounds.setLTRB(x, y, x + 1, y + 1);
            } else {
                bounds.join(SkIRect::MakeLTRB(x, y, x + 1, y + 1));
            }
        }
    }
    return bounds;
}

struct SnapshotColumn {
    size_t utf16Start = 0;
    size_t utf16End = 0;
    SkRect bounds = SkRect::MakeEmpty();
    SkScalar inlineExtent = 0;
    SkScalar blockThickness = 0;
    SkScalar baseline = 0;
};

struct ParagraphSnapshot {
    SkSize physicalSize;
    SkRect contentBounds;
    SkRect inkBounds;
    SkScalar longest = 0;
    SkScalar minIntrinsic = 0;
    SkScalar maxIntrinsic = 0;
    bool exceededMaxLines = false;
    std::vector<SnapshotColumn> columns;
    std::vector<VisitorRun> runs;
};

ParagraphSnapshot Capture(skia_text::Paragraph* paragraph) {
    const auto& metrics = paragraph->getLayoutMetrics();
    ParagraphSnapshot snapshot;
    snapshot.physicalSize = metrics.physicalSize;
    snapshot.contentBounds = metrics.contentBounds;
    snapshot.inkBounds = metrics.inkBounds;
    snapshot.longest = metrics.longestInlineExtent;
    snapshot.minIntrinsic = metrics.minIntrinsicInlineExtent;
    snapshot.maxIntrinsic = metrics.maxIntrinsicInlineExtent;
    snapshot.exceededMaxLines = paragraph->didExceedMaxLines();
    for (const auto& column: metrics.columns) {
        snapshot.columns.push_back({column.utf16Start,
                                    column.utf16End,
                                    column.physicalBounds,
                                    column.inlineExtent,
                                    column.blockThickness,
                                    column.physicalBaseline});
    }
    snapshot.runs = SnapshotRuns(paragraph);
    return snapshot;
}

void ExpectSnapshotsNear(const ParagraphSnapshot& expected, const ParagraphSnapshot& actual) {
    EXPECT_NEAR(actual.physicalSize.width(), expected.physicalSize.width(), kTolerance);
    EXPECT_NEAR(actual.physicalSize.height(), expected.physicalSize.height(), kTolerance);
    EXPECT_NEAR(actual.longest, expected.longest, kTolerance);
    EXPECT_NEAR(actual.minIntrinsic, expected.minIntrinsic, kTolerance);
    EXPECT_NEAR(actual.maxIntrinsic, expected.maxIntrinsic, kTolerance);
    EXPECT_EQ(actual.exceededMaxLines, expected.exceededMaxLines);
    ASSERT_EQ(actual.columns.size(), expected.columns.size());
    for (size_t index = 0; index < expected.columns.size(); ++index) {
        EXPECT_EQ(actual.columns[index].utf16Start, expected.columns[index].utf16Start);
        EXPECT_EQ(actual.columns[index].utf16End, expected.columns[index].utf16End);
        EXPECT_NEAR(actual.columns[index].bounds.left(), expected.columns[index].bounds.left(), kTolerance);
        EXPECT_NEAR(actual.columns[index].bounds.top(), expected.columns[index].bounds.top(), kTolerance);
        EXPECT_NEAR(actual.columns[index].inlineExtent, expected.columns[index].inlineExtent, kTolerance);
        EXPECT_NEAR(actual.columns[index].blockThickness, expected.columns[index].blockThickness, kTolerance);
        EXPECT_NEAR(actual.columns[index].baseline, expected.columns[index].baseline, kTolerance);
    }
    ASSERT_EQ(actual.runs.size(), expected.runs.size());
    for (size_t index = 0; index < expected.runs.size(); ++index) {
        EXPECT_EQ(actual.runs[index].line, expected.runs[index].line);
        EXPECT_EQ(actual.runs[index].glyphs, expected.runs[index].glyphs);
        EXPECT_EQ(actual.runs[index].starts, expected.runs[index].starts);
        EXPECT_TRUE(IsClockwise(actual.runs[index].transform) == IsClockwise(expected.runs[index].transform));
        EXPECT_EQ(actual.runs[index].font.getTypeface()->uniqueID(),
                  expected.runs[index].font.getTypeface()->uniqueID());
        EXPECT_NEAR(actual.runs[index].font.getSize(), expected.runs[index].font.getSize(), kTolerance);
        EXPECT_NEAR(actual.runs[index].origin.x(), expected.runs[index].origin.x(), kTolerance);
        EXPECT_NEAR(actual.runs[index].origin.y(), expected.runs[index].origin.y(), kTolerance);
    }
}

class SkiaVerticalParagraph : public ::testing::Test {
protected:
    void SetUp() override {
        LoadIcuData();
        TestFontCollection()->enableFontFallback();
        TestFontCollection()->clearCaches();
    }
};

skia_text::PlaceholderStyle MiddlePlaceholder(float width, float height) {
    return {width, height, skia_text::PlaceholderAlignment::kMiddle,
            skia_text::TextBaseline::kAlphabetic, 0};
}

std::unique_ptr<skia_text::Paragraph> BuildVerticalRuby(
        const std::string& text = "漢字", const std::string& annotation = "かんじ",
        skia_text::RubyStyle rubyStyle = {}, const BuildOptions& options = {}) {
    return BuildCoreParagraph(text, PrimaryStyle(40), options, [&](auto* builder) {
        builder->addRuby(skia_text::RubyAnnotation({0, text.size()}, annotation, rubyStyle));
    });
}

TEST_F(SkiaVerticalParagraph, RB01_RB02_RB09_RightSideMixedGlyphsAndSharedGeometry) {
    auto plain = BuildCoreParagraph("漢字", PrimaryStyle(40));
    auto ruby = BuildVerticalRuby("漢字", "かなABC");
    plain->layoutWithInlineExtent(400);
    ruby->layoutWithInlineExtent(400);
    EXPECT_THROW(ruby->visit([](int, const skia_text::Paragraph::VisitorInfo*) {}), std::logic_error);
    const auto& plainColumn = plain->getLayoutMetrics().columns[0];
    const auto& rubyColumn = ruby->getLayoutMetrics().columns[0];
    EXPECT_FLOAT_EQ(plainColumn.physicalBaseline, rubyColumn.physicalBaseline);
    EXPECT_GT(rubyColumn.blockThickness, plainColumn.blockThickness + 2);
    bool upright = false;
    bool sideways = false;
    for (const auto& run : SnapshotRuns(ruby.get())) {
        if (!(run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag)) continue;
        upright |= !run.sideways;
        sideways |= run.sideways;
        EXPECT_GE(run.origin.x(), plainColumn.physicalBounds.right() + 2);
        for (auto start : run.starts) EXPECT_TRUE(start == 0 || start == 6);
    }
    EXPECT_TRUE(upright);
    EXPECT_TRUE(sideways);
    const auto boxes = TightBoxes(ruby.get(), 0, 2);
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_FLOAT_EQ(boxes[0].rect.width(), rubyColumn.blockThickness);
    SkPath plainPath;
    SkPath rubyPath;
    plain->getPath(0, &plainPath);
    ruby->getPath(0, &rubyPath);
    EXPECT_GT(rubyPath.getBounds().right(), plainPath.getBounds().right());
    EXPECT_GT(rubyPath.countVerbs(), plainPath.countVerbs());
}

TEST_F(SkiaVerticalParagraph, RB08_LineHeightDoesNotChangeAnnotationDistanceOrThickness) {
    SkScalar referenceDistance = 0;
    SkScalar referenceWidth = 0;
    SkScalar referenceInline = 0;
    for (const SkScalar height : {0.f, 1.f, 1.5f, 2.f, 3.f}) {
        auto style = PrimaryStyle(40);
        style.setHeightOverride(height != 0);
        style.setHeight(height);
        auto paragraph = BuildCoreParagraph("東京の夜", style, {}, [](auto* builder) {
            builder->addRuby(skia_text::RubyAnnotation({0, 6}, "とうきょう"));
        });
        paragraph->layoutWithInlineExtent(400);
        const auto runs = SnapshotRuns(paragraph.get());
        SkScalar baseX = 0;
        SkScalar annotationX = 0;
        for (const auto& run : runs) {
            if (run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag) {
                annotationX = run.origin.x();
                EXPECT_FLOAT_EQ(run.font.getSize(), 20);
            } else {
                baseX = run.origin.x();
            }
        }
        if (height == 0) {
            referenceDistance = annotationX - baseX;
            referenceWidth = paragraph->getLayoutMetrics().columns[0].blockThickness;
            referenceInline = paragraph->getLayoutMetrics().longestInlineExtent;
        }
        EXPECT_NEAR(annotationX - baseX, referenceDistance, kTolerance) << height;
        EXPECT_NEAR(paragraph->getLayoutMetrics().longestInlineExtent, referenceInline, kTolerance);
        EXPECT_GE(paragraph->getLayoutMetrics().columns[0].blockThickness, height * 40);
        if (height == 3) EXPECT_NEAR(paragraph->getLayoutMetrics().columns[0].blockThickness, 120, kTolerance);
        EXPECT_GT(referenceWidth, 40);
    }
}

TEST_F(SkiaVerticalParagraph, RB03_AllInlineAlignments) {
    for (const auto alignment : {skia_text::RubyAlignment::Start, skia_text::RubyAlignment::Center,
            skia_text::RubyAlignment::End, skia_text::RubyAlignment::SpaceAround,
            skia_text::RubyAlignment::SpaceBetween}) {
        SCOPED_TRACE(static_cast<int>(alignment));
        skia_text::RubyStyle style;
        style.alignment = alignment;
        auto ruby = BuildVerticalRuby("漢字", "あいうえおかきく", style);
        ruby->layoutWithInlineExtent(400);
        const auto runs = SnapshotRuns(ruby.get());
        ASSERT_GE(runs.size(), 2u);
        const auto base = AbsoluteGlyphOrigins(runs[0]);
        const auto extent = ruby->getLayoutMetrics().columns[0].inlineExtent;
        const float extra = extent - 80;
        const float expected = alignment == skia_text::RubyAlignment::Start ? 0 :
                alignment == skia_text::RubyAlignment::End ? extra :
                alignment == skia_text::RubyAlignment::SpaceAround ? extra / 4 :
                alignment == skia_text::RubyAlignment::SpaceBetween ? 0 : extra / 2;
        auto plain = BuildCoreParagraph("漢字", PrimaryStyle(40));
        plain->layoutWithInlineExtent(400);
        const auto plainBase = AbsoluteGlyphOrigins(SnapshotRuns(plain.get())[0]);
        EXPECT_NEAR(base[0].y() - plainBase[0].y(), expected, 0.1);
        if (alignment == skia_text::RubyAlignment::SpaceAround ||
            alignment == skia_text::RubyAlignment::SpaceBetween) {
            EXPECT_GT(base[1].y() - base[0].y(), 40);
        }
    }
}

TEST_F(SkiaVerticalParagraph, RB04_RB06_AllocationOverhangAndOverflowPolicy) {
    skia_text::RubyStyle style;
    style.overflow = skia_text::RubyOverflow::Inline;
    auto inlineRuby = BuildVerticalRuby("漢", "あいうえおかきく", style);
    inlineRuby->layoutWithInlineExtent(80);
    style.overflow = skia_text::RubyOverflow::PreserveBase;
    auto preserve = BuildVerticalRuby("漢", "あいうえおかきく", style);
    preserve->layoutWithInlineExtent(80);
    EXPECT_FLOAT_EQ(inlineRuby->getLayoutMetrics().maxIntrinsicInlineExtent,
                    preserve->getLayoutMetrics().maxIntrinsicInlineExtent);
    const auto inlineBase = AbsoluteGlyphOrigins(SnapshotRuns(inlineRuby.get())[0]);
    const auto preserveBase = AbsoluteGlyphOrigins(SnapshotRuns(preserve.get())[0]);
    EXPECT_LT(preserveBase[0].y(), inlineBase[0].y());
    style.overhang = skia_text::RubyOverhang::Allow;
    auto overhang = BuildVerticalRuby("漢", "あいうえおかきく", style);
    overhang->layoutWithInlineExtent(80);
    EXPECT_NEAR(overhang->getLayoutMetrics().maxIntrinsicInlineExtent, 40, 0.1);
    EXPECT_GT(overhang->getLayoutMetrics().inkBounds.height(), 80);
}

TEST_F(SkiaVerticalParagraph, RB05_RB07_AtomicWrapAndEllipsis) {
    auto make = [](const BuildOptions& options) {
        return BuildCoreParagraph("天漢字地", PrimaryStyle(40), options, [](auto* builder) {
            builder->addRuby(skia_text::RubyAnnotation({3, 9}, "かんじ"));
        });
    };
    auto ruby = make({});
    ruby->layoutWithInlineExtent(100);
    const auto& columns = ruby->getLayoutMetrics().columns;
    ASSERT_EQ(columns.size(), 3u);
    EXPECT_EQ(columns[1].utf8Range.start, 3u);
    EXPECT_EQ(columns[1].utf8Range.end, 9u);
    auto ellipsis = make({.maxLines = 1, .ellipsis = "…"});
    ellipsis->layoutWithInlineExtent(100);
    EXPECT_TRUE(TightBoxes(ellipsis.get(), 1, 3).empty());
    for (const auto& run : SnapshotRuns(ellipsis.get())) {
        EXPECT_FALSE(run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag);
    }
}

TEST_F(SkiaVerticalParagraph, RB08_RB10_JustifyIndentShiftAndPlaceholder) {
    auto ruby = BuildConfiguredParagraph("天漢字地日月", PrimaryStyle(40), [](auto& style) {
        style.setTextAlign(skia_text::TextAlign::kJustify);
        style.setTextIndent(10, false);
    }, [](auto* builder) {
        builder->addRuby(skia_text::RubyAnnotation({3, 9}, "かんじ"));
    });
    ruby->layoutWithInlineExtent(190);
    const auto& geometry = static_cast<skia_text::ParagraphImpl*>(ruby.get())->verticalColumns()[0];
    ASSERT_TRUE(geometry.justified);
    ASSERT_GE(geometry.justificationShifts.size(), 4u);
    EXPECT_FLOAT_EQ(geometry.justificationShifts[1], geometry.justificationShifts[2]);
    auto textStyle = PrimaryStyle(40);
    textStyle.setBaselineShift(-15);
    auto shifted = BuildCoreParagraph("漢字", textStyle, {}, [](auto* builder) {
        builder->addRuby(skia_text::RubyAnnotation({0, 6}, "かんじ"));
        builder->addPlaceholder(MiddlePlaceholder(90, 20));
    });
    shifted->layoutWithInlineExtent(400);
    const auto& column = shifted->getLayoutMetrics().columns[0];
    EXPECT_GT(column.blockThickness, 90);
    EXPECT_EQ(shifted->getRectsForPlaceholders().size(), 1u);
}

TEST_F(SkiaVerticalParagraph, RB13_StrictValidation) {
    for (int invalid = 0; invalid < 5; ++invalid) {
        SCOPED_TRACE(invalid);
        skia_text::RubyStyle style;
        if (invalid == 0) style.position = skia_text::RubyPosition::Below;
        if (invalid == 1) style.fontSizeMultiplier = 0;
        if (invalid == 2) style.fontSizeMultiplier = std::numeric_limits<float>::infinity();
        if (invalid == 3) style.gap = -1;
        if (invalid == 4) style.gap = std::numeric_limits<float>::quiet_NaN();
        EXPECT_THROW({ auto paragraph = BuildVerticalRuby("漢字", "かな", style);
                       paragraph->layoutWithInlineExtent(200); }, std::invalid_argument);
    }
    EXPECT_THROW({ auto paragraph = BuildVerticalRuby("漢\n字");
                   paragraph->layoutWithInlineExtent(200); }, std::invalid_argument);
    EXPECT_THROW({ auto paragraph = BuildVerticalRuby("漢字", "か\nな");
                   paragraph->layoutWithInlineExtent(200); }, std::invalid_argument);
    EXPECT_THROW({ auto paragraph = BuildVerticalRuby("漢字", "か\tな");
                   paragraph->layoutWithInlineExtent(200); }, std::invalid_argument);
    EXPECT_THROW({ auto paragraph = BuildCoreParagraph("漢字", PrimaryStyle(), {}, [](auto* builder) {
        builder->addRuby(skia_text::RubyAnnotation({0, 6}, "かな"));
        builder->addRuby(skia_text::RubyAnnotation({0, 3}, "か"));
    }); }, std::invalid_argument);
    auto placeholder = BuildCoreParagraph("漢", PrimaryStyle(), {}, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(40, 20));
        builder->addRuby(skia_text::RubyAnnotation({0, 6}, "かな"));
    });
    EXPECT_THROW(placeholder->layoutWithInlineExtent(200), std::invalid_argument);
}

TEST_F(SkiaVerticalParagraph, RB15_CacheIsolationResizeAndFailureRecovery) {
    auto ruby = BuildVerticalRuby("漢字", "あいうえおかきく");
    ruby->layoutWithInlineExtent(400);
    const auto first = SnapshotRuns(ruby.get());
    auto independent = BuildVerticalRuby("漢字", "か");
    independent->layoutWithInlineExtent(80);
    ruby->layoutWithInlineExtent(80);
    EXPECT_THROW(ruby->layoutWithInlineExtent(-1), std::invalid_argument);
    ruby->layoutWithInlineExtent(400);
    const auto restored = SnapshotRuns(ruby.get());
    ASSERT_EQ(first.size(), restored.size());
    for (size_t index = 0; index < first.size(); ++index) {
        EXPECT_EQ(first[index].positions, restored[index].positions);
        EXPECT_EQ(first[index].origin, restored[index].origin);
    }
    EXPECT_NE(ruby->getLayoutMetrics().maxIntrinsicInlineExtent,
              independent->getLayoutMetrics().maxIntrinsicInlineExtent);
}

TEST_F(SkiaVerticalParagraph, RB03_RB12_ShortAnnotationAlignmentAndDerivedFontSize) {
    std::vector<float> starts;
    for (auto alignment : {skia_text::RubyAlignment::Start, skia_text::RubyAlignment::Center,
                           skia_text::RubyAlignment::End}) {
        skia_text::RubyStyle style;
        style.alignment = alignment;
        auto ruby = BuildVerticalRuby("漢字", "か", style);
        ruby->layoutWithInlineExtent(200);
        for (const auto& run : SnapshotRuns(ruby.get())) {
            if (run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag) {
                EXPECT_FLOAT_EQ(run.font.getSize(), 20);
                starts.push_back(AbsoluteGlyphOrigins(run)[0].y());
            }
        }
        ruby->updateFontSize(0, 6, 60);
        ruby->layoutWithInlineExtent(200);
        for (const auto& run : SnapshotRuns(ruby.get())) {
            if (run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag) {
                EXPECT_FLOAT_EQ(run.font.getSize(), 30);
            }
        }
    }
    ASSERT_EQ(starts.size(), 3u);
    EXPECT_NEAR(starts[1] - starts[0], 30, 0.1);
    EXPECT_NEAR(starts[2] - starts[0], 60, 0.1);
}

TEST_F(SkiaVerticalParagraph, RB15_ForegroundUpdateRecolorsInheritedAnnotationWithoutRelayout) {
    auto ruby = BuildVerticalRuby();
    ruby->layoutWithInlineExtent(200);
    SkPaint paint;
    paint.setColor(SK_ColorRED);
    ruby->updateForegroundPaint(0, 6, paint);
    const auto bitmap = RasterizeText(ruby.get(), 0, 0, 100, 200);
    size_t annotationPixels = 0;
    for (int pixelY = 0; pixelY < bitmap.height(); ++pixelY) {
        for (int pixelX = 42; pixelX < bitmap.width(); ++pixelX) {
            const auto color = bitmap.getColor(pixelX, pixelY);
            annotationPixels += SkColorGetR(color) > SkColorGetG(color) + 10 &&
                                SkColorGetR(color) > SkColorGetB(color) + 10 ? 1 : 0;
        }
    }
    EXPECT_GT(annotationPixels, 20u);
}

TEST_F(SkiaVerticalParagraph, RB07_TrailingWhitespaceInsideRubyCannotLeavePartialGroup) {
    auto ruby = BuildCoreParagraph("漢 字地", PrimaryStyle(40), {.maxLines = 1, .ellipsis = "…"},
        [](auto* builder) { builder->addRuby(skia_text::RubyAnnotation({0, 4}, "かな")); });
    ruby->layoutWithInlineExtent(65);
    EXPECT_TRUE(TightBoxes(ruby.get(), 0, 2).empty());
    for (const auto& run : SnapshotRuns(ruby.get())) {
        EXPECT_FALSE(run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag);
    }
    auto full = BuildVerticalRuby("漢字", "かんじ");
    auto removed = BuildVerticalRuby("漢字", "かんじ", {}, {.maxLines = 1, .ellipsis = "…"});
    full->layoutWithInlineExtent(65);
    removed->layoutWithInlineExtent(65);
    EXPECT_FLOAT_EQ(full->getLayoutMetrics().columns[0].blockThickness,
                    removed->getLayoutMetrics().columns[0].blockThickness);
    EXPECT_TRUE(TightBoxes(removed.get(), 0, 2).empty());
}

std::unique_ptr<skia_text::Paragraph> BuildVerticalTateChuYoko(
        const std::string& text, std::vector<skia_text::TextRange> ranges,
        const skia_text::TextStyle& textStyle = PrimaryStyle(40),
        const BuildOptions& options = {},
        const std::function<void(skia_text::ParagraphBuilder*)>& extra = {}) {
    return BuildCoreParagraph(text, textStyle, options, [&](auto* builder) {
        for (auto range : ranges) {
            builder->addTateChuYoko(range);
        }
        if (extra) {
            extra(builder);
        }
    });
}

std::unique_ptr<skia_text::Paragraph> BuildVerticalTateChuYokoModes(
        const std::string& text,
        std::vector<skia_text::TateChuYokoAnnotation> annotations,
        const skia_text::TextStyle& textStyle = PrimaryStyle(40),
        const BuildOptions& options = {},
        const std::function<void(skia_text::ParagraphBuilder*)>& extra = {}) {
    return BuildCoreParagraph(text, textStyle, options, [&](auto* builder) {
        for (const auto& annotation : annotations) {
            builder->addTateChuYoko(annotation.range, annotation.sizing);
        }
        if (extra) {
            extra(builder);
        }
    });
}

std::vector<VisitorRun> TateChuYokoRuns(const std::vector<VisitorRun>& runs) {
    std::vector<VisitorRun> result;
    for (const auto& run : runs) {
        if (run.flags & skia_text::Paragraph::kTateChuYoko_VisitorFlag) {
            result.push_back(run);
        }
    }
    return result;
}

TEST_F(SkiaVerticalParagraph, TC01_BasicCellGeometryIsHorizontalAndOneEm) {
    auto plain = BuildCoreParagraph("天地", PrimaryStyle(40));
    auto tcy = BuildVerticalTateChuYoko("天12", {{3, 5}});
    plain->layoutWithInlineExtent(400);
    tcy->layoutWithInlineExtent(400);

    EXPECT_NEAR(tcy->getLayoutMetrics().longestInlineExtent,
                plain->getLayoutMetrics().longestInlineExtent, kTolerance);
    const auto runs = TateChuYokoRuns(SnapshotRuns(tcy.get()));
    ASSERT_EQ(runs.size(), 1u);
    EXPECT_FALSE(runs[0].sideways);
    EXPECT_FALSE(IsClockwise(runs[0].transform));
    EXPECT_FLOAT_EQ(runs[0].advance.height(), 40);
    EXPECT_EQ(runs[0].starts, (std::vector<uint32_t>{3, 4, 5}));

    const auto boxes = TightBoxes(tcy.get(), 1, 3);
    ASSERT_EQ(boxes.size(), 1u);
    const auto& column = tcy->getLayoutMetrics().columns[0];
    EXPECT_NEAR(boxes[0].rect.height(), 40, kTolerance);
    EXPECT_NEAR(boxes[0].rect.centerX(), column.physicalBaseline, kTolerance);
    EXPECT_NEAR(runs[0].origin.x(), boxes[0].rect.left(), kTolerance);
    const auto absoluteOrigins = AbsoluteGlyphOrigins(runs[0]);
    ASSERT_FALSE(absoluteOrigins.empty());
    EXPECT_GE(absoluteOrigins[0].y(), boxes[0].rect.top());
    EXPECT_LE(absoluteOrigins[0].y(), boxes[0].rect.bottom());
}

TEST_F(SkiaVerticalParagraph, TC02_TC03_OverwideSqueezesAndNarrowKeepsNaturalWidth) {
    auto wideContent = BuildCoreParagraph("ABC", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    wideContent->layout(10000);
    const SkScalar wideNatural = wideContent->getMaxIntrinsicWidth();
    EXPECT_GT(wideNatural, 40);

    auto squeezed = BuildVerticalTateChuYoko("天ABC", {{3, 6}});
    squeezed->layoutWithInlineExtent(400);
    const auto squeezedRuns = TateChuYokoRuns(SnapshotRuns(squeezed.get()));
    ASSERT_EQ(squeezedRuns.size(), 1u);
    EXPECT_NEAR(squeezedRuns[0].transform.getScaleX(), 40 / wideNatural, 0.001);
    auto squeezedBoxes = TightBoxes(squeezed.get(), 1, 4);
    ASSERT_EQ(squeezedBoxes.size(), 1u);
    EXPECT_NEAR(squeezedBoxes[0].rect.width(), 40, kTolerance);
    EXPECT_NEAR(squeezed->getLayoutMetrics().longestInlineExtent, 80, kTolerance);

    auto narrowContent = BuildCoreParagraph("A", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    narrowContent->layout(10000);
    const SkScalar narrowNatural = narrowContent->getMaxIntrinsicWidth();
    EXPECT_LT(narrowNatural, 40);

    auto natural = BuildVerticalTateChuYoko("天A", {{3, 4}});
    natural->layoutWithInlineExtent(400);
    const auto naturalRuns = TateChuYokoRuns(SnapshotRuns(natural.get()));
    ASSERT_EQ(naturalRuns.size(), 1u);
    EXPECT_NEAR(naturalRuns[0].transform.getScaleX(), 1, 0.001);
    auto naturalBoxes = TightBoxes(natural.get(), 1, 2);
    ASSERT_EQ(naturalBoxes.size(), 1u);
    EXPECT_NEAR(naturalBoxes[0].rect.width(), narrowNatural, kTolerance);
}

TEST_F(SkiaVerticalParagraph, TC04_CellWidthGrowsColumnSymmetric) {
    auto style = PrimaryStyle(40);
    style.setHeightOverride(true);
    style.setHeight(0.4f);
    auto plain = BuildCoreParagraph("天", style);
    auto tcy = BuildVerticalTateChuYoko("天1", {{3, 4}}, style);
    plain->layoutWithInlineExtent(400);
    tcy->layoutWithInlineExtent(400);
    const auto plainColumn = plain->getLayoutMetrics().columns[0];
    const auto tcyColumn = tcy->getLayoutMetrics().columns[0];
    const auto boxes = TightBoxes(tcy.get(), 1, 2);
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_GT(boxes[0].rect.width(), plainColumn.blockThickness);
    EXPECT_NEAR(tcyColumn.blockThickness, boxes[0].rect.width(), kTolerance);
    EXPECT_NEAR(boxes[0].rect.centerX(), tcyColumn.physicalBounds.centerX(), kTolerance);
    EXPECT_LT(plainColumn.blockThickness, tcyColumn.blockThickness);
}

TEST_F(SkiaVerticalParagraph, TC05_AtomicWrapAndJustifyOpportunity) {
    auto wrapped = BuildVerticalTateChuYoko("天12地", {{3, 5}});
    wrapped->layoutWithInlineExtent(50);
    const auto& metrics = wrapped->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 3u);
    EXPECT_EQ(metrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(metrics.columns[0].utf16End, 1u);
    EXPECT_EQ(metrics.columns[1].utf16Start, 1u);
    EXPECT_EQ(metrics.columns[1].utf16End, 3u);
    EXPECT_EQ(metrics.columns[2].utf16Start, 3u);
    EXPECT_EQ(metrics.columns[2].utf16End, 4u);
    const auto boxes = TightBoxes(wrapped.get(), 1, 3);
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_NEAR(boxes[0].rect.height(), 40, kTolerance);

    auto start = BuildVerticalTateChuYoko("天12地", {{3, 5}});
    auto justify = BuildVerticalTateChuYoko("天12地", {{3, 5}},
            PrimaryStyle(40), {.align = skia_text::TextAlign::kJustify});
    start->layoutWithInlineExtent(100);
    justify->layoutWithInlineExtent(100);
    const auto startBox = TightBoxes(start.get(), 1, 3);
    const auto justifyBox = TightBoxes(justify.get(), 1, 3);
    ASSERT_EQ(startBox.size(), 1u);
    ASSERT_EQ(justifyBox.size(), 1u);
    EXPECT_NEAR(justifyBox[0].rect.top() - startBox[0].rect.top(), 20, 0.1);
}

TEST_F(SkiaVerticalParagraph, TC06_EllipsisRemovesWholeGroupAndSnapsInsertion) {
    auto removed = BuildEllipsisParagraph("天地12", PrimaryStyle(40), 1, "\xE2\x80\xA6",
            skia_text::TextAlign::kStart,
            [](auto* builder) { builder->addTateChuYoko({6, 8}); });
    removed->layoutWithInlineExtent(90);
    EXPECT_TRUE(removed->didExceedMaxLines());
    EXPECT_TRUE(TightBoxes(removed.get(), 2, 4).empty());
    EXPECT_TRUE(TateChuYokoRuns(SnapshotRuns(removed.get())).empty());
    ASSERT_FALSE(removed->getLayoutMetrics().columns.empty());
    EXPECT_LE(removed->getLayoutMetrics().columns[0].utf16End, 2u);
    for (const auto& run : SnapshotRuns(removed.get())) {
        if (IsSyntheticAffixRun(run)) continue;
        for (size_t index = 0; index + 1 < run.starts.size(); ++index) {
            EXPECT_LT(run.starts[index], 6u);
        }
    }
}

TEST_F(SkiaVerticalParagraph, TC07_AdjacentGroupsAndEffectsStayInsideCells) {
    auto style = PrimaryStyle(40);
    SkPaint background;
    background.setColor(SK_ColorGREEN);
    style.setBackgroundPaint(background);
    style.setDecoration(skia_text::TextDecoration::kUnderline);
    style.addShadow(skia_text::TextShadow(SK_ColorBLUE, {4, 6}, 2));
    auto paragraph = BuildVerticalTateChuYoko("12天34", {{0, 2}, {5, 7}}, style);
    paragraph->layoutWithInlineExtent(400);
    const auto runs = TateChuYokoRuns(SnapshotRuns(paragraph.get()));
    ASSERT_EQ(runs.size(), 2u);
    EXPECT_EQ(runs[0].starts, (std::vector<uint32_t>{0, 1, 2}));
    EXPECT_EQ(runs[1].starts, (std::vector<uint32_t>{5, 6, 7}));
    EXPECT_EQ(TightBoxes(paragraph.get(), 0, 2).size(), 1u);
    EXPECT_EQ(TightBoxes(paragraph.get(), 3, 5).size(), 1u);

    const auto bitmap = RasterizeText(paragraph.get(), 0, 0, 120, 220);
    const auto green = PixelsMatching(bitmap, [](SkColor color) {
        return SkColorGetG(color) > 120 && SkColorGetR(color) < 100 && SkColorGetB(color) < 100;
    });
    EXPECT_GT(green, 40u);
    EXPECT_GT(NonWhitePixels(bitmap), 80u);
}

TEST_F(SkiaVerticalParagraph, TC07_UnderlineRemainsContinuousThroughTateChuYoko) {
    auto style = DecorationStyleFor(PrimaryStyle(40), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto paragraph = BuildVerticalTateChuYoko("天12地", {{3, 5}}, style);
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

    const auto decorations = RecordDecorations(paragraph.get());
    ASSERT_EQ(decorations.size(), 1u);
    EXPECT_NEAR(decorations.front().rect.top(), 0, kTolerance);
    EXPECT_NEAR(decorations.front().rect.bottom(),
                paragraph->getLayoutMetrics().longestInlineExtent,
                kTolerance);
    EXPECT_LT(decorations.front().rect.centerX(),
              paragraph->getLayoutMetrics().columns[0].physicalBaseline);
}

TEST_F(SkiaVerticalParagraph, TC08_RangeVisitorPathAndHitTestShareCellGeometry) {
    auto paragraph = BuildVerticalTateChuYoko("天12地", {{3, 5}});
    paragraph->layoutWithInlineExtent(400);
    const auto boxes = TightBoxes(paragraph.get(), 1, 3);
    ASSERT_EQ(boxes.size(), 1u);
    ASSERT_TRUE(paragraph->getRectsForPlaceholders().empty());

    const auto runs = TateChuYokoRuns(SnapshotRuns(paragraph.get()));
    ASSERT_EQ(runs.size(), 1u);
    EXPECT_EQ(runs[0].starts, (std::vector<uint32_t>{3, 4, 5}));
    EXPECT_EQ(runs[0].glyphs.size(), 2u);

    const auto cell = boxes[0].rect;
    auto firstHalf = paragraph->getGlyphPositionAtCoordinate(cell.centerX(), cell.top() + 5);
    auto secondHalf = paragraph->getGlyphPositionAtCoordinate(cell.centerX(), cell.bottom() - 5);
    EXPECT_EQ(firstHalf.position, 1u);
    EXPECT_EQ(secondHalf.position, 3u);

    auto onlyGroup = BuildVerticalTateChuYoko("12", {{0, 2}});
    onlyGroup->layoutWithInlineExtent(200);
    const auto groupCell = TightBoxes(onlyGroup.get(), 0, 2);
    ASSERT_EQ(groupCell.size(), 1u);
    SkPath path;
    EXPECT_EQ(onlyGroup->getPath(0, &path), 0);
    EXPECT_FALSE(path.isEmpty());
    const auto bounds = path.getBounds();
    EXPECT_GE(bounds.left(), groupCell[0].rect.left() - 1);
    EXPECT_LE(bounds.right(), groupCell[0].rect.right() + 1);
    EXPECT_GE(bounds.top(), groupCell[0].rect.top() - 1);
    EXPECT_LE(bounds.bottom(), groupCell[0].rect.bottom() + 1);
}

TEST_F(SkiaVerticalParagraph, TC11_StrictValidationAndHorizontalNoOp) {
    auto expectRejected = [](const std::string& text,
                             std::vector<skia_text::TextRange> ranges,
                             const std::function<void(skia_text::ParagraphBuilder*)>& extra = {}) {
        EXPECT_THROW({
            auto paragraph = BuildVerticalTateChuYoko(text, std::move(ranges), PrimaryStyle(40), {}, extra);
            paragraph->layoutWithInlineExtent(400);
        }, std::invalid_argument);
    };

    expectRejected("12", {{1, 1}});
    expectRejected("12", {{0, 3}});
    expectRejected("😀", {{0, 2}});
    expectRejected("12", {{0, 2}, {1, 2}});
    expectRejected("12\n3", {{0, 3}});
    expectRejected("\xE2\x80\x8E", {{0, 3}});
    expectRejected("12", {{0, 2}}, [](auto* builder) {
        builder->addRuby({{0, 1}, "a"});
    });
    expectRejected("12", {{0, 2}}, [](auto* builder) {
        builder->addPlaceholder({20, 20, skia_text::PlaceholderAlignment::kMiddle,
                                 skia_text::TextBaseline::kAlphabetic, 0});
        builder->addTateChuYoko({0, 5});
    });

    auto crossStyle = BuildCustomParagraph({}, [](auto* builder) {
        builder->pushStyle(PrimaryStyle(40));
        builder->addText("1", 1);
        builder->pushStyle(PrimaryStyle(20));
        builder->addText("2", 1);
        builder->addTateChuYoko({0, 2});
    });
    EXPECT_THROW(crossStyle->layoutWithInlineExtent(400), std::invalid_argument);

    auto plain = BuildCoreParagraph("12天地", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    auto annotated = BuildVerticalTateChuYoko("12天地", {{0, 2}}, PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    plain->layout(400);
    annotated->layout(400);
    ExpectSnapshotsNear(Capture(plain.get()), Capture(annotated.get()));
}

TEST_F(SkiaVerticalParagraph, TC13_LayoutFailuresResizeAndCacheIsolation) {
    auto paragraph = BuildVerticalTateChuYoko("天ABC地", {{3, 6}});
    paragraph->layoutWithInlineExtent(400);
    const auto first = TateChuYokoRuns(SnapshotRuns(paragraph.get()));
    ASSERT_EQ(first.size(), 1u);
    const SkScalar firstSqueeze = first[0].transform.getScaleX();
    EXPECT_THROW(paragraph->layoutWithInlineExtent(-1), std::invalid_argument);
    paragraph->layoutWithInlineExtent(400);
    const auto restored = TateChuYokoRuns(SnapshotRuns(paragraph.get()));
    ASSERT_EQ(restored.size(), 1u);
    EXPECT_NEAR(restored[0].transform.getScaleX(), firstSqueeze, 0.001);

    paragraph->layoutWithInlineExtent(50);
    EXPECT_FALSE(TateChuYokoRuns(SnapshotRuns(paragraph.get())).empty());
    paragraph->markDirty();
    paragraph->layoutWithInlineExtent(400);
    EXPECT_FALSE(TateChuYokoRuns(SnapshotRuns(paragraph.get())).empty());

    auto* cache = TestFontCollection()->getParagraphCache();
    ASSERT_NE(cache, nullptr);
    cache->reset();
    cache->turnOn(true);
    auto cachedAnnotated = BuildVerticalTateChuYoko("天12", {{3, 5}});
    auto cachedPlain = BuildCoreParagraph("天12", PrimaryStyle(40));
    auto cachedGrow = BuildVerticalTateChuYokoModes("天12",
            {{{3, 5}, skia_text::TateChuYokoSizing::kGrow}});
    cachedAnnotated->layoutWithInlineExtent(400);
    cachedPlain->layoutWithInlineExtent(400);
    cachedGrow->layoutWithInlineExtent(400);
    EXPECT_EQ(TateChuYokoRuns(SnapshotRuns(cachedAnnotated.get())).size(), 1u);
    EXPECT_TRUE(TateChuYokoRuns(SnapshotRuns(cachedPlain.get())).empty());
    const auto cachedSqueezeBoxes = TightBoxes(cachedAnnotated.get(), 1, 3);
    const auto cachedGrowBoxes = TightBoxes(cachedGrow.get(), 1, 3);
    ASSERT_EQ(cachedSqueezeBoxes.size(), 1u);
    ASSERT_EQ(cachedGrowBoxes.size(), 1u);
    EXPECT_NEAR(cachedSqueezeBoxes[0].rect.width(), 40, kTolerance);
    EXPECT_GT(cachedGrowBoxes[0].rect.width(), cachedSqueezeBoxes[0].rect.width() + 1);
    cache->reset();
    cache->turnOn(false);
}

TEST_F(SkiaVerticalParagraph, TC14_ForegroundAndFontSizeUpdatesReachGroupGlyphs) {
    auto paragraph = BuildVerticalTateChuYoko("天12", {{3, 5}});
    paragraph->layoutWithInlineExtent(200);
    SkPaint red;
    red.setColor(SK_ColorRED);
    paragraph->updateForegroundPaint(0, 5, red);
    const auto bitmap = RasterizeText(paragraph.get(), 0, 0, 100, 160);
    const auto cell = TightBoxes(paragraph.get(), 1, 3)[0].rect;
    const auto redPixels = PixelsMatching(bitmap, [&](SkColor color) {
        return IsRedLike(color);
    });
    EXPECT_GT(redPixels, 10u);
    EXPECT_GT(NonWhitePixels(bitmap, &cell), 20u);

    const auto before = TightBoxes(paragraph.get(), 1, 3)[0].rect.width();
    paragraph->updateFontSize(0, 5, 80);
    paragraph->layoutWithInlineExtent(400);
    const auto runs = TateChuYokoRuns(SnapshotRuns(paragraph.get()));
    ASSERT_EQ(runs.size(), 1u);
    EXPECT_FLOAT_EQ(runs[0].font.getSize(), 80);
    EXPECT_NEAR(runs[0].advance.height(), 80, kTolerance);
    EXPECT_GT(TightBoxes(paragraph.get(), 1, 3)[0].rect.width(), before);
}

TEST_F(SkiaVerticalParagraph, P01_P05_PhysicalSizeAdvanceAndSymmetricColumnGrowth) {
    auto plain = BuildCoreParagraph("天地", PrimaryStyle(40));
    auto paragraph = BuildCoreParagraph("天", PrimaryStyle(40), {}, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(90, 25));
        builder->addText("地");
    });
    plain->layoutWithInlineExtent(200);
    paragraph->layoutWithInlineExtent(200);
    const auto boxes = paragraph->getRectsForPlaceholders();
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_FLOAT_EQ(boxes[0].rect.width(), 90);
    EXPECT_FLOAT_EQ(boxes[0].rect.height(), 25);
    const auto& column = paragraph->getLayoutMetrics().columns[0];
    EXPECT_NEAR(column.inlineExtent, plain->getLayoutMetrics().columns[0].inlineExtent + 25, 0.01);
    EXPECT_FLOAT_EQ(column.blockThickness, 90);
    EXPECT_NEAR(boxes[0].rect.centerX(), column.physicalBounds.centerX(), 0.01);
    EXPECT_NEAR(column.physicalBaseline - plain->getLayoutMetrics().columns[0].physicalBaseline,
                (90 - plain->getLayoutMetrics().physicalSize.width()) / 2, 0.01);
    EXPECT_EQ(paragraph->getActualTextRange(0, true).end, 9u);
}

TEST_F(SkiaVerticalParagraph, P02_P03_MultipleAtomicPlaceholdersWrapBeforeAndAfter) {
    auto paragraph = BuildCoreParagraph("", PrimaryStyle(40), {}, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(20, 30));
        builder->addPlaceholder(MiddlePlaceholder(35, 40));
        builder->addPlaceholder(MiddlePlaceholder(10, 50));
        builder->addText("天");
    });
    paragraph->layoutWithInlineExtent(80);
    const auto boxes = paragraph->getRectsForPlaceholders();
    ASSERT_EQ(boxes.size(), 3u);
    EXPECT_FLOAT_EQ(boxes[0].rect.top(), 0);
    EXPECT_FLOAT_EQ(boxes[1].rect.top(), 30);
    EXPECT_NEAR(boxes[0].rect.centerX(), boxes[1].rect.centerX(), 0.01);
    EXPECT_FLOAT_EQ(boxes[2].rect.top(), 0);
    EXPECT_LT(boxes[2].rect.centerX(), boxes[1].rect.centerX());
    EXPECT_EQ(paragraph->getLayoutMetrics().columns.size(), 3u);
    EXPECT_FLOAT_EQ(paragraph->getLayoutMetrics().columns[0].inlineExtent, 70);
    EXPECT_FLOAT_EQ(paragraph->getLayoutMetrics().columns[0].blockThickness, 35);
}

TEST_F(SkiaVerticalParagraph, P04_TallPlaceholderOwnsColumnAndOverflowsContentBounds) {
    auto paragraph = BuildCoreParagraph("天", PrimaryStyle(40), {}, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(25, 150));
        builder->addText("地");
    });
    paragraph->layoutWithInlineExtent(80);
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 3u);
    EXPECT_EQ(metrics.columns[1].utf16Start, 1u);
    EXPECT_EQ(metrics.columns[1].utf16End, 2u);
    EXPECT_FLOAT_EQ(metrics.columns[1].blockThickness, 25);
    EXPECT_FLOAT_EQ(metrics.physicalSize.height(), 80);
    EXPECT_FLOAT_EQ(metrics.contentBounds.bottom(), 150);
    EXPECT_FLOAT_EQ(paragraph->getRectsForPlaceholders()[0].rect.height(), 150);
}

TEST_F(SkiaVerticalParagraph, P06_P07_EllipsisHidesSourcePlaceholderAndSentinel) {
    BuildOptions options;
    options.maxLines = 1;
    options.ellipsis = "…";
    auto paragraph = BuildCoreParagraph("天", PrimaryStyle(40), options, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(90, 50));
        builder->addText("地玄");
    });
    paragraph->layoutWithInlineExtent(80);
    EXPECT_TRUE(paragraph->didExceedMaxLines());
    EXPECT_TRUE(paragraph->getRectsForPlaceholders().empty());
    EXPECT_TRUE(paragraph->getRectsForRange(1, 2, skia_text::RectHeightStyle::kTight,
                                           skia_text::RectWidthStyle::kTight).empty());
    EXPECT_EQ(static_cast<skia_text::ParagraphImpl*>(paragraph.get())->text().size(), 12u);
    auto empty = BuildCoreParagraph("", PrimaryStyle(40));
    empty->layoutWithInlineExtent(80);
    EXPECT_TRUE(empty->getRectsForPlaceholders().empty());
}

TEST_F(SkiaVerticalParagraph, P07_P08_RangeClusterAndCoordinateUseExactPlaceholderBox) {
    auto paragraph = BuildCoreParagraph("天", PrimaryStyle(40), {}, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(12, 25));
        builder->addText("地");
    });
    paragraph->layoutWithInlineExtent(200);
    const auto placeholders = paragraph->getRectsForPlaceholders();
    const auto range = paragraph->getRectsForRange(1, 2, skia_text::RectHeightStyle::kTight,
                                                   skia_text::RectWidthStyle::kTight);
    ASSERT_EQ(placeholders.size(), 1u);
    ASSERT_EQ(range.size(), 1u);
    ExpectRectNear(range[0].rect, placeholders[0].rect);
    skia_text::Paragraph::GlyphInfo info;
    ASSERT_TRUE(paragraph->getGlyphInfoAtUTF16Offset(1, &info));
    ExpectRectNear(info.fGraphemeLayoutBounds, range[0].rect);
    const auto hit = paragraph->getGlyphPositionAtCoordinate(range[0].rect.centerX(), range[0].rect.centerY());
    EXPECT_TRUE(hit.position == 1 || hit.position == 2);
}

TEST_F(SkiaVerticalParagraph, P06_ObjectOnlyOverflowEllipsisHidesPlaceholder) {
    BuildOptions options;
    options.maxLines = 1;
    options.ellipsis = "…";
    auto paragraph = BuildCoreParagraph("", PrimaryStyle(40), options, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(90, 150));
    });
    paragraph->layoutWithInlineExtent(80);
    EXPECT_TRUE(paragraph->didExceedMaxLines());
    EXPECT_TRUE(paragraph->getRectsForPlaceholders().empty());
    EXPECT_TRUE(paragraph->getRectsForRange(0, 1, skia_text::RectHeightStyle::kTight,
                                           skia_text::RectWidthStyle::kTight).empty());
    RecordingPainter painter;
    paragraph->paint(&painter, 0, 0);
    EXPECT_GT(painter.textBlobCount, 0);
}

TEST_F(SkiaVerticalParagraph, P01_P10_ObjectOnlyStyleDoesNotPaintOrContributeTextMetrics) {
    auto style = PrimaryStyle(300);
    style.setDecoration(skia_text::TextDecoration::kUnderline);
    style.setBaselineShift(70);
    style.setHeightOverride(true);
    style.setHeight(4);
    SkPaint background;
    background.setColor(SK_ColorRED);
    style.setBackgroundPaint(background);
    style.addShadow(skia_text::TextShadow(SK_ColorBLUE, {5, 8}, 3));
    auto paragraph = BuildCoreParagraph("", style, {}, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(20, 25));
    });
    paragraph->layoutWithInlineExtent(80);
    EXPECT_FLOAT_EQ(paragraph->getLayoutMetrics().physicalSize.width(), 20);
    ASSERT_EQ(paragraph->getRectsForPlaceholders().size(), 1u);
    ExpectRectNear(paragraph->getRectsForPlaceholders()[0].rect, SkRect::MakeWH(20, 25));
    RecordingPainter painter;
    paragraph->paint(&painter, 0, 0);
    EXPECT_EQ(painter.textBlobCount, 0);
    EXPECT_TRUE(painter.backgrounds.empty());
    EXPECT_TRUE(painter.decorations.empty());
    EXPECT_TRUE(painter.shadows.empty());
}

TEST_F(SkiaVerticalParagraph, P10_IndentJustifyLineHeightAndBaselineShiftLeavePlaceholderFixed) {
    auto style = PrimaryStyle(40);
    style.setHeightOverride(true);
    style.setHeight(1.5f);
    style.setBaselineShift(-10);
    style.setLetterSpacing(4);
    auto paragraph = BuildConfiguredParagraph("天", style, [](auto& config) {
        config.setTextIndent(10, false);
        config.setTextAlign(skia_text::TextAlign::kJustify);
    }, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(100, 25));
        builder->addText("地玄黃宇宙");
    });
    paragraph->layoutWithInlineExtent(100);
    const auto boxes = paragraph->getRectsForPlaceholders();
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_FLOAT_EQ(boxes[0].rect.height(), 25);
    EXPECT_NEAR(boxes[0].rect.width(), 100, 0.01);
    EXPECT_NEAR(boxes[0].rect.centerX(), paragraph->getLayoutMetrics().columns[0].physicalBounds.centerX(), 0.01);
    EXPECT_GE(boxes[0].rect.top(), 10);
    EXPECT_GE(paragraph->getLayoutMetrics().columns[0].blockThickness, 100);
}

TEST_F(SkiaVerticalParagraph, P12_UnsupportedAlignmentBaselineOffsetAndInvalidDimensionsReject) {
    for (auto value : {0, 1, 2, 3, 4}) {
        auto paragraph = BuildCoreParagraph("", PrimaryStyle(40), {}, [value](auto* builder) {
            auto placeholder = MiddlePlaceholder(20, 30);
            placeholder.fAlignment = static_cast<skia_text::PlaceholderAlignment>(value);
            builder->addPlaceholder(placeholder);
        });
        EXPECT_THROW(paragraph->layoutWithInlineExtent(100), std::invalid_argument);
    }
    for (bool baseline : {false, true}) {
        auto paragraph = BuildCoreParagraph("", PrimaryStyle(40), {}, [baseline](auto* builder) {
            auto placeholder = MiddlePlaceholder(20, 30);
            if (baseline) placeholder.fBaseline = skia_text::TextBaseline::kIdeographic;
            else placeholder.fBaselineOffset = 1;
            builder->addPlaceholder(placeholder);
        });
        EXPECT_THROW(paragraph->layoutWithInlineExtent(100), std::invalid_argument);
    }
    for (float value : {0.f, -1.f, std::numeric_limits<float>::infinity(),
                        std::numeric_limits<float>::quiet_NaN()}) {
        EXPECT_THROW(BuildCoreParagraph("", PrimaryStyle(40), {}, [value](auto* builder) {
            builder->addPlaceholder(MiddlePlaceholder(value, 30));
        }), std::invalid_argument);
        EXPECT_THROW(BuildCoreParagraph("", PrimaryStyle(40), {}, [value](auto* builder) {
            builder->addPlaceholder(MiddlePlaceholder(20, value));
        }), std::invalid_argument);
    }
}

TEST_F(SkiaVerticalParagraph, P14_DirtyRelayoutFailureRecoveryAndCacheIsolation) {
    auto paragraph = BuildCoreParagraph("天", PrimaryStyle(40), {}, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(90, 50));
        builder->addText("地");
    });
    paragraph->layoutWithInlineExtent(200);
    const auto first = paragraph->getRectsForPlaceholders();
    paragraph->layoutWithInlineExtent(60);
    EXPECT_NE(paragraph->getRectsForPlaceholders()[0].rect, first[0].rect);
    paragraph->markDirty();
    paragraph->layoutWithInlineExtent(200);
    ExpectRectNear(paragraph->getRectsForPlaceholders()[0].rect, first[0].rect);
    EXPECT_THROW(paragraph->layoutWithInlineExtent(-1), std::invalid_argument);
    EXPECT_THROW(paragraph->getRectsForPlaceholders(), std::logic_error);
    paragraph->layoutWithInlineExtent(200);
    ExpectRectNear(paragraph->getRectsForPlaceholders()[0].rect, first[0].rect);
}

TEST_F(SkiaVerticalParagraph, P15_HorizontalSmokeRetainsPhysicalPlaceholderDimensions) {
    BuildOptions options;
    options.writingMode = skia_text::WritingMode::kHorizontalTb;
    auto paragraph = BuildCoreParagraph("天", PrimaryStyle(40), options, [](auto* builder) {
        builder->addPlaceholder(MiddlePlaceholder(23, 31));
        builder->addText("地");
    });
    paragraph->layout(200);
    const auto boxes = paragraph->getRectsForPlaceholders();
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_NEAR(boxes[0].rect.width(), 23, 0.01);
    EXPECT_NEAR(boxes[0].rect.height(), 31, 0.01);
}

TEST_F(SkiaVerticalParagraph, V01_UprightCjkUsesTtbGlyphsAndRightmostFirstColumn) {
    const std::string text = "天地玄黃";
    auto paragraph = BuildParagraph(text, PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(300);

    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.writingMode, skia_text::WritingMode::kVerticalRl);
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_EQ(paragraph->lineNumber(), 1u);
    EXPECT_FALSE(paragraph->didExceedMaxLines());
    EXPECT_NEAR(metrics.physicalSize.height(), 300, kTolerance);
    EXPECT_LT(metrics.physicalSize.width(), 100);
    EXPECT_NEAR(metrics.columns[0].physicalBounds.right(), metrics.physicalSize.width(), kTolerance);
    EXPECT_GE(metrics.columns[0].physicalBounds.left(), 0);
    ExpectContiguousSourceRanges(metrics, Utf16Length(text));

    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_FALSE(runs.empty());
    size_t glyphCount = 0;
    for (const auto& run: runs) {
        EXPECT_TRUE(IsIdentity(run.transform));
        EXPECT_FALSE(run.sideways);
        glyphCount += run.glyphs.size();
        for (size_t index = 1; index < run.positions.size(); ++index) {
            EXPECT_GE(run.positions[index].y(), run.positions[index - 1].y() - kTolerance);
        }
    }
    EXPECT_EQ(glyphCount, 4u);
    EXPECT_FALSE(metrics.inkBounds.isEmpty());
    EXPECT_TRUE(metrics.contentBounds.contains(metrics.inkBounds) || metrics.inkBounds.contains(metrics.contentBounds));
}

TEST_F(SkiaVerticalParagraph, V02_PunctuationUsesVerticalSubstitutionWithoutExtraRotation) {
    const std::string text = "「漢字」、。ー";
    auto vertical = BuildParagraph(text, PrimaryStyle(), {});
    vertical->layoutWithInlineExtent(400);
    auto horizontal = BuildParagraph(text, PrimaryStyle(), {.writingMode = skia_text::WritingMode::kHorizontalTb});
    horizontal->layout(1000);

    const auto vruns = SnapshotRuns(vertical.get());
    for (const auto& run: vruns) {
        EXPECT_TRUE(IsIdentity(run.transform));
    }
    std::vector<SkGlyphID> verticalGlyphs;
    for (const auto& run: vruns) {
        verticalGlyphs.insert(verticalGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    const auto hruns = SnapshotRuns(horizontal.get());
    std::vector<SkGlyphID> horizontalGlyphs;
    for (const auto& run: hruns) {
        horizontalGlyphs.insert(horizontalGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    ASSERT_EQ(verticalGlyphs.size(), horizontalGlyphs.size());
    EXPECT_NE(verticalGlyphs, horizontalGlyphs);
    const auto boxes = TightBoxes(vertical.get(), 0, Utf16Length(text));
    ASSERT_FALSE(boxes.empty());
    EXPECT_GT(boxes.front().rect.height(), 0);
}

TEST_F(SkiaVerticalParagraph, V03_MixedLatinAndDigitsFormClockwiseSidewaysRuns) {
    const std::string text = "漢字ABC2026かな";
    auto paragraph = BuildParagraph(text, PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(400);

    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_GE(runs.size(), 3u);
    int sidewaysRuns = 0;
    int uprightRuns = 0;
    for (const auto& run: runs) {
        bool containsAscii = false;
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            const uint32_t start = run.starts[index];
            if (start < text.size() && std::isalnum(static_cast<unsigned char>(text[start]))) {
                containsAscii = true;
            }
        }
        if (containsAscii) {
            ++sidewaysRuns;
            EXPECT_TRUE(IsClockwise(run.transform))
                    << "line=" << run.line << " starts=" << run.starts.front() << ".." << run.starts.back()
                    << " matrix=" << run.transform.getScaleX() << "," << run.transform.getSkewX() << ","
                    << run.transform.getSkewY() << "," << run.transform.getScaleY();
            for (size_t index = 1; index < run.positions.size(); ++index) {
                EXPECT_GE(run.positions[index].x(), run.positions[index - 1].x() - kTolerance);
            }
            for (size_t index = 0; index < run.glyphs.size(); ++index) {
                const SkPoint physical = run.origin + run.positions[index];
                EXPECT_GE(physical.y(), run.origin.y() - kTolerance);
            }
        } else {
            ++uprightRuns;
            EXPECT_TRUE(IsIdentity(run.transform));
        }
    }
    EXPECT_GT(sidewaysRuns, 0);
    EXPECT_GT(uprightRuns, 0);
}

TEST_F(SkiaVerticalParagraph, V04_SidewaysShapingPreservesKerningAndLigatures) {
    const std::string text = "AVATAR office ffi";
    auto horizontal = BuildParagraph(text,
                                     LatinStyle(),
                                     {.writingMode = skia_text::WritingMode::kHorizontalTb, .fallback = false});
    horizontal->layout(2000);
    auto vertical = BuildParagraph(text, LatinStyle(), {.fallback = false});
    vertical->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

    const auto runs = SnapshotRuns(vertical.get());
    ASSERT_FALSE(runs.empty());
    for (const auto& run: runs) {
        EXPECT_TRUE(IsClockwise(run.transform));
    }
    EXPECT_NEAR(vertical->getLayoutMetrics().longestInlineExtent,
                horizontal->getLongestLine(),
                // HarfBuzz fixed-point advances round independently from the
                // horizontal SkShaper path; one 1/64 px unit is expected.
                0.2f);
    EXPECT_EQ(CountGlyphs(runs), CountGlyphs(SnapshotRuns(horizontal.get())));

    auto av = BuildParagraph("AV",
                             LatinStyle(),
                             {.writingMode = skia_text::WritingMode::kHorizontalTb, .fallback = false});
    av->layout(1000);
    auto a = BuildParagraph("A",
                            LatinStyle(),
                            {.writingMode = skia_text::WritingMode::kHorizontalTb, .fallback = false});
    a->layout(1000);
    auto v = BuildParagraph("V",
                            LatinStyle(),
                            {.writingMode = skia_text::WritingMode::kHorizontalTb, .fallback = false});
    v->layout(1000);
    EXPECT_LT(av->getLongestLine(), a->getLongestLine() + v->getLongestLine() - 0.1f);
}

TEST_F(SkiaVerticalParagraph, V05_TransformedRotatedCharactersStayUprightWithoutSubstitution) {
    const std::string text = "\u3008\u3009\u3030\u30A0\u30FC";
    for (SkUnichar codepoint: {0x3008, 0x3009, 0x3030, 0x30A0, 0x30FC}) {
        EXPECT_EQ(u_getIntPropertyValue(codepoint, UCHAR_VERTICAL_ORIENTATION), U_VO_TRANSFORMED_ROTATED);
    }

    auto substituted = BuildParagraph(text, PrimaryStyle(), {});
    substituted->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    auto horizontal = BuildParagraph(text, PrimaryStyle(), {.writingMode = skia_text::WritingMode::kHorizontalTb});
    horizontal->layout(2000);

    const auto substitutedRuns = SnapshotRuns(substituted.get());
    ASSERT_FALSE(substitutedRuns.empty());
    for (const auto& run: substitutedRuns) {
        EXPECT_TRUE(IsIdentity(run.transform));
    }
    std::vector<SkGlyphID> substitutedGlyphs;
    std::vector<SkGlyphID> horizontalGlyphs;
    for (const auto& run: substitutedRuns) {
        substitutedGlyphs.insert(substitutedGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    for (const auto& run: SnapshotRuns(horizontal.get())) {
        horizontalGlyphs.insert(horizontalGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    EXPECT_EQ(substitutedGlyphs.size(), horizontalGlyphs.size());
    EXPECT_NE(substitutedGlyphs, horizontalGlyphs);

    auto noSubstitutionStyle = PrimaryStyle();
    noSubstitutionStyle.addFontFeature(SkString("vert"), 0);
    noSubstitutionStyle.addFontFeature(SkString("vrt2"), 0);
    auto noSubstitution = BuildParagraph(text, noSubstitutionStyle, {});
    noSubstitution->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto noSubstitutionRuns = SnapshotRuns(noSubstitution.get());
    ASSERT_FALSE(noSubstitutionRuns.empty());
    for (const auto& run: noSubstitutionRuns) {
        EXPECT_TRUE(IsIdentity(run.transform));
    }
    std::vector<SkGlyphID> noSubstitutionGlyphs;
    for (const auto& run: noSubstitutionRuns) {
        noSubstitutionGlyphs.insert(noSubstitutionGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    EXPECT_EQ(noSubstitutionGlyphs.size(), substitutedGlyphs.size());
    EXPECT_NE(noSubstitutionGlyphs, substitutedGlyphs);
}

TEST_F(SkiaVerticalParagraph, V06_FallbackSynthesizedMetricsAndMixedColumnThickness) {
    auto latinTypeface = SkFontMgr_New_Custom_Empty()->makeFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH);
    ASSERT_NE(latinTypeface, nullptr);
    EXPECT_FALSE(latinTypeface->copyTableData(SkSetFourByteTag('v', 'h', 'e', 'a')));
    EXPECT_FALSE(latinTypeface->copyTableData(SkSetFourByteTag('v', 'm', 't', 'x')));
    EXPECT_EQ(u_getIntPropertyValue(0x00A7, UCHAR_VERTICAL_ORIENTATION), U_VO_UPRIGHT);

    const std::string uprightWithoutVerticalTables = "\u00A7";
    auto synthesized = BuildParagraph(uprightWithoutVerticalTables, LatinStyle(), {});
    synthesized->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto synthesizedRuns = SnapshotRuns(synthesized.get());
    ASSERT_EQ(synthesizedRuns.size(), 1u);
    EXPECT_TRUE(IsIdentity(synthesizedRuns.front().transform));
    EXPECT_GT(synthesized->getLayoutMetrics().longestInlineExtent, 0);
    EXPECT_TRUE(MetricsAreFinite(synthesized->getLayoutMetrics()));

    const std::string mixed = "漢§字";
    auto paragraph = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        auto primary40 = PrimaryStyle(40);
        builder->pushStyle(primary40);
        builder->addText(mixed.data(), 3);
        builder->pop();
        auto latin64 = LatinStyle(64);
        builder->pushStyle(latin64);
        builder->addText(mixed.data() + 3, 2);
        builder->pop();
        auto primary80 = PrimaryStyle(80);
        builder->pushStyle(primary80);
        builder->addText(mixed.data() + 5, 3);
        builder->pop();
    });
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 3u);
    EXPECT_NEAR(runs[0].font.getSize(), 40, kTolerance);
    EXPECT_NEAR(runs[1].font.getSize(), 64, kTolerance);
    EXPECT_NEAR(runs[2].font.getSize(), 80, kTolerance);
    EXPECT_NE(runs[0].font.getTypeface()->uniqueID(), runs[1].font.getTypeface()->uniqueID());
    ASSERT_EQ(paragraph->getLayoutMetrics().columns.size(), 1u);
    EXPECT_GE(paragraph->getLayoutMetrics().columns[0].blockThickness, 64);
    EXPECT_EQ(paragraph->unresolvedGlyphs(), 0);

    auto fallback = BuildParagraph("漢§", LatinStyle(), {});
    fallback->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    EXPECT_EQ(fallback->unresolvedGlyphs(), 0);
    const auto fonts = fallback->getFonts();
    ASSERT_EQ(fonts.size(), 2u);
    EXPECT_NE(fonts[0].fFont.getTypeface()->uniqueID(), fonts[1].fFont.getTypeface()->uniqueID());

    auto missing = BuildParagraph("\u0378", FamilyStyle("Absent Test Family", 48), {.fallback = false});
    missing->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    EXPECT_GT(missing->unresolvedGlyphs(), 0);
    const auto missingRuns = SnapshotRuns(missing.get());
    ASSERT_FALSE(missingRuns.empty());
    EXPECT_TRUE(std::all_of(missingRuns.front().glyphs.begin(), missingRuns.front().glyphs.end(), [](SkGlyphID glyph) {
        return glyph == 0;
    }));
}

TEST_F(SkiaVerticalParagraph, V06b_MixedOrientationUsesTheCentralBaseline) {
    auto paragraph = BuildParagraph("漢GPT字", PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto runs = SnapshotRuns(paragraph.get());

    SkScalar uprightCenter = 0;
    ASSERT_FALSE(runs.empty());
    for (const auto& run: runs) {
        ASSERT_FALSE(run.glyphs.empty());
        if (!run.sideways) {
            if (uprightCenter == 0) {
                uprightCenter = GlyphInkBounds(run, 0).centerX();
            }
        }
    }
    ASSERT_GT(uprightCenter, 0);

    for (const auto& run: runs) {
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            EXPECT_NEAR(GlyphInkBounds(run, index).centerX(), uprightCenter, 4.0f)
                    << "line=" << run.line << " sideways=" << run.sideways << " glyph=" << run.glyphs[index];
        }
    }

    ASSERT_EQ(paragraph->getLayoutMetrics().columns.size(), 1u);
    EXPECT_NEAR(paragraph->getLayoutMetrics().columns[0].blockThickness, 48, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V06c_CrossFontMixedOrientationUsesTheCentralBaseline) {
    auto paragraph = BuildCustomParagraph({}, [](skia_text::ParagraphBuilder* builder) {
        auto primary = PrimaryStyle(48);
        builder->pushStyle(primary);
        builder->addText("漢", 3);
        builder->pop();
        auto latin = LatinStyle(48);
        builder->pushStyle(latin);
        builder->addText("GPT", 3);
        builder->pop();
        builder->pushStyle(primary);
        builder->addText("字", 3);
        builder->pop();
    });
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 3u);
    EXPECT_EQ(runs[0].font.getTypeface()->uniqueID() == runs[1].font.getTypeface()->uniqueID(), false);
    const SkScalar uprightCenter = GlyphInkBounds(runs[0], 0).centerX();
    for (const auto& run: runs) {
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            EXPECT_NEAR(GlyphInkBounds(run, index).centerX(), uprightCenter, 4.0f)
                    << "line=" << run.line << " sideways=" << run.sideways << " glyph=" << run.glyphs[index];
        }
    }
    ASSERT_EQ(paragraph->getLayoutMetrics().columns.size(), 1u);
    EXPECT_NEAR(paragraph->getLayoutMetrics().columns[0].blockThickness, 48, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V07_VariationsFeatureRangesAndCacheIsolation) {
    auto variationStyle = [](int weight) {
        auto style = PrimaryStyle();
        SkFontArguments::VariationPosition::Coordinate coordinate{SkSetFourByteTag('w', 'g', 'h', 't'),
                                                                  static_cast<SkScalar>(weight)};
        SkFontArguments arguments;
        arguments.setVariationDesignPosition({&coordinate, 1});
        style.setFontArguments(arguments);
        return style;
    };

    auto light = BuildParagraph("漢字", variationStyle(200), {});
    light->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    auto bold = BuildParagraph("漢字", variationStyle(900), {});
    bold->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto lightRuns = SnapshotRuns(light.get());
    const auto boldRuns = SnapshotRuns(bold.get());
    ASSERT_EQ(lightRuns.size(), 1u);
    ASSERT_EQ(boldRuns.size(), 1u);
    EXPECT_NE(lightRuns[0].font.getTypeface()->uniqueID(), boldRuns[0].font.getTypeface()->uniqueID());

    const auto lightSnapshot = Capture(light.get());
    const auto boldSnapshot = Capture(bold.get());
    light->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    bold->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    ExpectSnapshotsNear(lightSnapshot, Capture(light.get()));
    ExpectSnapshotsNear(boldSnapshot, Capture(bold.get()));

    auto kernedStyle = LatinStyle();
    auto unkernedStyle = LatinStyle();
    unkernedStyle.addFontFeature(SkString("kern"), 0);
    auto kerned = BuildParagraph("AV", kernedStyle, {});
    kerned->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    auto unkerned = BuildParagraph("AV", unkernedStyle, {});
    unkerned->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    EXPECT_LT(kerned->getLayoutMetrics().longestInlineExtent, unkerned->getLayoutMetrics().longestInlineExtent - 0.1f);

    const std::string transformed = "「漢字」、。ー";
    auto horizontal =
            BuildParagraph(transformed, PrimaryStyle(), {.writingMode = skia_text::WritingMode::kHorizontalTb});
    horizontal->layout(1000);
    std::vector<SkGlyphID> horizontalGlyphs;
    for (const auto& run: SnapshotRuns(horizontal.get())) {
        horizontalGlyphs.insert(horizontalGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    ASSERT_EQ(horizontalGlyphs.size(), Utf16Length(transformed));
    auto enabled = BuildParagraph(transformed, PrimaryStyle(), {});
    enabled->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    std::vector<SkGlyphID> enabledGlyphs;
    for (const auto& run: SnapshotRuns(enabled.get())) {
        enabledGlyphs.insert(enabledGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    EXPECT_NE(enabledGlyphs, horizontalGlyphs);
    auto disabledStyle = PrimaryStyle();
    disabledStyle.addFontFeature(SkString("vert"), 0);
    disabledStyle.addFontFeature(SkString("vrt2"), 0);
    auto disabled = BuildParagraph(transformed, disabledStyle, {});
    disabled->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    std::vector<SkGlyphID> disabledGlyphs;
    for (const auto& run: SnapshotRuns(disabled.get())) {
        disabledGlyphs.insert(disabledGlyphs.end(), run.glyphs.begin(), run.glyphs.end());
    }
    EXPECT_EQ(disabledGlyphs, horizontalGlyphs);
}

TEST_F(SkiaVerticalParagraph, V08_GraphemeSequencesRemainAtomicAcrossOrientationAndFallback) {
    const std::string enclosing = "A\u0301\u20DD";
    auto enclosingParagraph = BuildParagraph(enclosing, LatinStyle(), {});
    enclosingParagraph->layoutWithInlineExtent(1000);
    const auto enclosingRuns = SnapshotRuns(enclosingParagraph.get());
    ASSERT_EQ(enclosingRuns.size(), 1u);
    EXPECT_TRUE(IsIdentity(enclosingRuns.front().transform));
    skia_text::Paragraph::GlyphInfo enclosingInfo;
    ASSERT_TRUE(enclosingParagraph->getGlyphInfoAtUTF16Offset(0, &enclosingInfo));
    EXPECT_EQ(enclosingInfo.fGraphemeClusterTextRange.start, 0u);
    EXPECT_EQ(enclosingInfo.fGraphemeClusterTextRange.end, 3u);

    const std::string variation = "漢\uFE00";
    auto variationParagraph = BuildParagraph(variation, PrimaryStyle(), {});
    variationParagraph->layoutWithInlineExtent(1000);
    skia_text::Paragraph::GlyphInfo variationInfo;
    ASSERT_TRUE(variationParagraph->getGlyphInfoAtUTF16Offset(0, &variationInfo));
    EXPECT_EQ(variationInfo.fGraphemeClusterTextRange.start, 0u);
    EXPECT_EQ(variationInfo.fGraphemeClusterTextRange.end, 2u);
    EXPECT_FALSE(variationInfo.fGraphemeLayoutBounds.isEmpty());

    const std::string family = "\U0001F469\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";
    auto emoji = BuildParagraph(family, PrimaryStyle(), {});
    emoji->layoutWithInlineExtent(1000);
    ASSERT_EQ(emoji->getLayoutMetrics().columns.size(), 1u);
    skia_text::Paragraph::GlyphInfo emojiInfo;
    ASSERT_TRUE(emoji->getGlyphInfoAtUTF16Offset(0, &emojiInfo));
    EXPECT_EQ(emojiInfo.fGraphemeClusterTextRange.start, 0u);
    EXPECT_EQ(emojiInfo.fGraphemeClusterTextRange.end, Utf16Length(family));
    const auto boxes = TightBoxes(emoji.get(), 0, Utf16Length(family));
    ASSERT_EQ(boxes.size(), 1u);
    const auto partialBoxes = TightBoxes(emoji.get(), 0, 1);
    ASSERT_EQ(partialBoxes.size(), 1u);
    EXPECT_EQ(partialBoxes.front().rect, boxes.front().rect);
}

TEST_F(SkiaVerticalParagraph, V09_FiniteHeightBreaksColumnsAtTheExactBoundary) {
    auto one = BuildParagraph("天", PrimaryStyle(), {});
    one->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = one->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    auto paragraph = BuildParagraph("天地玄黃", PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(advance * 2);
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    EXPECT_NEAR(metrics.columns[0].inlineExtent, advance * 2, 0.2f);
    EXPECT_NEAR(metrics.columns[1].inlineExtent, advance * 2, 0.2f);
    EXPECT_EQ(metrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(metrics.columns[0].utf16End, 2u);
    EXPECT_EQ(metrics.columns[1].utf16Start, 2u);
    EXPECT_EQ(metrics.columns[1].utf16End, 4u);
    EXPECT_NEAR(metrics.columns[0].physicalBounds.right(), metrics.physicalSize.width(), kTolerance);
    EXPECT_NEAR(metrics.columns[1].physicalBounds.right(), metrics.columns[0].physicalBounds.left(), kTolerance);
    EXPECT_LE(metrics.columns[1].physicalBounds.left(), metrics.columns[0].physicalBounds.left());
    ExpectContiguousSourceRanges(metrics, 4);

    auto narrow = BuildParagraph("天地玄黃", PrimaryStyle(), {});
    narrow->layoutWithInlineExtent(advance * 1.5f);
    ASSERT_EQ(narrow->getLayoutMetrics().columns.size(), 4u);
    for (const auto& column: narrow->getLayoutMetrics().columns) {
        EXPECT_NEAR(column.inlineExtent, advance, 0.2f);
    }
}

TEST_F(SkiaVerticalParagraph, V10_LongSidewaysRunsWrapOnlyAtBreakOpportunities) {
    skia_text::TextStyle c03Style = LatinStyle(20);
    c03Style.setHeight(2.0f);
    c03Style.setHeightOverride(true);

    const std::string unbreakableText = "abcdefghijklmnopqrstuvwxyz";
    auto unbreakable = BuildParagraph(unbreakableText, c03Style, {});
    unbreakable->layoutWithInlineExtent(60);
    const auto& unbreakableMetrics = unbreakable->getLayoutMetrics();
    ASSERT_EQ(unbreakableMetrics.columns.size(), 1u);
    EXPECT_EQ(unbreakableMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(unbreakableMetrics.columns[0].utf16End, Utf16Length(unbreakableText));
    EXPECT_GT(unbreakableMetrics.columns[0].inlineExtent, 60 + kTolerance);
    EXPECT_NEAR(unbreakableMetrics.columns[0].blockThickness, 40, kTolerance);
    EXPECT_NEAR(unbreakableMetrics.physicalSize.width(), 40, kTolerance);
    EXPECT_NEAR(unbreakableMetrics.physicalSize.height(), 60, kTolerance);
    EXPECT_GT(unbreakableMetrics.contentBounds.height(),
              unbreakableMetrics.physicalSize.height());
    EXPECT_NEAR(unbreakableMetrics.longestInlineExtent,
                unbreakableMetrics.columns[0].inlineExtent,
                kTolerance);
    EXPECT_TRUE(MetricsAreFinite(unbreakableMetrics));
    ExpectContiguousSourceRanges(unbreakableMetrics, Utf16Length(unbreakableText));

    const std::string wordsText = "alpha beta";
    auto words = BuildParagraph(wordsText, c03Style, {});
    words->layoutWithInlineExtent(60);
    const auto& wordMetrics = words->getLayoutMetrics();
    ASSERT_EQ(wordMetrics.columns.size(), 2u);
    EXPECT_LE(wordMetrics.longestInlineExtent, 60 + kTolerance);
    for (const auto& column: wordMetrics.columns) {
        EXPECT_GT(column.utf16End, column.utf16Start);
        if (column.utf16End < Utf16Length(wordsText)) {
            EXPECT_EQ(wordsText[column.utf16End - 1], ' ')
                    << "columns must end at a legal whitespace break";
        }
    }
    EXPECT_TRUE(MetricsAreFinite(wordMetrics));
    ExpectContiguousSourceRanges(wordMetrics, Utf16Length(wordsText));

    auto atom = BuildParagraph("天", PrimaryStyle(), {});
    atom->layoutWithInlineExtent(1);
    const auto& atomMetrics = atom->getLayoutMetrics();
    ASSERT_EQ(atomMetrics.columns.size(), 1u);
    EXPECT_GT(atomMetrics.longestInlineExtent, 1);
    EXPECT_GT(atomMetrics.contentBounds.height(), atomMetrics.physicalSize.height());
    EXPECT_NEAR(atomMetrics.physicalSize.height(), 1, kTolerance);
    EXPECT_FALSE(atom->didExceedMaxLines());
    ExpectContiguousSourceRanges(atomMetrics, 1);
}

TEST_F(SkiaVerticalParagraph, V11_ZeroTinyAndInfiniteHeightsTerminateWithFiniteGeometry) {
    for (SkScalar height: {SkScalar(0), std::numeric_limits<SkScalar>::denorm_min(), SkScalar(0.5f)}) {
        SCOPED_TRACE(height);
        auto paragraph = BuildParagraph("天地玄黃", PrimaryStyle(), {});
        paragraph->layoutWithInlineExtent(height);
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 4u);
        EXPECT_TRUE(MetricsAreFinite(metrics));
        EXPECT_NEAR(metrics.physicalSize.height(), height, kTolerance);
        for (const auto& column: metrics.columns) {
            EXPECT_GT(column.inlineExtent, 0);
            EXPECT_FALSE(column.physicalBounds.isEmpty());
        }
        ExpectContiguousSourceRanges(metrics, 4);
    }

    auto infinite = BuildParagraph("漢\n字", PrimaryStyle(), {});
    infinite->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& infiniteMetrics = infinite->getLayoutMetrics();
    ASSERT_EQ(infiniteMetrics.columns.size(), 2u);
    EXPECT_TRUE(MetricsAreFinite(infiniteMetrics));
    EXPECT_EQ(infiniteMetrics.columns[0].utf16End, 2u);
    EXPECT_EQ(infiniteMetrics.columns[1].utf16Start, 2u);
    EXPECT_GT(infiniteMetrics.minIntrinsicInlineExtent, 0);
    EXPECT_GE(infiniteMetrics.maxIntrinsicInlineExtent, infiniteMetrics.longestInlineExtent - kTolerance);

    auto noBreak = BuildParagraph("漢字", PrimaryStyle(), {});
    noBreak->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    ASSERT_EQ(noBreak->getLayoutMetrics().columns.size(), 1u);
    EXPECT_TRUE(MetricsAreFinite(noBreak->getLayoutMetrics()));
}

TEST_F(SkiaVerticalParagraph, V12_InvalidConstraintsRejectAndRecoverWithoutPartialResults) {
    auto paragraph = BuildParagraph("漢字ABC", PrimaryStyle(), {});
    for (SkScalar invalid:
         {SkScalar(-1), -std::numeric_limits<SkScalar>::infinity(), std::numeric_limits<SkScalar>::quiet_NaN()}) {
        EXPECT_THROW(paragraph->layoutWithInlineExtent(invalid), std::invalid_argument);
        EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
        EXPECT_THROW(paragraph->getRectsForRange(0,
                                                 1,
                                                 skia_text::RectHeightStyle::kTight,
                                                 skia_text::RectWidthStyle::kTight),
                     std::logic_error);
    }
    paragraph->layoutWithInlineExtent(300);
    EXPECT_TRUE(MetricsAreFinite(paragraph->getLayoutMetrics()));
    EXPECT_EQ(paragraph->getLayoutMetrics().columns.size(), 1u);
    const auto inlineSnapshot = Capture(paragraph.get());
    EXPECT_NO_THROW(paragraph->layout(300));
    ExpectSnapshotsNear(inlineSnapshot, Capture(paragraph.get()));

    for (SkScalar invalid:
         {SkScalar(-1), -std::numeric_limits<SkScalar>::infinity(), std::numeric_limits<SkScalar>::quiet_NaN()}) {
        EXPECT_THROW(paragraph->layout(invalid), std::invalid_argument);
        EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
        EXPECT_EQ(paragraph->getMaxWidth(), 0);
        EXPECT_EQ(paragraph->getHeight(), 0);
        EXPECT_EQ(paragraph->getLongestLine(), 0);
        EXPECT_FALSE(paragraph->didExceedMaxLines());
    }
    EXPECT_NO_THROW(paragraph->layout(300));
    EXPECT_TRUE(MetricsAreFinite(paragraph->getLayoutMetrics()));

    auto invalidSize = BuildParagraph("漢字", PrimaryStyle(0), {});
    EXPECT_THROW(invalidSize->layoutWithInlineExtent(300), std::invalid_argument);
    EXPECT_THROW(invalidSize->getLayoutMetrics(), std::logic_error);
    invalidSize->updateFontSize(0, 6, 48);
    invalidSize->layoutWithInlineExtent(300);
    EXPECT_TRUE(MetricsAreFinite(invalidSize->getLayoutMetrics()));

    auto horizontal = BuildParagraph("AB", LatinStyle(), {.writingMode = skia_text::WritingMode::kHorizontalTb});
    horizontal->layoutWithInlineExtent(120);
    EXPECT_NEAR(horizontal->getMaxWidth(), 120, kTolerance);
    EXPECT_GT(horizontal->getHeight(), 0);
}

TEST_F(SkiaVerticalParagraph, V13_EmptyAndHardBreakSourcesPreserveColumnsAndRanges) {
    auto empty = BuildParagraph("", PrimaryStyle(), {});
    empty->layoutWithInlineExtent(200);
    const auto& emptyMetrics = empty->getLayoutMetrics();
    ASSERT_EQ(emptyMetrics.columns.size(), 1u);
    EXPECT_EQ(emptyMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(emptyMetrics.columns[0].utf16End, 0u);
    EXPECT_EQ(emptyMetrics.columns[0].inlineExtent, 0);
    EXPECT_TRUE(empty->getRectsForRange(0, 1, skia_text::RectHeightStyle::kTight, skia_text::RectWidthStyle::kTight)
                        .empty());

    auto newline = BuildParagraph("\n", PrimaryStyle(), {});
    newline->layoutWithInlineExtent(200);
    const auto& newlineMetrics = newline->getLayoutMetrics();
    ASSERT_EQ(newlineMetrics.columns.size(), 2u);
    EXPECT_EQ(newlineMetrics.columns[0].utf8Range.start, 0u);
    EXPECT_EQ(newlineMetrics.columns[0].utf8Range.end, 1u);
    EXPECT_TRUE(newlineMetrics.columns[0].hardBreak);
    EXPECT_EQ(newlineMetrics.columns[1].utf16Start, 1u);
    EXPECT_EQ(newlineMetrics.columns[1].utf16End, 1u);
    ExpectContiguousSourceRanges(newlineMetrics, 1);

    auto consecutive = BuildParagraph("A\n\nB", PrimaryStyle(), {});
    consecutive->layoutWithInlineExtent(1000);
    const auto& consecutiveMetrics = consecutive->getLayoutMetrics();
    ASSERT_EQ(consecutiveMetrics.columns.size(), 3u);
    EXPECT_EQ(consecutiveMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(consecutiveMetrics.columns[0].utf16End, 2u);
    EXPECT_TRUE(consecutiveMetrics.columns[0].hardBreak);
    EXPECT_EQ(consecutiveMetrics.columns[1].utf16Start, 2u);
    EXPECT_EQ(consecutiveMetrics.columns[1].utf16End, 3u);
    EXPECT_TRUE(consecutiveMetrics.columns[1].hardBreak);
    EXPECT_EQ(consecutiveMetrics.columns[2].utf16Start, 3u);
    EXPECT_EQ(consecutiveMetrics.columns[2].utf16End, 4u);
    EXPECT_FALSE(consecutiveMetrics.columns[2].hardBreak);
    ExpectContiguousSourceRanges(consecutiveMetrics, 4);

    auto crlf = BuildParagraph("A\r\nB", PrimaryStyle(), {});
    crlf->layoutWithInlineExtent(1000);
    const auto& crlfMetrics = crlf->getLayoutMetrics();
    ASSERT_EQ(crlfMetrics.columns.size(), 2u);
    EXPECT_EQ(crlfMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(crlfMetrics.columns[0].utf16End, 3u);
    EXPECT_TRUE(crlfMetrics.columns[0].hardBreak);
    EXPECT_EQ(crlfMetrics.columns[1].utf16Start, 3u);
    EXPECT_EQ(crlfMetrics.columns[1].utf16End, 4u);
    ExpectContiguousSourceRanges(crlfMetrics, 4);

    auto trailing = BuildParagraph("A\n", PrimaryStyle(), {});
    trailing->layoutWithInlineExtent(1000);
    const auto& trailingMetrics = trailing->getLayoutMetrics();
    ASSERT_EQ(trailingMetrics.columns.size(), 2u);
    EXPECT_TRUE(trailingMetrics.columns[0].hardBreak);
    EXPECT_EQ(trailingMetrics.columns[1].utf16Start, 2u);
    EXPECT_EQ(trailingMetrics.columns[1].utf16End, 2u);
    EXPECT_TRUE(trailing->getRectsForRange(1, 2, skia_text::RectHeightStyle::kTight, skia_text::RectWidthStyle::kTight)
                        .empty());
}

TEST_F(SkiaVerticalParagraph, V13b_HardBreakClustersDoNotProduceVisibleGlyphs) {
    const std::string plainText = "漢";
    auto plain = BuildParagraph(plainText, PrimaryStyle(), {});
    plain->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& plainMetrics = plain->getLayoutMetrics();
    ASSERT_EQ(plainMetrics.columns.size(), 1u);
    ASSERT_GT(plainMetrics.columns[0].inlineExtent, 0);

    SkPath plainPath;
    ASSERT_EQ(plain->getPath(0, &plainPath), 0);
    const SkRect plainBounds = plainPath.getBounds();
    ASSERT_GT(plainBounds.height(), 0);

    for (const std::string breakText: {std::string("漢\n字"), std::string("漢\r\n字")}) {
        SCOPED_TRACE(breakText);
        auto paragraph = BuildParagraph(breakText, PrimaryStyle(), {});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 2u);
        EXPECT_TRUE(metrics.columns[0].hardBreak);
        EXPECT_NEAR(metrics.columns[0].inlineExtent, plainMetrics.columns[0].inlineExtent, kTolerance);

        const auto runs = SnapshotRuns(paragraph.get());
        ASSERT_FALSE(runs.empty());
        for (const auto& run: runs) {
            ASSERT_FALSE(run.starts.empty());
            // extendedVisit appends the run-end sentinel after the glyph starts.
            for (size_t index = 0; index + 1 < run.starts.size(); ++index) {
                const uint32_t start = run.starts[index];
                ASSERT_LT(start, breakText.size());
                EXPECT_NE(breakText[start], '\n') << "glyph start=" << start;
                EXPECT_NE(breakText[start], '\r') << "glyph start=" << start;
            }
        }

        SkPath breakPath;
        ASSERT_EQ(paragraph->getPath(0, &breakPath), 0);
        const SkRect breakBounds = breakPath.getBounds();
        EXPECT_NEAR(breakBounds.width(), plainBounds.width(), kTolerance);
        EXPECT_NEAR(breakBounds.height(), plainBounds.height(), kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, V13c_BidiNeutralPunctuationIsNotRejectedAsStrongRtl) {
    auto paragraph = BuildParagraph(std::string("\xD8\x8C") + "漢", PrimaryStyle(), {});
    EXPECT_NO_THROW(paragraph->layoutWithInlineExtent(300));
    EXPECT_FALSE(paragraph->getLayoutMetrics().columns.empty());

    auto strongRtl = BuildParagraph("\xD9\x85\xD8\xB1\xD8\xAD\xD8\xA8\xD8\xA7", PrimaryStyle(), {});
    EXPECT_THROW(strongRtl->layoutWithInlineExtent(300), std::invalid_argument);
}

TEST_F(SkiaVerticalParagraph, V13d_LatinHardBreaksKeepPrecedingGlyphsAndColumnMetrics) {
    const SkScalar infinity = std::numeric_limits<SkScalar>::infinity();

    auto plain = BuildParagraph("ABC", LatinStyle(), {});
    plain->layoutWithInlineExtent(infinity);
    const auto& plainMetrics = plain->getLayoutMetrics();
    ASSERT_EQ(plainMetrics.columns.size(), 1u);
    ASSERT_GT(plainMetrics.columns[0].inlineExtent, 0);
    EXPECT_EQ(CountGlyphs(SnapshotRuns(plain.get())), 3u);

    SkPath plainPath;
    ASSERT_EQ(plain->getPath(0, &plainPath), 0);
    const SkRect plainBounds = plainPath.getBounds();
    ASSERT_GT(plainBounds.height(), 0);

    for (const std::string breakText: {std::string("ABC\n"), std::string("ABC\r\n")}) {
        SCOPED_TRACE(breakText);
        auto paragraph = BuildParagraph(breakText, LatinStyle(), {});
        paragraph->layoutWithInlineExtent(infinity);
        const auto& metrics = paragraph->getLayoutMetrics();

        ASSERT_EQ(metrics.columns.size(), 2u);
        EXPECT_TRUE(metrics.columns[0].hardBreak);
        EXPECT_NEAR(metrics.columns[0].inlineExtent, plainMetrics.columns[0].inlineExtent, kTolerance);
        EXPECT_NEAR(metrics.columns[0].blockThickness, plainMetrics.columns[0].blockThickness, kTolerance);
        EXPECT_NEAR(metrics.physicalSize.height(), plainMetrics.physicalSize.height(), kTolerance);
        EXPECT_NEAR(metrics.inkBounds.width(), plainMetrics.inkBounds.width(), kTolerance);
        EXPECT_NEAR(metrics.inkBounds.height(), plainMetrics.inkBounds.height(), kTolerance);
        EXPECT_EQ(metrics.columns[1].inlineExtent, 0);
        // A trailing hard break intentionally leaves an empty final column;
        // only the visible first column must match the plain text.
        EXPECT_GE(metrics.physicalSize.width(), plainMetrics.physicalSize.width());
        EXPECT_EQ(CountGlyphs(SnapshotRuns(paragraph.get())), 3u);

        SkPath breakPath;
        ASSERT_EQ(paragraph->getPath(0, &breakPath), 0);
        const SkRect breakBounds = breakPath.getBounds();
        EXPECT_NEAR(breakBounds.width(), plainBounds.width(), kTolerance);
        EXPECT_NEAR(breakBounds.height(), plainBounds.height(), kTolerance);
    }

    auto first = BuildParagraph("AB", LatinStyle(), {});
    auto second = BuildParagraph("CD", LatinStyle(), {});
    auto twoColumns = BuildParagraph("AB\nCD", LatinStyle(), {});
    first->layoutWithInlineExtent(infinity);
    second->layoutWithInlineExtent(infinity);
    twoColumns->layoutWithInlineExtent(infinity);

    const auto& firstMetrics = first->getLayoutMetrics();
    const auto& secondMetrics = second->getLayoutMetrics();
    const auto& twoMetrics = twoColumns->getLayoutMetrics();
    ASSERT_EQ(firstMetrics.columns.size(), 1u);
    ASSERT_EQ(secondMetrics.columns.size(), 1u);
    ASSERT_EQ(twoMetrics.columns.size(), 2u);
    EXPECT_TRUE(twoMetrics.columns[0].hardBreak);
    EXPECT_FALSE(twoMetrics.columns[1].hardBreak);
    EXPECT_NEAR(twoMetrics.columns[0].inlineExtent, firstMetrics.columns[0].inlineExtent, kTolerance);
    EXPECT_NEAR(twoMetrics.columns[0].blockThickness, firstMetrics.columns[0].blockThickness, kTolerance);
    EXPECT_NEAR(twoMetrics.columns[1].inlineExtent, secondMetrics.columns[0].inlineExtent, kTolerance);
    EXPECT_NEAR(twoMetrics.columns[1].blockThickness, secondMetrics.columns[0].blockThickness, kTolerance);
    EXPECT_NEAR(twoMetrics.physicalSize.width(),
                firstMetrics.physicalSize.width() + secondMetrics.physicalSize.width(),
                kTolerance);
    EXPECT_EQ(CountGlyphs(SnapshotRuns(twoColumns.get())), 4u);

    SkPath firstPath;
    SkPath secondPath;
    ASSERT_EQ(twoColumns->getPath(0, &firstPath), 0);
    ASSERT_EQ(twoColumns->getPath(1, &secondPath), 0);
    const SkRect firstBounds = firstPath.getBounds();
    const SkRect secondBounds = secondPath.getBounds();
    SkPath firstPlainPath;
    SkPath secondPlainPath;
    ASSERT_EQ(first->getPath(0, &firstPlainPath), 0);
    ASSERT_EQ(second->getPath(0, &secondPlainPath), 0);
    const SkRect firstPlainBounds = firstPlainPath.getBounds();
    const SkRect secondPlainBounds = secondPlainPath.getBounds();
    EXPECT_NEAR(firstBounds.width(), firstPlainBounds.width(), kTolerance);
    EXPECT_NEAR(firstBounds.height(), firstPlainBounds.height(), kTolerance);
    EXPECT_NEAR(secondBounds.width(), secondPlainBounds.width(), kTolerance);
    EXPECT_NEAR(secondBounds.height(), secondPlainBounds.height(), kTolerance);

    auto consecutive = BuildParagraph("ABC\n\n", LatinStyle(), {});
    consecutive->layoutWithInlineExtent(infinity);
    const auto& consecutiveMetrics = consecutive->getLayoutMetrics();
    ASSERT_EQ(consecutiveMetrics.columns.size(), 3u);
    EXPECT_TRUE(consecutiveMetrics.columns[0].hardBreak);
    EXPECT_TRUE(consecutiveMetrics.columns[1].hardBreak);
    EXPECT_NEAR(consecutiveMetrics.columns[0].inlineExtent,
                plainMetrics.columns[0].inlineExtent,
                kTolerance);
    EXPECT_EQ(CountGlyphs(SnapshotRuns(consecutive.get())), 3u);

    auto leading = BuildParagraph("\nABC", LatinStyle(), {});
    leading->layoutWithInlineExtent(infinity);
    const auto& leadingMetrics = leading->getLayoutMetrics();
    ASSERT_EQ(leadingMetrics.columns.size(), 2u);
    EXPECT_TRUE(leadingMetrics.columns[0].hardBreak);
    EXPECT_EQ(leadingMetrics.columns[0].inlineExtent, 0);
    EXPECT_NEAR(leadingMetrics.columns[1].inlineExtent, plainMetrics.columns[0].inlineExtent, kTolerance);
    EXPECT_EQ(CountGlyphs(SnapshotRuns(leading.get())), 3u);
}

TEST_F(SkiaVerticalParagraph, V13e_TrailingHardBreakKeepsEmptyColumnWithFixedExtent) {
    skia_text::TextStyle fixedStyle = LatinStyle(20);
    fixedStyle.setHeight(2.0f);
    fixedStyle.setHeightOverride(true);

    auto paragraph = BuildParagraph(std::string("A").append(1, static_cast<char>(10)), fixedStyle, {});
    paragraph->layoutWithInlineExtent(60);
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    EXPECT_TRUE(metrics.columns[0].hardBreak);
    EXPECT_EQ(metrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(metrics.columns[0].utf16End, 2u);
    EXPECT_FALSE(metrics.columns[1].hardBreak);
    EXPECT_EQ(metrics.columns[1].utf16Start, 2u);
    EXPECT_EQ(metrics.columns[1].utf16End, 2u);
    EXPECT_EQ(metrics.columns[1].inlineExtent, 0);
    EXPECT_NEAR(metrics.columns[1].blockThickness, 40, kTolerance);
    EXPECT_GT(metrics.columns[1].physicalBounds.width(), 0);
    EXPECT_NEAR(metrics.physicalSize.width(), 80, kTolerance);
    EXPECT_NEAR(metrics.physicalSize.height(), 60, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));
    ExpectContiguousSourceRanges(metrics, 2);
}

TEST_F(SkiaVerticalParagraph, V14_WhitespaceAdvancesRangesAndOnlyWhitespaceStayObservable) {
    const std::string padded = " A ";
    auto paragraph = BuildParagraph(padded, PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(1000);
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_GT(metrics.columns[0].inlineExtent, 0);
    EXPECT_EQ(metrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(metrics.columns[0].utf16End, 3u);
    EXPECT_EQ(metrics.columns[0].utf16EndExcludingWhitespace, 2u);
    EXPECT_EQ(metrics.columns[0].utf16EndIncludingNewline, 3u);
    const auto boxes = TightBoxes(paragraph.get(), 0, 3);
    ASSERT_FALSE(boxes.empty());
    EXPECT_GT(boxes.front().rect.height(), 0);

    const std::string fullWidth = "漢　字";
    auto fullWidthParagraph = BuildParagraph(fullWidth, PrimaryStyle(), {});
    fullWidthParagraph->layoutWithInlineExtent(1000);
    EXPECT_GT(fullWidthParagraph->getLayoutMetrics().longestInlineExtent, 0);
    EXPECT_FALSE(TightBoxes(fullWidthParagraph.get(), 1, 2).empty());

    auto spaces = BuildParagraph("   ", LatinStyle(), {});
    spaces->layoutWithInlineExtent(1000);
    const auto& spaceMetrics = spaces->getLayoutMetrics();
    ASSERT_EQ(spaceMetrics.columns.size(), 1u);
    EXPECT_GT(spaceMetrics.longestInlineExtent, 0);
    EXPECT_EQ(spaceMetrics.columns[0].utf16EndExcludingWhitespace, 0u);
    EXPECT_EQ(spaceMetrics.columns[0].utf16End, 3u);
    EXPECT_TRUE(MetricsAreFinite(spaceMetrics));
}

TEST_F(SkiaVerticalParagraph, V15_AlignmentAndSpacingActAlongTheInlineAxisWithoutNegativeShift) {
    auto one = BuildParagraph("天", PrimaryStyle(), {});
    one->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar extent = one->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(extent, 0);

    auto start = BuildParagraph("天", PrimaryStyle(), {.align = skia_text::TextAlign::kStart});
    start->layoutWithInlineExtent(300);
    auto end = BuildParagraph("天", PrimaryStyle(), {.align = skia_text::TextAlign::kEnd});
    end->layoutWithInlineExtent(300);
    auto center = BuildParagraph("天", PrimaryStyle(), {.align = skia_text::TextAlign::kCenter});
    center->layoutWithInlineExtent(300);
    EXPECT_NEAR(start->getLayoutMetrics().columns[0].physicalBounds.top(), 0, kTolerance);
    EXPECT_NEAR(end->getLayoutMetrics().columns[0].physicalBounds.top(), 300 - extent, 0.2f);
    EXPECT_NEAR(center->getLayoutMetrics().columns[0].physicalBounds.top(), (300 - extent) * 0.5f, 0.2f);

    for (auto align: {skia_text::TextAlign::kStart, skia_text::TextAlign::kEnd, skia_text::TextAlign::kCenter}) {
        auto overflow = BuildParagraph("天", PrimaryStyle(), {.align = align});
        overflow->layoutWithInlineExtent(extent * 0.5f);
        EXPECT_NEAR(overflow->getLayoutMetrics().columns[0].physicalBounds.top(), 0, kTolerance);
    }

    const std::string text = "天地玄黃";
    auto spacedStyle = PrimaryStyle();
    spacedStyle.setLetterSpacing(10);
    auto spaced = BuildParagraph(text, spacedStyle, {});
    spaced->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    auto horizontal = BuildParagraph(text, spacedStyle, {.writingMode = skia_text::WritingMode::kHorizontalTb});
    horizontal->layout(2000);
    EXPECT_NEAR(spaced->getLayoutMetrics().longestInlineExtent, horizontal->getLongestLine(), 0.2f);
    const auto spacedRuns = SnapshotRuns(spaced.get());
    ASSERT_FALSE(spacedRuns.empty());
    ASSERT_GE(spacedRuns.front().positions.size(), 3u);
    EXPECT_GT(spacedRuns.front().positions[1].y() - spacedRuns.front().positions[0].y(), 48);

    auto wordSpacedStyle = LatinStyle();
    wordSpacedStyle.setWordSpacing(20);
    auto wordSpaced = BuildParagraph("A B", wordSpacedStyle, {});
    wordSpaced->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    auto wordHorizontal =
            BuildParagraph("A B", wordSpacedStyle, {.writingMode = skia_text::WritingMode::kHorizontalTb});
    wordHorizontal->layout(1000);
    EXPECT_NEAR(wordSpaced->getLayoutMetrics().longestInlineExtent, wordHorizontal->getLongestLine(), 0.3f);
}

TEST_F(SkiaVerticalParagraph, V16_MaxLinesCountsVisibleColumnsAndHidesQueries) {
    auto one = BuildParagraph("天", PrimaryStyle(), {});
    one->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = one->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    auto truncated = BuildParagraph("天地玄黃", PrimaryStyle(), {.maxLines = 2});
    truncated->layoutWithInlineExtent(advance);
    const auto& metrics = truncated->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    EXPECT_TRUE(truncated->didExceedMaxLines());
    EXPECT_EQ(truncated->lineNumber(), 2u);
    EXPECT_EQ(metrics.columns[0].utf16End, 1u);
    EXPECT_EQ(metrics.columns[1].utf16End, 2u);
    EXPECT_TRUE(TightBoxes(truncated.get(), 2, 4).empty());
    EXPECT_EQ(truncated->getLineNumberAtUTF16Offset(0), 0);
    EXPECT_EQ(truncated->getLineNumberAtUTF16Offset(2), -1);
    EXPECT_TRUE(truncated->getActualTextRange(2, false).empty());
    EXPECT_TRUE(truncated->getGlyphClusterAt(0, nullptr));
    skia_text::Paragraph::GlyphClusterInfo cluster;
    EXPECT_FALSE(truncated->getGlyphClusterAt(6, &cluster));
    skia_text::Paragraph::GlyphInfo hiddenInfo;
    EXPECT_FALSE(truncated->getGlyphInfoAtUTF16Offset(2, &hiddenInfo));

    auto exactlyFits = BuildParagraph("天地", PrimaryStyle(), {.maxLines = 2});
    exactlyFits->layoutWithInlineExtent(advance);
    ASSERT_EQ(exactlyFits->getLayoutMetrics().columns.size(), 2u);
    EXPECT_FALSE(exactlyFits->didExceedMaxLines());

    auto oneColumn = BuildParagraph("天", PrimaryStyle(), {.maxLines = 1});
    oneColumn->layoutWithInlineExtent(1000);
    ASSERT_EQ(oneColumn->getLayoutMetrics().columns.size(), 1u);
    EXPECT_FALSE(oneColumn->didExceedMaxLines());

    auto trailing = BuildParagraph("天\n", PrimaryStyle(), {.maxLines = 1});
    trailing->layoutWithInlineExtent(1000);
    ASSERT_EQ(trailing->getLayoutMetrics().columns.size(), 1u);
    EXPECT_TRUE(trailing->didExceedMaxLines());
}

TEST_F(SkiaVerticalParagraph, V17_Utf16RangesExpandInsideGraphemesAndSpanColumns) {
    auto one = BuildParagraph("天", PrimaryStyle(), {});
    one->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = one->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    const std::string columnsText = "天地玄黃";
    auto columns = BuildParagraph(columnsText, PrimaryStyle(), {});
    columns->layoutWithInlineExtent(advance * 2);
    ASSERT_EQ(columns->getLayoutMetrics().columns.size(), 2u);
    const auto boxes = TightBoxes(columns.get(), 0, 4);
    ASSERT_EQ(boxes.size(), 2u);
    EXPECT_GT(boxes[0].rect.centerX(), boxes[1].rect.centerX());
    EXPECT_NEAR(boxes[0].rect.left(), columns->getLayoutMetrics().columns[0].physicalBounds.left(), kTolerance);
    EXPECT_NEAR(boxes[1].rect.left(), columns->getLayoutMetrics().columns[1].physicalBounds.left(), kTolerance);

    const std::string emojiText = "A\U0001F600B";
    auto emoji = BuildParagraph(emojiText, PrimaryStyle(), {});
    emoji->layoutWithInlineExtent(1000);
    const auto fullEmoji = TightBoxes(emoji.get(), 1, 3);
    ASSERT_EQ(fullEmoji.size(), 1u);
    const auto halfEmojiStart = TightBoxes(emoji.get(), 1, 2);
    ASSERT_EQ(halfEmojiStart.size(), 1u);
    EXPECT_EQ(halfEmojiStart.front().rect, fullEmoji.front().rect);
    const auto halfEmojiEnd = TightBoxes(emoji.get(), 2, 3);
    ASSERT_EQ(halfEmojiEnd.size(), 1u);
    EXPECT_EQ(halfEmojiEnd.front().rect, fullEmoji.front().rect);
    skia_text::Paragraph::GlyphInfo emojiInfo;
    ASSERT_TRUE(emoji->getGlyphInfoAtUTF16Offset(0, &emojiInfo));
    EXPECT_EQ(emojiInfo.fGraphemeClusterTextRange.start, 0u);
    ASSERT_TRUE(emoji->getGlyphInfoAtUTF16Offset(1, &emojiInfo));
    EXPECT_EQ(emojiInfo.fGraphemeClusterTextRange.start, 1u);
    EXPECT_EQ(emojiInfo.fGraphemeClusterTextRange.end, 3u);

    const std::string composed = "A\u0301B";
    auto composedParagraph = BuildParagraph(composed, LatinStyle(), {});
    composedParagraph->layoutWithInlineExtent(1000);
    const auto composedBoxes = TightBoxes(composedParagraph.get(), 0, 1);
    ASSERT_EQ(composedBoxes.size(), 1u);
    const auto fullComposedBoxes = TightBoxes(composedParagraph.get(), 0, 2);
    ASSERT_EQ(fullComposedBoxes.size(), 1u);
    EXPECT_EQ(composedBoxes.front().rect, fullComposedBoxes.front().rect);

    const auto all = TightBoxes(emoji.get(), 0, 100);
    const auto clamped = TightBoxes(emoji.get(), 0, Utf16Length(emojiText));
    ASSERT_EQ(all.size(), clamped.size());
    for (size_t index = 0; index < all.size(); ++index) {
        EXPECT_EQ(all[index].rect, clamped[index].rect);
    }
    EXPECT_TRUE(TightBoxes(emoji.get(), 0, 0).empty());
    EXPECT_TRUE(TightBoxes(emoji.get(), 99, 100).empty());
    EXPECT_THROW(emoji->getRectsForRange(0,
                                         Utf16Length(emojiText),
                                         skia_text::RectHeightStyle::kMax,
                                         skia_text::RectWidthStyle::kTight),
                 std::logic_error);
}

TEST_F(SkiaVerticalParagraph, V18_HitTestingSelectsNearestColumnAndLegalSourceBoundaries) {
    auto one = BuildParagraph("天", PrimaryStyle(), {});
    one->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = one->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    auto columns = BuildParagraph("天地玄黃", PrimaryStyle(), {});
    columns->layoutWithInlineExtent(advance * 2);
    const auto& metrics = columns->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    const SkScalar rightX = metrics.columns[0].physicalBounds.centerX();
    const SkScalar leftX = metrics.columns[1].physicalBounds.centerX();
    EXPECT_EQ(columns->getGlyphPositionAtCoordinate(rightX, 0).position, 0);
    EXPECT_EQ(columns->getGlyphPositionAtCoordinate(leftX, 0).position, 2);
    EXPECT_EQ(columns->getGlyphPositionAtCoordinate(rightX, -100).position, 0);
    EXPECT_EQ(columns->getGlyphPositionAtCoordinate(rightX, metrics.columns[0].inlineExtent + 100).position, 2);
    EXPECT_EQ(columns->getGlyphPositionAtCoordinate(metrics.physicalSize.width() + 100, 0).position, 0);
    EXPECT_EQ(columns->getGlyphPositionAtCoordinate(-100, 0).position, 2);
    EXPECT_EQ(columns->getGlyphPositionAtCoordinate(metrics.columns[0].physicalBounds.left(), 0).position, 0);

    const auto quarter = columns->getGlyphPositionAtCoordinate(rightX, advance * 0.25f);
    EXPECT_EQ(quarter.position, 0);
    EXPECT_EQ(quarter.affinity, skia_text::Affinity::kUpstream);
    const auto threeQuarters = columns->getGlyphPositionAtCoordinate(rightX, advance * 0.75f);
    EXPECT_EQ(threeQuarters.position, 1);
    EXPECT_EQ(threeQuarters.affinity, skia_text::Affinity::kDownstream);

    auto emptyColumn = BuildParagraph("A\n", PrimaryStyle(), {});
    emptyColumn->layoutWithInlineExtent(1000);
    ASSERT_EQ(emptyColumn->getLayoutMetrics().columns.size(), 2u);
    const auto& emptyMetrics = emptyColumn->getLayoutMetrics();
    EXPECT_EQ(emptyColumn->getGlyphPositionAtCoordinate(emptyMetrics.columns[1].physicalBounds.centerX(), 500).position,
              2);

    auto truncated = BuildParagraph("天地玄黃", PrimaryStyle(), {.maxLines = 2});
    truncated->layoutWithInlineExtent(advance);
    const auto truncatedPosition = truncated->getGlyphPositionAtCoordinate(-100, 0);
    EXPECT_GE(truncatedPosition.position, 1);
    EXPECT_LE(truncatedPosition.position, 2);

    const std::string emojiText = "\U0001F600";
    auto emoji = BuildParagraph(emojiText, PrimaryStyle(), {});
    emoji->layoutWithInlineExtent(1000);
    for (SkScalar y: {SkScalar(0), advance * 0.5f, advance * 2}) {
        const auto position =
                emoji->getGlyphPositionAtCoordinate(emoji->getLayoutMetrics().columns[0].physicalBounds.centerX(), y);
        EXPECT_NE(position.position, 1);
        EXPECT_TRUE(position.position == 0 || position.position == 2);
    }

    skia_text::Paragraph::GlyphClusterInfo cluster;
    ASSERT_TRUE(columns->getClosestGlyphClusterAt(rightX, 0, &cluster));
    EXPECT_EQ(cluster.fClusterTextRange.start, 0u);
    EXPECT_EQ(cluster.fClusterTextRange.end, 3u);
    EXPECT_FALSE(cluster.fBounds.isEmpty());
    skia_text::Paragraph::GlyphInfo info;
    ASSERT_TRUE(columns->getClosestUTF16GlyphInfoAt(rightX, 0, &info));
    EXPECT_EQ(info.fGraphemeClusterTextRange.start, 0u);
    EXPECT_EQ(info.fGraphemeClusterTextRange.end, 1u);
    EXPECT_TRUE(columns->getGlyphPositionAtCoordinate(rightX, 0).affinity == skia_text::Affinity::kDownstream);
}

TEST_F(SkiaVerticalParagraph, V19_CanvasPainterAndPathShareUprightAndSidewaysGeometry) {
    auto paragraph = BuildParagraph("漢A", PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(1000);
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_GE(runs.size(), 2u);
    EXPECT_TRUE(std::any_of(runs.begin(), runs.end(), [](const VisitorRun& run) {
        return IsClockwise(run.transform);
    }));
    EXPECT_TRUE(std::any_of(runs.begin(), runs.end(), [](const VisitorRun& run) {
        return IsIdentity(run.transform);
    }));

    constexpr int width = 220;
    constexpr int height = 520;
    const auto canvasBitmap = RasterizeText(paragraph.get(), 0, 0, width, height);
    SkBitmap painterBitmap;
    painterBitmap.allocN32Pixels(width, height);
    painterBitmap.eraseColor(SK_ColorWHITE);
    {
        SkCanvas canvas(painterBitmap);
        CanvasPainter painter(&canvas);
        paragraph->paint(&painter, 0, 0);
    }
    EXPECT_EQ(DifferingPixels(canvasBitmap, painterBitmap), 0u);
    EXPECT_GT(NonWhitePixels(canvasBitmap), 0u);
    EXPECT_GT(NonWhitePixels(painterBitmap), 0u);

    const SkPath path = CorePath(paragraph.get());
    EXPECT_FALSE(path.isEmpty());
    const auto pathBitmap = RasterizePath(path, width, height);
    EXPECT_GT(NonWhitePixels(pathBitmap), 0u);
    const size_t totalPixels = static_cast<size_t>(width) * height;
    EXPECT_LT(DifferingPixels(canvasBitmap, pathBitmap), totalPixels / 20);
    const SkRect expectedInk = paragraph->getLayoutMetrics().inkBounds;
    const SkRect pathBounds = path.getBounds();
    EXPECT_NEAR(pathBounds.left(), expectedInk.left(), 1.5f);
    EXPECT_NEAR(pathBounds.top(), expectedInk.top(), 1.5f);
    EXPECT_NEAR(pathBounds.right(), expectedInk.right(), 1.5f);
    EXPECT_NEAR(pathBounds.bottom(), expectedInk.bottom(), 1.5f);
}

TEST_F(SkiaVerticalParagraph, V20_PaintOffsetAndClipDoNotMutateLayoutOrQueries) {
    auto paragraph = BuildParagraph("漢A", PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(1000);
    const auto before = Capture(paragraph.get());
    const auto boxesBefore = TightBoxes(paragraph.get(), 0, 3);
    ASSERT_FALSE(boxesBefore.empty());

    constexpr int width = 240;
    constexpr int height = 540;
    const auto base = RasterizeText(paragraph.get(), 0, 0, width, height);
    const auto translated = RasterizeText(paragraph.get(), 25, 35, width, height);
    for (int y = 0; y < height; ++y) {
        for (int x = 0; x < width; ++x) {
            const bool sourceInk = base.getColor(x, y) != SK_ColorWHITE;
            const int destinationX = x + 25;
            const int destinationY = y + 35;
            const bool destinationInk = destinationX < width && destinationY < height &&
                                        translated.getColor(destinationX, destinationY) != SK_ColorWHITE;
            EXPECT_EQ(sourceInk, destinationInk) << x << "," << y;
        }
    }

    const SkRect clip = SkRect::MakeLTRB(0, 0, 90, 300);
    const auto clipped = RasterizeText(paragraph.get(), 0, 0, width, height, &clip);
    EXPECT_GT(NonWhitePixels(clipped, &clip), 0u);
    size_t outsideInk = 0;
    for (int y = 0; y < height; ++y) {
        for (int x = 0; x < width; ++x) {
            if (!clip.contains(x + 0.5f, y + 0.5f) && clipped.getColor(x, y) != SK_ColorWHITE) {
                ++outsideInk;
            }
        }
    }
    EXPECT_EQ(outsideInk, 0u);

    const auto path = CorePath(paragraph.get());
    const auto translatedPath = CorePath(paragraph.get(), 25, 35);
    EXPECT_NEAR(translatedPath.getBounds().left(), path.getBounds().left() + 25, kTolerance);
    EXPECT_NEAR(translatedPath.getBounds().top(), path.getBounds().top() + 35, kTolerance);
    EXPECT_NEAR(translatedPath.getBounds().right(), path.getBounds().right() + 25, kTolerance);
    EXPECT_NEAR(translatedPath.getBounds().bottom(), path.getBounds().bottom() + 35, kTolerance);

    ExpectSnapshotsNear(before, Capture(paragraph.get()));
    const auto boxesAfter = TightBoxes(paragraph.get(), 0, 3);
    ASSERT_EQ(boxesAfter.size(), boxesBefore.size());
    for (size_t index = 0; index < boxesAfter.size(); ++index) {
        EXPECT_EQ(boxesAfter[index].rect, boxesBefore[index].rect);
    }
}

TEST_F(SkiaVerticalParagraph, V21_RelayoutRestoresLargeHeightAndClearsExceededState) {
    auto paragraph = BuildParagraph("天地玄黃", PrimaryStyle(), {.maxLines = 3});
    paragraph->layoutWithInlineExtent(1000);
    const auto large = Capture(paragraph.get());
    ASSERT_EQ(large.columns.size(), 1u);
    EXPECT_FALSE(paragraph->didExceedMaxLines());

    auto one = BuildParagraph("天", PrimaryStyle(), {});
    one->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = one->getLayoutMetrics().longestInlineExtent;
    paragraph->layoutWithInlineExtent(advance * 0.5f);
    const auto& truncatedMetrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(truncatedMetrics.columns.size(), 3u);
    EXPECT_EQ(truncatedMetrics.columns[0].utf16End, 1u);
    EXPECT_EQ(truncatedMetrics.columns[1].utf16End, 2u);
    EXPECT_EQ(truncatedMetrics.columns[2].utf16End, 3u);
    EXPECT_TRUE(paragraph->didExceedMaxLines());
    EXPECT_EQ(paragraph->lineNumber(), 3u);
    EXPECT_TRUE(TightBoxes(paragraph.get(), 3, 4).empty());

    paragraph->layoutWithInlineExtent(1000);
    EXPECT_FALSE(paragraph->didExceedMaxLines());
    ExpectSnapshotsNear(large, Capture(paragraph.get()));
    EXPECT_EQ(paragraph->lineNumber(), 1u);
    EXPECT_FALSE(TightBoxes(paragraph.get(), 0, 4).empty());
}

TEST_F(SkiaVerticalParagraph, V22_HorizontalVerticalAndHeightVariantsRemainCacheIsolated) {
    skia_text::ParagraphStyle defaultStyle;
    EXPECT_EQ(defaultStyle.getWritingMode(), skia_text::WritingMode::kHorizontalTb);
    skia_text::ParagraphStyle verticalStyle;
    verticalStyle.setWritingMode(skia_text::WritingMode::kVerticalRl);
    EXPECT_EQ(verticalStyle.getWritingMode(), skia_text::WritingMode::kVerticalRl);
    EXPECT_FALSE(defaultStyle == verticalStyle);
    const skia_text::ParagraphStyle copied = verticalStyle;
    EXPECT_EQ(copied.getWritingMode(), skia_text::WritingMode::kVerticalRl);

    const std::string text = "「漢字ABC2026かな」、。ー";
    auto horizontal = BuildParagraph(text, PrimaryStyle(), {.writingMode = skia_text::WritingMode::kHorizontalTb});
    horizontal->layout(300);
    const SkScalar horizontalWidth = horizontal->getMaxWidth();
    const SkScalar horizontalHeight = horizontal->getHeight();
    const auto horizontalRuns = SnapshotRuns(horizontal.get());
    const auto horizontalBoxes = TightBoxes(horizontal.get(), 0, Utf16Length(text));
    ASSERT_FALSE(horizontalBoxes.empty());

    auto vertical = BuildParagraph(text, PrimaryStyle(), {});
    vertical->layoutWithInlineExtent(200);
    const auto verticalSnapshot = Capture(vertical.get());
    ASSERT_GT(verticalSnapshot.columns.size(), 1u);

    for (int round = 0; round < 3; ++round) {
        SCOPED_TRACE(round);
        horizontal->layout(250 + round);
        vertical->layoutWithInlineExtent(100 + round * 10);
    }
    horizontal->layout(300);
    vertical->layoutWithInlineExtent(200);
    EXPECT_NEAR(horizontal->getMaxWidth(), horizontalWidth, kTolerance);
    EXPECT_NEAR(horizontal->getHeight(), horizontalHeight, kTolerance);
    ExpectSnapshotsNear(verticalSnapshot, Capture(vertical.get()));
    const auto horizontalRunsAfter = SnapshotRuns(horizontal.get());
    ASSERT_EQ(horizontalRunsAfter.size(), horizontalRuns.size());
    for (size_t index = 0; index < horizontalRuns.size(); ++index) {
        EXPECT_EQ(horizontalRunsAfter[index].glyphs, horizontalRuns[index].glyphs);
        EXPECT_EQ(horizontalRunsAfter[index].starts, horizontalRuns[index].starts);
    }
    const auto horizontalBoxesAfter = TightBoxes(horizontal.get(), 0, Utf16Length(text));
    ASSERT_EQ(horizontalBoxesAfter.size(), horizontalBoxes.size());
    for (size_t index = 0; index < horizontalBoxes.size(); ++index) {
        EXPECT_EQ(horizontalBoxesAfter[index].rect, horizontalBoxes[index].rect);
    }

    auto small = BuildParagraph("漢字ABC", PrimaryStyle(32), {});
    small->layoutWithInlineExtent(160);
    auto large = BuildParagraph("漢字ABC", PrimaryStyle(72), {});
    large->layoutWithInlineExtent(260);
    const auto smallSnapshot = Capture(small.get());
    const auto largeSnapshot = Capture(large.get());
    for (int round = 0; round < 3; ++round) {
        small->layoutWithInlineExtent(100 + round);
        large->layoutWithInlineExtent(300 + round);
    }
    small->layoutWithInlineExtent(160);
    large->layoutWithInlineExtent(260);
    ExpectSnapshotsNear(smallSnapshot, Capture(small.get()));
    ExpectSnapshotsNear(largeSnapshot, Capture(large.get()));

    auto ruby = BuildCustomParagraph({.writingMode = skia_text::WritingMode::kHorizontalTb},
                                     [](skia_text::ParagraphBuilder* builder) {
                                         auto style = PrimaryStyle();
                                         builder->pushStyle(style);
                                         const std::string base = "漢字";
                                         builder->addText(base.data(), base.size());
                                         builder->addRuby(skia_text::RubyAnnotation({0, base.size()}, "かんじ"));
                                         builder->pop();
                                     });
    ruby->layout(1000);
    EXPECT_EQ(ruby->lineNumber(), 1u);
    EXPECT_FALSE(TightBoxes(ruby.get(), 0, 2).empty());
}

TEST_F(SkiaVerticalParagraph, V25_DirtyUpdatesAndRebuildInvalidateOnlyOwnedLayout) {
    const std::string text = "「漢字ABC2026かな」、。ー";
    auto paragraph = BuildParagraph(text, PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(180);
    const auto baseline = Capture(paragraph.get());
    const auto baselineBoxes = TightBoxes(paragraph.get(), 0, Utf16Length(text));
    ASSERT_FALSE(baselineBoxes.empty());
    const auto baselineBitmap = RasterizeText(paragraph.get(), 0, 0, 360, 520);
    ASSERT_GT(NonWhitePixels(baselineBitmap), 0u);

    paragraph->markDirty();
    EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    paragraph->layoutWithInlineExtent(180);
    ExpectSnapshotsNear(baseline, Capture(paragraph.get()));
    const auto boxesAfterDirty = TightBoxes(paragraph.get(), 0, Utf16Length(text));
    ASSERT_EQ(boxesAfterDirty.size(), baselineBoxes.size());
    for (size_t index = 0; index < baselineBoxes.size(); ++index) {
        EXPECT_EQ(boxesAfterDirty[index].rect, baselineBoxes[index].rect);
    }

    auto fontReference = BuildParagraph(text, PrimaryStyle(36), {});
    fontReference->layoutWithInlineExtent(180);
    paragraph->updateFontSize(0, text.size(), 36);
    EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    paragraph->layoutWithInlineExtent(180);
    ExpectSnapshotsNear(Capture(fontReference.get()), Capture(paragraph.get()));
    EXPECT_GT(std::abs(paragraph->getLayoutMetrics().maxIntrinsicInlineExtent - baseline.maxIntrinsic), kTolerance);

    auto alignReference = BuildParagraph(text, PrimaryStyle(36), {.align = skia_text::TextAlign::kEnd});
    alignReference->layoutWithInlineExtent(180);
    paragraph->updateTextAlign(skia_text::TextAlign::kEnd);
    EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    paragraph->layoutWithInlineExtent(180);
    ExpectSnapshotsNear(Capture(alignReference.get()), Capture(paragraph.get()));

    const auto geometryAfterUpdates = Capture(paragraph.get());
    const auto blackBitmap = RasterizeText(paragraph.get(), 0, 0, 360, 520);
    SkPaint red;
    red.setColor(SK_ColorRED);
    paragraph->updateForegroundPaint(0, text.size(), red);
    ExpectSnapshotsNear(geometryAfterUpdates, Capture(paragraph.get()));
    const auto redBitmap = RasterizeText(paragraph.get(), 0, 0, 360, 520);
    EXPECT_GT(DifferingPixels(blackBitmap, redBitmap), 0u);
    size_t redPixels = 0;
    for (int y = 0; y < redBitmap.height(); ++y) {
        for (int x = 0; x < redBitmap.width(); ++x) {
            const SkColor color = redBitmap.getColor(x, y);
            if (SkColorGetR(color) == 255 && SkColorGetG(color) < 240) {
                ++redPixels;
            }
        }
    }
    EXPECT_GT(redPixels, 0u);

    const auto beforeRejectedUpdates = Capture(paragraph.get());
    const auto beforeRejectedBitmap = RasterizeText(paragraph.get(), 0, 0, 360, 520);

    EXPECT_THROW(paragraph->updateFontSize(0, text.size(), -1), std::invalid_argument);
    ExpectSnapshotsNear(beforeRejectedUpdates, Capture(paragraph.get()));
    EXPECT_EQ(DifferingPixels(beforeRejectedBitmap, RasterizeText(paragraph.get(), 0, 0, 360, 520)), 0u);

    auto rebuilt = BuildParagraph(text, PrimaryStyle(36), {.align = skia_text::TextAlign::kEnd});
    rebuilt->layoutWithInlineExtent(180);
    ExpectSnapshotsNear(Capture(rebuilt.get()), Capture(paragraph.get()));
}

TEST_F(SkiaVerticalParagraph, Performance_FixedCorpusProbe) {
    std::string corpus;
    const std::string line = "「漢字ABC2026かな」、。ー\n";
    for (int index = 0; index < 20; ++index) {
        corpus += line;
    }
    auto paragraph = BuildParagraph(corpus, PrimaryStyle(28), {});
    const auto milliseconds = [](auto duration) {
        return std::chrono::duration<double, std::milli>(duration).count();
    };

    auto start = std::chrono::steady_clock::now();
    paragraph->layoutWithInlineExtent(420);
    const double coldLayoutMs = milliseconds(std::chrono::steady_clock::now() - start);

    start = std::chrono::steady_clock::now();
    paragraph->layoutWithInlineExtent(420);
    const double cachedLayoutMs = milliseconds(std::chrono::steady_clock::now() - start);

    paragraph->markDirty();
    start = std::chrono::steady_clock::now();
    paragraph->layoutWithInlineExtent(420);
    const double dirtyLayoutMs = milliseconds(std::chrono::steady_clock::now() - start);

    const auto& metrics = paragraph->getLayoutMetrics();
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_FALSE(metrics.columns.empty());
    ASSERT_FALSE(runs.empty());
    const int width = std::max(1, SkScalarCeilToInt(metrics.physicalSize.width()) + 20);
    const int height = std::max(1, SkScalarCeilToInt(metrics.physicalSize.height()) + 20);
    SkBitmap bitmap;
    bitmap.allocN32Pixels(width, height);
    bitmap.eraseColor(SK_ColorWHITE);
    SkCanvas canvas(bitmap);
    start = std::chrono::steady_clock::now();
    paragraph->paint(&canvas, 0, 0);
    const double paintMs = milliseconds(std::chrono::steady_clock::now() - start);
    ASSERT_GT(NonWhitePixels(bitmap), 0u);

    const size_t runCount = runs.size();
    const size_t columnCount = metrics.columns.size();
    const size_t glyphCount = CountGlyphs(runs);
    RecordProperty("run_count", static_cast<int>(runCount));
    RecordProperty("column_count", static_cast<int>(columnCount));
    RecordProperty("glyph_count", static_cast<int>(glyphCount));
    std::cout << "[vertical-paragraph-perf] runs=" << runCount << " columns=" << columnCount << " glyphs=" << glyphCount
              << " cold_layout_ms=" << coldLayoutMs << " cached_layout_ms=" << cachedLayoutMs
              << " dirty_layout_ms=" << dirtyLayoutMs << " paint_ms=" << paintMs << std::endl;
}

TEST_F(SkiaVerticalParagraph, V26_LineHeightOverrideUsesFontSizeWithoutReshaping) {
    const SkScalar fontSize = 32;
    std::vector<ParagraphSnapshot> snapshots;
    std::vector<VisitorRun> firstRuns;
    for (const SkScalar height: {SkScalar(1), SkScalar(1.5f), SkScalar(2)}) {
        auto style = PrimaryStyle(fontSize);
        style.setHeightOverride(true);
        style.setHeight(height);
        auto paragraph = BuildParagraph("漢", style, {});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 1u);
        EXPECT_NEAR(metrics.columns[0].blockThickness, height * fontSize, kTolerance);
        EXPECT_NEAR(metrics.physicalSize.width(), height * fontSize, kTolerance);
        EXPECT_NEAR(metrics.physicalSize.height(), metrics.longestInlineExtent, kTolerance);
        EXPECT_TRUE(MetricsAreFinite(metrics));

        const auto runs = SnapshotRuns(paragraph.get());
        ASSERT_EQ(runs.size(), 1u);
        EXPECT_NEAR(runs[0].font.getSize(), fontSize, kTolerance);
        if (firstRuns.empty()) {
            firstRuns = runs;
        } else {
            EXPECT_EQ(runs[0].glyphs, firstRuns[0].glyphs);
            EXPECT_EQ(runs[0].positions, firstRuns[0].positions);
            EXPECT_NEAR(metrics.longestInlineExtent,
                        snapshots.front().longest,
                        kTolerance);
        }

        const auto snapshot = Capture(paragraph.get());
        snapshots.push_back(snapshot);
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
        ExpectSnapshotsNear(snapshot, Capture(paragraph.get()));
    }

    auto natural = BuildParagraph("漢", PrimaryStyle(fontSize), {});
    natural->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    auto ignoredHeight = PrimaryStyle(fontSize);
    ignoredHeight.setHeight(-2);
    auto ignored = BuildParagraph("漢", ignoredHeight, {});
    ignored->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    ExpectSnapshotsNear(Capture(natural.get()), Capture(ignored.get()));
    EXPECT_NEAR(snapshots.front().longest, Capture(natural.get()).longest, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V27_LineHeightOverrideHonorsSignedHalfLeading) {
    auto naturalStyle = LatinStyle(32);
    auto natural = BuildParagraph("A", naturalStyle, {});
    natural->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& naturalMetrics = natural->getLayoutMetrics();
    ASSERT_EQ(naturalMetrics.columns.size(), 1u);
    const SkScalar naturalThickness = naturalMetrics.columns[0].blockThickness;
    const SkScalar naturalDescent = naturalMetrics.columns[0].physicalBaseline;
    const SkScalar naturalAscent = naturalThickness - naturalDescent;
    ASSERT_GT(naturalThickness, 0);

    const SkScalar targetThickness = 0.75f * 32;
    for (const bool halfLeading: {false, true}) {
        SCOPED_TRACE(halfLeading);
        auto style = LatinStyle(32);
        style.setHeightOverride(true);
        style.setHeight(0.75f);
        style.setHalfLeading(halfLeading);
        auto paragraph = BuildParagraph("A", style, {});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

        const SkScalar expectedAscent = halfLeading
                ? naturalAscent + (targetThickness - naturalThickness) * 0.5f
                : targetThickness * (naturalAscent / naturalThickness);
        const SkScalar expectedDescent = targetThickness - expectedAscent;
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 1u);
        EXPECT_NEAR(metrics.columns[0].blockThickness, targetThickness, kTolerance);
        EXPECT_NEAR(metrics.columns[0].physicalBaseline, expectedDescent, kTolerance);
        EXPECT_NEAR(metrics.columns[0].physicalBounds.left(), 0, kTolerance);
        EXPECT_NEAR(metrics.columns[0].physicalBounds.right(), targetThickness, kTolerance);
    }

    TestFontCollection()->getParagraphCache()->reset();
    auto tinyStyle = LatinStyle(32);
    tinyStyle.setHeightOverride(true);
    tinyStyle.setHeight(1e-9f);
    tinyStyle.setHalfLeading(true);
    auto tiny = BuildParagraph("j", tinyStyle, {});
    ASSERT_NO_THROW(tiny->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity()));
    const auto& tinyMetrics = tiny->getLayoutMetrics();
    ASSERT_EQ(tinyMetrics.columns.size(), 1u);
    const SkScalar tinyTarget = 1e-9f * 32;
    EXPECT_GT(tinyMetrics.columns[0].blockThickness, 0);
    EXPECT_NEAR(tinyMetrics.columns[0].blockThickness, tinyTarget, tinyTarget * 1e-4f);
    EXPECT_NEAR(tinyMetrics.physicalSize.width(), tinyTarget, tinyTarget * 1e-4f);
    EXPECT_TRUE(MetricsAreFinite(tinyMetrics));
}

TEST_F(SkiaVerticalParagraph, V28_MixedRunsMergeEachBlockSideIndependently) {
    const SkScalar height = 1.5f;
    auto measureNaturalSides = [](const skia_text::TextStyle& style, const char* text) {
        auto paragraph = BuildParagraph(text, style, {});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
        const auto& metrics = paragraph->getLayoutMetrics();
        EXPECT_EQ(metrics.columns.size(), 1u);
        const SkScalar thickness = metrics.columns[0].blockThickness;
        const SkScalar descent = metrics.columns[0].physicalBaseline;
        return std::pair<SkScalar, SkScalar>(thickness - descent, descent);
    };
    auto targetSides = [height](std::pair<SkScalar, SkScalar> natural, SkScalar fontSize) {
        const SkScalar target = height * fontSize;
        const SkScalar ascent = target * (natural.first / (natural.first + natural.second));
        return std::pair<SkScalar, SkScalar>(ascent, target - ascent);
    };

    auto smallStyle = PrimaryStyle(32);
    auto largeStyle = LatinStyle(48);
    smallStyle.setHeightOverride(true);
    smallStyle.setHeight(height);
    largeStyle.setHeightOverride(true);
    largeStyle.setHeight(height);
    const auto small = targetSides(measureNaturalSides(PrimaryStyle(32), "漢"), 32);
    const auto large = targetSides(measureNaturalSides(LatinStyle(48), "A"), 48);

    auto paragraph = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(smallStyle);
        builder->addText("漢", 3);
        builder->pop();
        builder->pushStyle(largeStyle);
        builder->addText("A", 1);
        builder->pop();
    });
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    const SkScalar expectedAscent = std::max(small.first, large.first);
    const SkScalar expectedDescent = std::max(small.second, large.second);
    EXPECT_NEAR(metrics.columns[0].blockThickness, expectedAscent + expectedDescent, kTolerance);
    EXPECT_NEAR(metrics.columns[0].physicalBaseline, expectedDescent, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V29_EmptyColumnsUseSelectedStyleAndHardBreaksDoNotWiden) {
    auto style = PrimaryStyle(32);
    style.setHeightOverride(true);
    style.setHeight(1.5f);
    const SkScalar expectedThickness = 48;
    for (const std::string& text: {std::string(), std::string("A\n"), std::string("\nA"),
                                   std::string("A\n\n"), std::string("A\r\n")}) {
        SCOPED_TRACE(text);
        auto paragraph = BuildParagraph(text, style, {});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_FALSE(metrics.columns.empty());
        for (const auto& column: metrics.columns) {
            EXPECT_NEAR(column.blockThickness, expectedThickness, kTolerance);
            EXPECT_TRUE(std::isfinite(column.physicalBaseline));
        }
    }

    auto plainStyle = PrimaryStyle(16);
    plainStyle.setHeightOverride(true);
    plainStyle.setHeight(1);
    auto breakStyle = PrimaryStyle(64);
    breakStyle.setHeightOverride(true);
    breakStyle.setHeight(1);
    auto paragraph = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(plainStyle);
        builder->addText("A", 1);
        builder->pop();
        builder->pushStyle(breakStyle);
        builder->addText("\n", 1);
        builder->pop();
    });
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    EXPECT_NEAR(metrics.columns[0].blockThickness, 16, kTolerance);
    EXPECT_NEAR(metrics.columns[0].physicalBounds.right(), metrics.physicalSize.width(), kTolerance);
}

TEST_F(SkiaVerticalParagraph, V30_SmallLineHeightKeepsInkAndQueriesOnFinalColumnGeometry) {
    auto style = PrimaryStyle(48);
    style.setHeightOverride(true);
    style.setHeight(0.5f);
    auto paragraph = BuildParagraph("漢", style, {});
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_NEAR(metrics.columns[0].blockThickness, 24, kTolerance);
    EXPECT_GT(metrics.inkBounds.width(), metrics.columns[0].blockThickness);
    EXPECT_LT(metrics.inkBounds.left(), metrics.columns[0].physicalBounds.left());

    const auto boxes = TightBoxes(paragraph.get(), 0, 1);
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_NEAR(boxes[0].rect.width(), metrics.columns[0].blockThickness, kTolerance);
    EXPECT_NEAR(boxes[0].rect.left(), metrics.columns[0].physicalBounds.left(), kTolerance);
    const auto position = paragraph->getGlyphPositionAtCoordinate(boxes[0].rect.centerX(),
                                                                 boxes[0].rect.centerY());
    EXPECT_GE(position.position, 0u);
    EXPECT_LE(position.position, Utf16Length("漢"));

    SkPath path;
    ASSERT_EQ(paragraph->getPath(0, &path), 0);
    ASSERT_FALSE(path.isEmpty());
    EXPECT_GT(path.getBounds().width(), metrics.columns[0].blockThickness);
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_FALSE(runs.empty());
    SkRect visitorInk = SkRect::MakeEmpty();
    bool hasVisitorInk = false;
    for (const auto& run: runs) {
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            const SkRect glyph = GlyphInkBounds(run, index);
            if (!hasVisitorInk) {
                visitorInk = glyph;
                hasVisitorInk = true;
            } else {
                visitorInk.join(glyph);
            }
        }
    }
    ASSERT_TRUE(hasVisitorInk);
    EXPECT_NEAR(visitorInk.left(), metrics.inkBounds.left(), 0.5f);
    EXPECT_NEAR(visitorInk.top(), metrics.inkBounds.top(), 0.5f);
    EXPECT_NEAR(visitorInk.right(), metrics.inkBounds.right(), 0.5f);
    EXPECT_NEAR(visitorInk.bottom(), metrics.inkBounds.bottom(), 0.5f);
}

TEST_F(SkiaVerticalParagraph, V31_InvalidLineHeightFailsAndInvalidatesLayout) {
    auto expectRejected = [](SkScalar height) {
        auto style = PrimaryStyle(32);
        style.setHeightOverride(true);
        style.setHeight(height);
        auto paragraph = BuildParagraph("漢", style, {});
        EXPECT_THROW(paragraph->layoutWithInlineExtent(200), std::invalid_argument);
        EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    };
    expectRejected(0);
    expectRejected(-1);
    expectRejected(std::numeric_limits<SkScalar>::quiet_NaN());
    expectRejected(std::numeric_limits<SkScalar>::infinity());
    expectRejected(std::numeric_limits<SkScalar>::max());

    auto style = PrimaryStyle(32);
    style.setHeightOverride(true);
    style.setHeight(1.5f);
    auto wrapped = BuildParagraph("天地玄黃", style, {.maxLines = 2});
    wrapped->layoutWithInlineExtent(1);
    const auto& metrics = wrapped->getLayoutMetrics();
    EXPECT_EQ(metrics.columns.size(), 2u);
    EXPECT_TRUE(wrapped->didExceedMaxLines());
    for (const auto& column: metrics.columns) {
        EXPECT_NEAR(column.blockThickness, 48, kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, V32_FixedInlineLineHeightOnlyChangesColumnGeometry) {
    const std::string text = "天地玄黃宇宙洪荒";
    constexpr SkScalar fontSize = 32;
    constexpr SkScalar inlineExtent = 100;

    auto build = [&](SkScalar height, bool overrideHeight = true) {
        auto style = PrimaryStyle(fontSize);
        style.setHeightOverride(overrideHeight);
        style.setHeight(height);
        auto paragraph = BuildParagraph(text, style, {});
        paragraph->layoutWithInlineExtent(inlineExtent);
        return paragraph;
    };

    auto baseline = build(1);
    const auto baselineSnapshot = Capture(baseline.get());
    ASSERT_GT(baselineSnapshot.columns.size(), 1u);
    EXPECT_NEAR(baselineSnapshot.physicalSize.height(), inlineExtent, kTolerance);

    std::vector<VisitorRun> baselineRuns;
    for (SkScalar height: {SkScalar(1.5f), SkScalar(2)}) {
        SCOPED_TRACE(height);
        auto paragraph = build(height);
        const auto snapshot = Capture(paragraph.get());
        ASSERT_EQ(snapshot.columns.size(), baselineSnapshot.columns.size());
        EXPECT_NEAR(snapshot.physicalSize.height(), inlineExtent, kTolerance);
        EXPECT_NEAR(snapshot.physicalSize.width(),
                    baselineSnapshot.columns.size() * height * fontSize,
                    kTolerance);
        EXPECT_NEAR(snapshot.longest, baselineSnapshot.longest, kTolerance);
        EXPECT_NEAR(snapshot.minIntrinsic, baselineSnapshot.minIntrinsic, kTolerance);
        EXPECT_NEAR(snapshot.maxIntrinsic, baselineSnapshot.maxIntrinsic, kTolerance);
        for (size_t index = 0; index < snapshot.columns.size(); ++index) {
            EXPECT_EQ(snapshot.columns[index].utf16Start, baselineSnapshot.columns[index].utf16Start);
            EXPECT_EQ(snapshot.columns[index].utf16End, baselineSnapshot.columns[index].utf16End);
            EXPECT_NEAR(snapshot.columns[index].inlineExtent,
                        baselineSnapshot.columns[index].inlineExtent,
                        kTolerance);
            EXPECT_NEAR(snapshot.columns[index].blockThickness, height * fontSize, kTolerance);
            EXPECT_NEAR(snapshot.columns[index].bounds.top(),
                        baselineSnapshot.columns[index].bounds.top(),
                        kTolerance);
        }
        EXPECT_NEAR(std::accumulate(snapshot.columns.begin(), snapshot.columns.end(), SkScalar(0),
                                    [](SkScalar sum, const SnapshotColumn& column) {
                                        return sum + column.blockThickness;
                                    }),
                    snapshot.physicalSize.width(),
                    kTolerance);

        const auto runs = SnapshotRuns(paragraph.get());
        if (baselineRuns.empty()) {
            baselineRuns = runs;
        } else {
            ASSERT_EQ(runs.size(), baselineRuns.size());
            for (size_t index = 0; index < runs.size(); ++index) {
                EXPECT_EQ(runs[index].glyphs, baselineRuns[index].glyphs);
                EXPECT_EQ(runs[index].positions, baselineRuns[index].positions);
            }
        }
    }

    auto natural = build(0, false);
    auto ignoredPositive = build(3, false);
    ExpectSnapshotsNear(Capture(natural.get()), Capture(ignoredPositive.get()));
}

TEST_F(SkiaVerticalParagraph, V33_LineHeightMixedOutputsShareFinalGeometryAndPreserveInkOverflow) {
    const std::string text = "「漢A1、。ー天地」";
    constexpr SkScalar fontSize = 40;
    const SkScalar columns[] = {1.5f, 0.4f};
    for (const SkScalar height: columns) {
        SCOPED_TRACE(height);
        auto style = PrimaryStyle(fontSize);
        style.setHeightOverride(true);
        style.setHeight(height);
        auto paragraph = BuildParagraph(text, style, {});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 1u);
        EXPECT_NEAR(metrics.columns[0].blockThickness, height * fontSize, kTolerance);
        const auto runs = SnapshotRuns(paragraph.get());
        ASSERT_FALSE(runs.empty());
        EXPECT_TRUE(std::any_of(runs.begin(), runs.end(), [](const VisitorRun& run) {
            return IsIdentity(run.transform);
        }));
        EXPECT_TRUE(std::any_of(runs.begin(), runs.end(), [](const VisitorRun& run) {
            return IsClockwise(run.transform);
        }));

        constexpr int width = 220;
        constexpr int heightPixels = 560;
        const auto canvasBitmap = RasterizeText(paragraph.get(), 0, 0, width, heightPixels);
        SkBitmap painterBitmap;
        painterBitmap.allocN32Pixels(width, heightPixels);
        painterBitmap.eraseColor(SK_ColorWHITE);
        {
            SkCanvas canvas(painterBitmap);
            CanvasPainter painter(&canvas);
            paragraph->paint(&painter, 0, 0);
        }
        EXPECT_EQ(DifferingPixels(canvasBitmap, painterBitmap), 0u);
        EXPECT_GT(NonWhitePixels(canvasBitmap), 0u);

        const SkPath path = CorePath(paragraph.get());
        EXPECT_FALSE(path.isEmpty());
        const SkRect pathBounds = path.getBounds();
        EXPECT_NEAR(pathBounds.left(), metrics.inkBounds.left(), 1.5f);
        EXPECT_NEAR(pathBounds.top(), metrics.inkBounds.top(), 1.5f);
        EXPECT_NEAR(pathBounds.right(), metrics.inkBounds.right(), 1.5f);
        EXPECT_NEAR(pathBounds.bottom(), metrics.inkBounds.bottom(), 1.5f);
        const auto visitorInk = VisitorInkBounds(runs);
        ASSERT_TRUE(visitorInk.has_value());
        EXPECT_NEAR(visitorInk->left(), metrics.inkBounds.left(), 0.5f);
        EXPECT_NEAR(visitorInk->top(), metrics.inkBounds.top(), 0.5f);
        EXPECT_NEAR(visitorInk->right(), metrics.inkBounds.right(), 0.5f);
        EXPECT_NEAR(visitorInk->bottom(), metrics.inkBounds.bottom(), 0.5f);
        if (height < 1) {
            EXPECT_GT(metrics.inkBounds.width(), metrics.columns[0].blockThickness);
            EXPECT_GT(NonWhitePixels(RasterizePath(path, width, heightPixels)), 0u);
        }
    }

    auto emojiStyle = PrimaryStyle(fontSize);
    emojiStyle.setHeightOverride(true);
    emojiStyle.setHeight(1.5f);
    auto emoji = BuildParagraph("漢A1、。\U0001F600ー", emojiStyle, {});
    emoji->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    EXPECT_EQ(emoji->unresolvedGlyphs(), 0u);
    const auto emojiRuns = SnapshotRuns(emoji.get());
    EXPECT_GT(CountGlyphs(emojiRuns), 0u);
    EXPECT_NEAR(emoji->getLayoutMetrics().physicalSize.width(), fontSize * 1.5f, 0.2f);
    EXPECT_NEAR(emoji->getLayoutMetrics().columns[0].blockThickness,
                emoji->getLayoutMetrics().physicalSize.width(),
                kTolerance);
}

TEST_F(SkiaVerticalParagraph, V34_LineHeightPreservesNoWrapSingleLineAndMaxLinesSemantics) {
    auto style = PrimaryStyle(32);
    style.setHeightOverride(true);
    style.setHeight(1.5f);

    auto noWrap = BuildParagraph("天地玄黃", style, {});
    noWrap->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& noWrapMetrics = noWrap->getLayoutMetrics();
    ASSERT_EQ(noWrapMetrics.columns.size(), 1u);
    EXPECT_NEAR(noWrapMetrics.columns[0].blockThickness, 48, kTolerance);
    EXPECT_NEAR(noWrapMetrics.physicalSize.height(), noWrapMetrics.longestInlineExtent, kTolerance);
    EXPECT_FALSE(noWrap->didExceedMaxLines());

    auto hardBreak = BuildParagraph("天地\n玄黃", style, {});
    hardBreak->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& hardBreakMetrics = hardBreak->getLayoutMetrics();
    ASSERT_EQ(hardBreakMetrics.columns.size(), 2u);
    EXPECT_TRUE(hardBreakMetrics.columns[0].hardBreak);
    ExpectContiguousSourceRanges(hardBreakMetrics, Utf16Length("天地\n玄黃"));
    for (const auto& column: hardBreakMetrics.columns) {
        EXPECT_NEAR(column.blockThickness, 48, kTolerance);
    }

    auto singleLine = BuildParagraph("天地玄黃", style, {.maxLines = 1});
    singleLine->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& singleLineMetrics = singleLine->getLayoutMetrics();
    ASSERT_EQ(singleLineMetrics.columns.size(), 1u);
    EXPECT_FALSE(singleLine->didExceedMaxLines());
    EXPECT_NEAR(singleLineMetrics.physicalSize.width(), 48, kTolerance);
    EXPECT_NEAR(singleLineMetrics.physicalSize.height(), noWrapMetrics.longestInlineExtent, kTolerance);

    auto truncated = BuildParagraph("天地玄黃", style, {.maxLines = 2});
    truncated->layoutWithInlineExtent(1);
    const auto& truncatedMetrics = truncated->getLayoutMetrics();
    ASSERT_EQ(truncatedMetrics.columns.size(), 2u);
    EXPECT_TRUE(truncated->didExceedMaxLines());
    EXPECT_EQ(truncatedMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(truncatedMetrics.columns[1].utf16Start, 1u);
    EXPECT_LT(truncatedMetrics.columns[1].utf16End, Utf16Length("天地玄黃"));
    for (const auto& column: truncatedMetrics.columns) {
        EXPECT_NEAR(column.blockThickness, 48, kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, V35_LineHeightPreservesAlignmentAndExtentSemantics) {
    auto makeStyle = [] {
        auto style = PrimaryStyle(32);
        style.setHeightOverride(true);
        style.setHeight(1.5f);
        return style;
    };

    auto start = BuildParagraph("天地玄黃", makeStyle(), {.align = skia_text::TextAlign::kStart});
    start->layoutWithInlineExtent(300);
    auto end = BuildParagraph("天地玄黃", makeStyle(), {.align = skia_text::TextAlign::kEnd});
    end->layoutWithInlineExtent(300);
    auto center = BuildParagraph("天地玄黃", makeStyle(), {.align = skia_text::TextAlign::kCenter});
    center->layoutWithInlineExtent(300);
    const auto& startMetrics = start->getLayoutMetrics();
    const auto& endMetrics = end->getLayoutMetrics();
    const auto& centerMetrics = center->getLayoutMetrics();
    ASSERT_EQ(startMetrics.columns.size(), 1u);
    ASSERT_EQ(endMetrics.columns.size(), 1u);
    ASSERT_EQ(centerMetrics.columns.size(), 1u);
    const SkScalar remaining = 300 - startMetrics.columns[0].inlineExtent;
    EXPECT_NEAR(startMetrics.columns[0].physicalBounds.top(), 0, kTolerance);
    EXPECT_NEAR(endMetrics.columns[0].physicalBounds.top(), remaining, 0.2f);
    EXPECT_NEAR(centerMetrics.columns[0].physicalBounds.top(), remaining * 0.5f, 0.2f);
    EXPECT_NEAR(startMetrics.columns[0].blockThickness, 48, kTolerance);

    auto zero = BuildParagraph("天地玄黃", makeStyle(), {});
    zero->layoutWithInlineExtent(0);
    const auto& zeroMetrics = zero->getLayoutMetrics();
    ASSERT_EQ(zeroMetrics.columns.size(), 4u);
    EXPECT_NEAR(zeroMetrics.physicalSize.height(), 0, kTolerance);
    EXPECT_NEAR(zeroMetrics.physicalSize.width(), 4 * 48, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(zeroMetrics));
    for (const auto& column: zeroMetrics.columns) {
        EXPECT_NEAR(column.physicalBounds.top(), 0, kTolerance);
        EXPECT_GT(column.inlineExtent, 0);
    }

    auto infinite = BuildParagraph("天地玄黃", makeStyle(), {});
    infinite->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& infiniteMetrics = infinite->getLayoutMetrics();
    ASSERT_EQ(infiniteMetrics.columns.size(), 1u);
    EXPECT_NEAR(infiniteMetrics.physicalSize.height(), infiniteMetrics.longestInlineExtent, kTolerance);
    EXPECT_NEAR(infiniteMetrics.columns[0].physicalBounds.top(), 0, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(infiniteMetrics));
}

TEST_F(SkiaVerticalParagraph, V36_InterleavedLineHeightVariantsRemainCacheIsolated) {
    const std::string text = "「漢字ABC2026かな」、。ー";
    struct Variant {
        std::unique_ptr<skia_text::Paragraph> paragraph;
        ParagraphSnapshot snapshot;
    };
    std::vector<Variant> variants;
    for (const auto [height, halfLeading]: {std::pair{1.0f, false}, std::pair{1.5f, false},
                                                      std::pair{2.0f, false}, std::pair{1.5f, true}}) {
        auto style = PrimaryStyle(32);
        style.setHeightOverride(true);
        style.setHeight(height);
        style.setHalfLeading(halfLeading);
        auto paragraph = BuildParagraph(text, style, {});
        paragraph->layoutWithInlineExtent(180);
        auto snapshot = Capture(paragraph.get());
        variants.push_back({std::move(paragraph), std::move(snapshot)});
    }

    auto horizontal = BuildParagraph(text, PrimaryStyle(32), {.writingMode = skia_text::WritingMode::kHorizontalTb});
    horizontal->layout(300);
    const SkScalar horizontalHeight = horizontal->getHeight();

    for (size_t round = 0; round < 3; ++round) {
        for (size_t index = 0; index < variants.size(); ++index) {
            variants[index].paragraph->layoutWithInlineExtent(90 + round * 20 + index);
            horizontal->layout(280 + round * 10);
        }
    }
    for (size_t index = 0; index < variants.size(); ++index) {
        variants[index].paragraph->layoutWithInlineExtent(180);
        ExpectSnapshotsNear(variants[index].snapshot, Capture(variants[index].paragraph.get()));
        variants[index].paragraph->markDirty();
        EXPECT_THROW(variants[index].paragraph->getLayoutMetrics(), std::logic_error);
        variants[index].paragraph->layoutWithInlineExtent(180);
        ExpectSnapshotsNear(variants[index].snapshot, Capture(variants[index].paragraph.get()));
    }
    horizontal->layout(300);
    EXPECT_NEAR(horizontal->getHeight(), horizontalHeight, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V39_LineHeightCacheUsesExactMultiplier) {
    const std::string text = "快取行高";
    const SkScalar tinyHeight = 0.00001f;
    const SkScalar tinyThickness = tinyHeight * 32;
    auto layoutThickness = [&](std::optional<SkScalar> height) {
        auto style = PrimaryStyle(32);
        if (height.has_value()) {
            style.setHeightOverride(true);
            style.setHeight(*height);
        }
        auto paragraph = BuildParagraph(text, style, {});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
        const auto& metrics = paragraph->getLayoutMetrics();
        EXPECT_EQ(metrics.columns.size(), 1u);
        EXPECT_NEAR(metrics.physicalSize.width(), metrics.columns[0].blockThickness, kTolerance);
        return metrics.columns[0].blockThickness;
    };

    TestFontCollection()->getParagraphCache()->reset();
    const SkScalar natural = layoutThickness(std::nullopt);
    const SkScalar tinyAfterNatural = layoutThickness(tinyHeight);
    const SkScalar naturalAfterTiny = layoutThickness(std::nullopt);
    EXPECT_NEAR(tinyAfterNatural, tinyThickness, tinyThickness * 1e-4f);
    EXPECT_NEAR(naturalAfterTiny, natural, kTolerance);

    TestFontCollection()->getParagraphCache()->reset();
    const SkScalar tinyCold = layoutThickness(tinyHeight);
    const SkScalar naturalAfterColdTiny = layoutThickness(std::nullopt);
    EXPECT_NEAR(tinyCold, tinyThickness, tinyThickness * 1e-4f);
    EXPECT_NEAR(naturalAfterColdTiny, natural, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V37_LineHeightVisualRasterProbe) {
    auto build = [](SkScalar height) {
        auto base = PrimaryStyle(40);
        base.setHeightOverride(true);
        base.setHeight(height);
        auto large = PrimaryStyle(56);
        large.setHeightOverride(true);
        large.setHeight(height);
        auto small = LatinStyle(28);
        small.setHeightOverride(true);
        small.setHeight(height);
        return BuildCoreParagraph("天地玄黃", base, {}, [&](skia_text::ParagraphBuilder* builder) {
            builder->pushStyle(large);
            builder->addText("ABC123", 6);
            builder->pop();
            builder->pushStyle(small);
            builder->addText("、。ー", 9);
            builder->pop();
            builder->pushStyle(base);
            builder->addText("\n\n日月", 8);
            builder->pop();
        });
    };

    auto normal = build(1.4f);
    normal->layoutWithInlineExtent(200);
    const auto normalMetrics = normal->getLayoutMetrics();
    ASSERT_GT(normalMetrics.columns.size(), 1u);
    ASSERT_TRUE(std::any_of(normalMetrics.columns.begin(), normalMetrics.columns.end(), [](const auto& column) {
        return column.inlineExtent == 0;
    }));

    auto small = build(0.4f);
    small->layoutWithInlineExtent(200);
    const auto smallMetrics = small->getLayoutMetrics();
    ASSERT_EQ(smallMetrics.columns.size(), normalMetrics.columns.size());
    EXPECT_LT(smallMetrics.physicalSize.width(), normalMetrics.physicalSize.width());
    EXPECT_GT(smallMetrics.inkBounds.width(), smallMetrics.columns[0].blockThickness);
    EXPECT_NEAR(smallMetrics.columns[0].blockThickness, 0.4f * 40, kTolerance);

    auto render = [](skia_text::Paragraph* paragraph, const char* path) {
        const auto& metrics = paragraph->getLayoutMetrics();
        constexpr SkScalar padding = 20;
        const int width = std::max(360, SkScalarCeilToInt(std::max(metrics.physicalSize.width(),
                                                                  metrics.inkBounds.right()) + padding * 2));
        const int height = std::max(520, SkScalarCeilToInt(std::max(metrics.physicalSize.height(),
                                                                   metrics.inkBounds.bottom()) + padding * 2));
        SkBitmap bitmap;
        bitmap.allocN32Pixels(width, height);
        bitmap.eraseColor(SK_ColorWHITE);
        SkCanvas canvas(bitmap);
        SkPaint highlight;
        highlight.setColor(SkColorSetARGB(90, 70, 140, 255));
        const size_t highlightEnd = std::min<size_t>(6, Utf16Length("天地玄黃ABC123、。ー\n\n日月"));
        for (const auto& box: TightBoxes(paragraph, 0, highlightEnd)) {
            SkRect rect = box.rect;
            rect.offset(padding, padding);
            canvas.drawRect(rect, highlight);
        }
        paragraph->paint(&canvas, padding, padding);
        EXPECT_GT(NonWhitePixels(bitmap), 0u);
        SavePng(bitmap, path);
    };
    render(normal.get(), "vertical-line-height-normal.png");
    render(small.get(), "vertical-line-height-small.png");
}

TEST_F(SkiaVerticalParagraph, V38_ControlCharactersAndTabsFollowVerticalTextSemantics) {
    auto style = PrimaryStyle(32);
    style.setHeightOverride(true);
    style.setHeight(1.5f);
    const std::pair<std::string, std::string> cases[] = {
            {"A\u00ADB", "soft hyphen"},
            {"A\u200EB", "left-to-right mark"},
            {"A\u200FB", "right-to-left mark"},
            {"A\u061CB", "arabic letter mark"},
    };
    for (const auto& [text, label]: cases) {
        SCOPED_TRACE(label);
        auto paragraph = BuildParagraph(text, style, {});
        EXPECT_NO_THROW(paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity()));
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_FALSE(metrics.columns.empty());
        EXPECT_TRUE(MetricsAreFinite(metrics));
        EXPECT_NEAR(metrics.columns[0].blockThickness, 48, kTolerance);
        EXPECT_EQ(CountGlyphs(SnapshotRuns(paragraph.get())), 2u);
        EXPECT_EQ(paragraph->unresolvedGlyphs(), 0u);
        EXPECT_NO_THROW(TightBoxes(paragraph.get(), 0, Utf16Length(text)));

        SkPath path;
        EXPECT_EQ(paragraph->getPath(0, &path), 0);
        EXPECT_FALSE(path.isEmpty());
    }

    auto measureInline = [](const std::string& text, bool replaceTabs) {
        auto paragraph = BuildParagraph(text, LatinStyle(32),
                {.replaceTabCharacters = replaceTabs});
        paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
        const auto& metrics = paragraph->getLayoutMetrics();
        EXPECT_EQ(metrics.columns.size(), 1u);
        EXPECT_EQ(paragraph->unresolvedGlyphs(), 0u);
        EXPECT_EQ(CountGlyphs(SnapshotRuns(paragraph.get())), Utf16Length(text));
        return metrics.columns[0].inlineExtent;
    };
    const SkScalar ab = measureInline("AB", false);
    const SkScalar aSpaceB = measureInline("A B", false);
    const SkScalar tabAsSpace = measureInline("A\tB", false);
    const SkScalar replacedTabAsSpace = measureInline("A\tB", true);
    EXPECT_GT(aSpaceB, ab);
    EXPECT_NEAR(tabAsSpace, aSpaceB, kTolerance);
    EXPECT_NEAR(replacedTabAsSpace, aSpaceB, kTolerance);

    auto onlyControls = BuildParagraph("\u00ADA\u200E\u200F\u061C", style, {});
    EXPECT_NO_THROW(onlyControls->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity()));
    const auto& controlMetrics = onlyControls->getLayoutMetrics();
    ASSERT_EQ(controlMetrics.columns.size(), 1u);
    EXPECT_EQ(CountGlyphs(SnapshotRuns(onlyControls.get())), 1u);
    EXPECT_GT(controlMetrics.columns[0].blockThickness, 0);
    EXPECT_TRUE(MetricsAreFinite(controlMetrics));
}

TEST_F(SkiaVerticalParagraph, V24_UnsupportedVerticalCombinationsAreRejectedExplicitly) {
    auto expectRejected = [](std::unique_ptr<skia_text::Paragraph> paragraph, const char* label = "") {
        SCOPED_TRACE(label);
        EXPECT_THROW(paragraph->layoutWithInlineExtent(300), std::invalid_argument);
        EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    };

    expectRejected(BuildParagraph("漢字", PrimaryStyle(), {.direction = skia_text::TextDirection::kRtl}), "rtl base");
    expectRejected(BuildParagraph("\u05E9\u05DC\u05D5\u05DD", PrimaryStyle(), {}), "strong rtl");
    expectRejected(BuildParagraph("漢字", PrimaryStyle(), {.align = skia_text::TextAlign::kLeft}), "left align");
    expectRejected(BuildParagraph("漢字", PrimaryStyle(), {.align = skia_text::TextAlign::kRight}), "right align");
    expectRejected(BuildParagraph("漢字", PrimaryStyle(), {.maxLines = 0}), "zero max lines");

    expectRejected(BuildConfiguredParagraph("漢字",
                                            PrimaryStyle(),
                                            [](skia_text::ParagraphStyle& style) {
                                                auto strut = style.getStrutStyle();
                                                strut.setStrutEnabled(true);
                                                style.setStrutStyle(strut);
                                            }),
                   "strut");
    expectRejected(BuildConfiguredParagraph("漢字",
                                            PrimaryStyle(),
                                            [](skia_text::ParagraphStyle& style) {
                                                style.setTextHeightBehavior(skia_text::kDisableFirstAscent);
                                            }),
                   "height behavior");
    expectRejected(BuildConfiguredParagraph(
                           "漢字",
                           PrimaryStyle(),
                           [](skia_text::ParagraphStyle&) {},
                           [](skia_text::ParagraphBuilder* builder) {
                               skia_text::RubyStyle rubyStyle;
                               rubyStyle.position = skia_text::RubyPosition::Below;
                               builder->addRuby(skia_text::RubyAnnotation({0, 3}, "かん", rubyStyle));
                           }),
                   "ruby");
    expectRejected(BuildConfiguredParagraph(
                           "漢字",
                           PrimaryStyle(),
                           [](skia_text::ParagraphStyle&) {},
                           [](skia_text::ParagraphBuilder* builder) {
                               builder->addPlaceholder(
                                       skia_text::PlaceholderStyle(10,
                                                                   10,
                                                                   skia_text::PlaceholderAlignment::kBaseline,
                                                                   skia_text::TextBaseline::kAlphabetic,
                                                                   0));
                           }),
                   "placeholder");

    for (auto decoration: {skia_text::TextDecoration::kUnderline,
                           skia_text::TextDecoration::kLineThrough,
                           static_cast<skia_text::TextDecoration>(
                                   static_cast<int>(skia_text::TextDecoration::kUnderline) |
                                   static_cast<int>(skia_text::TextDecoration::kLineThrough))}) {
        auto supported = DecorationStyleFor(PrimaryStyle(), decoration);
        EXPECT_NO_THROW(BuildParagraph("漢字", supported, {})->layoutWithInlineExtent(300));
    }
    auto overline = DecorationStyleFor(PrimaryStyle(), skia_text::TextDecoration::kOverline);
    expectRejected(BuildParagraph("漢字", overline, {}), "overline");
    auto overlineCombined = DecorationStyleFor(
            PrimaryStyle(),
            static_cast<skia_text::TextDecoration>(
                    static_cast<int>(skia_text::TextDecoration::kUnderline) |
                    static_cast<int>(skia_text::TextDecoration::kOverline)));
    expectRejected(BuildParagraph("漢字", overlineCombined, {}), "underline overline combination");

    auto gaps = DecorationStyleFor(PrimaryStyle(), skia_text::TextDecoration::kUnderline);
    gaps.setDecorationMode(skia_text::TextDecorationMode::kGaps);
    expectRejected(BuildParagraph("漢字", gaps, {}), "decoration gaps mode");
    for (auto style: {skia_text::TextDecorationStyle::kDouble,
                      skia_text::TextDecorationStyle::kDotted,
                      skia_text::TextDecorationStyle::kDashed,
                      skia_text::TextDecorationStyle::kWavy}) {
        auto nonSolid = DecorationStyleFor(PrimaryStyle(), skia_text::TextDecoration::kUnderline);
        nonSolid.setDecorationStyle(style);
        expectRejected(BuildParagraph("漢字", nonSolid, {}), "non-solid decoration style");
    }
    for (SkScalar multiplier: {SkScalar(0),
                               SkScalar(-1),
                               std::numeric_limits<SkScalar>::quiet_NaN(),
                               std::numeric_limits<SkScalar>::infinity()}) {
        auto invalidMultiplier = DecorationStyleFor(
                PrimaryStyle(), skia_text::TextDecoration::kUnderline, SK_ColorRED, multiplier);
        expectRejected(BuildParagraph("漢字", invalidMultiplier, {}), "invalid decoration multiplier");
    }
    auto background = PrimaryStyle();
    SkPaint backgroundPaint;
    backgroundPaint.setColor(SK_ColorRED);
    background.setBackgroundPaint(backgroundPaint);
    EXPECT_NO_THROW(BuildParagraph("漢字", background, {})->layoutWithInlineExtent(200));
    auto shadow = PrimaryStyle();
    shadow.addShadow(skia_text::TextShadow());
    EXPECT_NO_THROW(BuildParagraph("漢字", shadow, {})->layoutWithInlineExtent(200));
    auto baseline = PrimaryStyle();
    baseline.setBaselineShift(std::numeric_limits<SkScalar>::infinity());
    expectRejected(BuildParagraph("漢字", baseline, {}), "baseline shift");
    auto letterSpacing = PrimaryStyle();
    letterSpacing.setLetterSpacing(-1);
    expectRejected(BuildParagraph("漢字", letterSpacing, {}), "negative letter spacing");
    auto wordSpacing = PrimaryStyle();
    wordSpacing.setWordSpacing(-1);
    expectRejected(BuildParagraph("漢字", wordSpacing, {}), "negative word spacing");

    auto splitGrapheme = BuildCustomParagraph({}, [](skia_text::ParagraphBuilder* builder) {
        const std::string text = "A\u0301";
        auto first = LatinStyle();
        auto second = LatinStyle();
        second.setFontStyle(SkFontStyle::Bold());
        builder->pushStyle(first);
        builder->addText(text.data(), 1);
        builder->pop();
        builder->pushStyle(second);
        builder->addText(text.data() + 1, 2);
        builder->pop();
    });
    expectRejected(std::move(splitGrapheme), "style inside grapheme");
}

TEST_F(SkiaVerticalParagraph, V24b_LegacyHorizontalOnlyEntryPointsRejectWhileSourceQueriesWork) {
    auto paragraph = BuildParagraph("漢字ABC", PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(1000);
    EXPECT_THROW(paragraph->getMinIntrinsicWidth(), std::logic_error);
    EXPECT_THROW(paragraph->getMaxIntrinsicWidth(), std::logic_error);
    EXPECT_THROW(paragraph->getAlphabeticBaseline(), std::logic_error);
    EXPECT_THROW(paragraph->getIdeographicBaseline(), std::logic_error);
    std::vector<skia_text::LineMetrics> lineMetrics;
    EXPECT_THROW(paragraph->getLineMetrics(lineMetrics), std::logic_error);
    skia_text::LineMetrics lineMetric;
    EXPECT_THROW(paragraph->getLineMetricsAt(0, &lineMetric), std::logic_error);
    EXPECT_TRUE(paragraph->getRectsForPlaceholders().empty());
    auto visitor = [](int, const skia_text::Paragraph::VisitorInfo*) {};
    EXPECT_THROW(paragraph->visit(visitor), std::logic_error);
    const auto word = paragraph->getWordBoundary(0);
    EXPECT_LE(word.start, word.end);
    EXPECT_LE(word.end, Utf16Length("漢字ABC"));
}

TEST_F(SkiaVerticalParagraph, V40_BidiControlsDoNotLeakDirectionIntoVerticalRangesAndColumns) {
    const std::string text = "A\u202EBC\u202CD";
    auto paragraph = BuildParagraph(text, LatinStyle(32), {});
    EXPECT_NO_THROW(paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity()));
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_TRUE(MetricsAreFinite(metrics));

    const auto bBoxes = TightBoxes(paragraph.get(), 2, 3);
    const auto cBoxes = TightBoxes(paragraph.get(), 3, 4);
    ASSERT_EQ(bBoxes.size(), 1u);
    ASSERT_EQ(cBoxes.size(), 1u);
    EXPECT_NE(bBoxes.front().rect, cBoxes.front().rect);
    EXPECT_EQ(bBoxes.front().direction, skia_text::TextDirection::kLtr);
    EXPECT_EQ(cBoxes.front().direction, skia_text::TextDirection::kLtr);
    EXPECT_GT(bBoxes.front().rect.height(), 0);
    EXPECT_GT(cBoxes.front().rect.height(), 0);
    EXPECT_LE(bBoxes.front().rect.bottom(), cBoxes.front().rect.top() + kTolerance);

    skia_text::Paragraph::GlyphInfo glyphInfo;
    ASSERT_TRUE(paragraph->getGlyphInfoAtUTF16Offset(2, &glyphInfo));
    EXPECT_EQ(glyphInfo.fGraphemeClusterTextRange.start, 2u);
    EXPECT_EQ(glyphInfo.fGraphemeClusterTextRange.end, 3u);
    ASSERT_TRUE(paragraph->getGlyphInfoAtUTF16Offset(3, &glyphInfo));
    EXPECT_EQ(glyphInfo.fGraphemeClusterTextRange.start, 3u);
    EXPECT_EQ(glyphInfo.fGraphemeClusterTextRange.end, 4u);

    auto one = BuildParagraph("A", LatinStyle(32), {});
    one->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = one->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    const std::string columnsText = "A\u202EBC \u202CDE";
    auto columns = BuildParagraph(columnsText, LatinStyle(32), {});
    EXPECT_NO_THROW(columns->layoutWithInlineExtent(advance * 1.5f));
    const auto& columnMetrics = columns->getLayoutMetrics();
    ASSERT_GT(columnMetrics.columns.size(), 1u);
    EXPECT_TRUE(MetricsAreFinite(columnMetrics));
    ExpectContiguousSourceRanges(columnMetrics, Utf16Length(columnsText));
    for (const auto& column: columnMetrics.columns) {
        EXPECT_GE(column.inlineExtent, 0);
        EXPECT_GT(column.blockThickness, 0);
        EXPECT_GE(column.physicalBounds.left(), -kTolerance);
        EXPECT_LE(column.physicalBounds.right(), columnMetrics.physicalSize.width() + kTolerance);
        EXPECT_GE(column.physicalBounds.width(), 0);
    }

    const auto columnBBoxes = TightBoxes(columns.get(), 2, 3);
    const auto columnCBoxes = TightBoxes(columns.get(), 3, 4);
    ASSERT_FALSE(columnBBoxes.empty());
    ASSERT_FALSE(columnCBoxes.empty());
    EXPECT_NE(columnBBoxes.front().rect, columnCBoxes.front().rect);
    for (const auto& box: columnBBoxes) {
        EXPECT_GE(box.rect.left(), -kTolerance);
        EXPECT_LE(box.rect.right(), columnMetrics.physicalSize.width() + kTolerance);
        EXPECT_TRUE(std::isfinite(box.rect.top()));
        EXPECT_TRUE(std::isfinite(box.rect.bottom()));
    }
    for (const auto& box: columnCBoxes) {
        EXPECT_GE(box.rect.left(), -kTolerance);
        EXPECT_LE(box.rect.right(), columnMetrics.physicalSize.width() + kTolerance);
        EXPECT_TRUE(std::isfinite(box.rect.top()));
        EXPECT_TRUE(std::isfinite(box.rect.bottom()));
    }
}

TEST_F(SkiaVerticalParagraph, V41_TinyHalfLeadingKeepsPositiveThicknessAcrossClusters) {
    constexpr SkScalar kHeight = 1e-9f;
    constexpr SkScalar kFontSize = 32.0f;
    constexpr SkScalar kExpectedThickness = kHeight * kFontSize;
    const skia_text::TextStyle styles[] = {LatinStyle(kFontSize), PrimaryStyle(kFontSize)};
    for (const auto& baseStyle: styles) {
        auto style = baseStyle;
        style.setHeightOverride(true);
        style.setHeight(kHeight);
        style.setHalfLeading(true);
        for (const char* text: {"j", "jj", "gj"}) {
            SCOPED_TRACE(text);
            auto paragraph = BuildParagraph(text, style, {});
            EXPECT_NO_THROW(paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity()));
            const auto& metrics = paragraph->getLayoutMetrics();
            ASSERT_EQ(metrics.columns.size(), 1u);
            EXPECT_TRUE(MetricsAreFinite(metrics));
            EXPECT_GT(metrics.columns[0].blockThickness, 0);
            EXPECT_NEAR(metrics.columns[0].blockThickness, kExpectedThickness, 1e-12f);
            EXPECT_NEAR(metrics.physicalSize.width(), kExpectedThickness, 1e-12f);
            EXPECT_NO_THROW(TightBoxes(paragraph.get(), 0, Utf16Length(text)));
        }
    }
}

TEST_F(SkiaVerticalParagraph, V42_MixedRunsPreserveTinyHalfLeadingThickness) {
    constexpr SkScalar kHeight = 1e-8f;
    constexpr SkScalar kFontSize = 32.0f;
    constexpr SkScalar kExpectedThickness = kHeight * kFontSize;

    auto firstStyle = LatinStyle(kFontSize);
    firstStyle.setLocale(SkString("en"));
    firstStyle.setHeightOverride(true);
    firstStyle.setHeight(kHeight);
    firstStyle.setHalfLeading(true);
    auto secondStyle = firstStyle;
    secondStyle.setLocale(SkString("ja"));

    auto paragraph = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(firstStyle);
        builder->addText("j", 1);
        builder->pop();
        builder->pushStyle(secondStyle);
        builder->addText("j", 1);
        builder->pop();
    });
    EXPECT_NO_THROW(paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity()));
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_TRUE(MetricsAreFinite(metrics));
    EXPECT_NEAR(metrics.columns[0].blockThickness, kExpectedThickness, kExpectedThickness * 1e-6f);
    EXPECT_NEAR(metrics.physicalSize.width(), kExpectedThickness, kExpectedThickness * 1e-6f);
}

TEST_F(SkiaVerticalParagraph, V43_MixedRunThicknessOverflowFailsExplicitly) {
    constexpr SkScalar kHeight = 1.05e37f;
    constexpr SkScalar kFontSize = 32.0f;
    static_assert(kHeight * kFontSize < std::numeric_limits<SkScalar>::max());

    auto cjkStyle = PrimaryStyle(kFontSize);
    cjkStyle.setHeightOverride(true);
    cjkStyle.setHeight(kHeight);
    auto latinStyle = LatinStyle(kFontSize);
    latinStyle.setHeightOverride(true);
    latinStyle.setHeight(kHeight);

    auto paragraph = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(cjkStyle);
        builder->addText("漢", 3);
        builder->pop();
        builder->pushStyle(latinStyle);
        builder->addText("j", 1);
        builder->pop();
    });
    EXPECT_THROW(paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity()),
                 std::invalid_argument);
}

TEST_F(SkiaVerticalParagraph, V44_PixelIndentMovesOnlyTheFirstColumnAcrossWraps) {
    auto unit = BuildParagraph("天", PrimaryStyle(), {});
    unit->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = unit->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    constexpr SkScalar kIndent = 24;
    auto paragraph = BuildConfiguredParagraph("天地玄黃天地玄黃",
                                              PrimaryStyle(),
                                              [](skia_text::ParagraphStyle& style) {
                                                  style.setTextIndent(kIndent, false);
                                              });
    paragraph->layoutWithInlineExtent(advance * 3);
    const auto& metrics = paragraph->getLayoutMetrics();

    ASSERT_GE(metrics.columns.size(), 2u);
    EXPECT_NEAR(metrics.columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(metrics.columns[0].physicalBounds.bottom(), kIndent + metrics.columns[0].inlineExtent,
                kTolerance);
    for (size_t index = 1; index < metrics.columns.size(); ++index) {
        EXPECT_NEAR(metrics.columns[index].physicalBounds.top(), 0, kTolerance);
    }
    EXPECT_NEAR(metrics.physicalSize.height(), advance * 3, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));

    const auto boxes = TightBoxes(paragraph.get(), 0, 1);
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_NEAR(boxes.front().rect.top(), kIndent, kTolerance);

}

TEST_F(SkiaVerticalParagraph, V45_NegativeIndentSharesPainterPathVisitorAndRangeGeometry) {
    constexpr SkScalar kIndent = -30;
    auto plain = BuildParagraph("天", PrimaryStyle(), {});
    auto indented = BuildConfiguredParagraph("天",
                                             PrimaryStyle(),
                                             [](skia_text::ParagraphStyle& style) {
                                                 style.setTextIndent(kIndent, false);
                                             });
    plain->layoutWithInlineExtent(300);
    indented->layoutWithInlineExtent(300);

    const auto& plainMetrics = plain->getLayoutMetrics();
    const auto& metrics = indented->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_NEAR(metrics.columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(metrics.contentBounds.top(), kIndent, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));

    const auto plainBoxes = TightBoxes(plain.get(), 0, 1);
    const auto boxes = TightBoxes(indented.get(), 0, 1);
    ASSERT_EQ(plainBoxes.size(), 1u);
    ASSERT_EQ(boxes.size(), 1u);
    EXPECT_NEAR(boxes.front().rect.top(), plainBoxes.front().rect.top() + kIndent, kTolerance);
    EXPECT_NEAR(boxes.front().rect.left(), plainBoxes.front().rect.left(), kTolerance);

    const auto plainRuns = SnapshotRuns(plain.get());
    const auto runs = SnapshotRuns(indented.get());
    ASSERT_EQ(runs.size(), 1u);
    ASSERT_EQ(plainRuns.size(), 1u);
    EXPECT_NEAR(runs.front().origin.y(), plainRuns.front().origin.y() + kIndent, kTolerance);

    skia_text::Paragraph::GlyphInfo info;
    ASSERT_TRUE(indented->getGlyphInfoAtUTF16Offset(0, &info));
    EXPECT_NEAR(info.fGraphemeLayoutBounds.top(), kIndent, kTolerance);

    const SkRect plainPathBounds = CorePath(plain.get()).getBounds();
    const SkRect pathBounds = CorePath(indented.get()).getBounds();
    EXPECT_NEAR(pathBounds.top(), plainPathBounds.top() + kIndent, kTolerance + 0.5f);
    EXPECT_NEAR(pathBounds.left(), plainPathBounds.left(), kTolerance + 0.5f);

    const SkBitmap plainBitmap = RasterizeText(plain.get(), 0, 80, 200, 200);
    const SkBitmap bitmap = RasterizeText(indented.get(), 0, 80, 200, 200);
    const SkIRect plainInk = NonWhiteBounds(plainBitmap);
    const SkIRect ink = NonWhiteBounds(bitmap);
    EXPECT_FALSE(plainInk.isEmpty());
    EXPECT_FALSE(ink.isEmpty());
    EXPECT_NEAR(ink.top(), plainInk.top() + kIndent, 1.0f);
    EXPECT_NEAR(ink.left(), plainInk.left(), 1.0f);
}

TEST_F(SkiaVerticalParagraph, V46_PercentageIndentUsesHeightOnlyAndZeroesIntrinsicContribution) {
    auto unit = BuildParagraph("天", PrimaryStyle(), {});
    unit->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = unit->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    constexpr char kText[] = "天地玄黃天地";

    auto plain = BuildParagraph(kText, PrimaryStyle(), {});
    auto percent = BuildConfiguredParagraph(kText,
                                            PrimaryStyle(),
                                            [](skia_text::ParagraphStyle& style) {
                                                style.setTextIndent(25, true);
                                            });
    auto pixels = BuildConfiguredParagraph(kText,
                                           PrimaryStyle(),
                                           [&](skia_text::ParagraphStyle& style) {
                                               style.setTextIndent(advance, false);
                                           });
    plain->layoutWithInlineExtent(advance * 4);
    percent->layoutWithInlineExtent(advance * 4);
    pixels->layoutWithInlineExtent(advance * 4);

    const auto& plainMetrics = plain->getLayoutMetrics();
    const auto& percentMetrics = percent->getLayoutMetrics();
    const auto& pixelMetrics = pixels->getLayoutMetrics();
    ASSERT_EQ(percentMetrics.columns.size(), pixelMetrics.columns.size());
    for (size_t index = 0; index < percentMetrics.columns.size(); ++index) {
        EXPECT_NEAR(percentMetrics.columns[index].physicalBounds.top(),
                    pixelMetrics.columns[index].physicalBounds.top(), kTolerance);
        EXPECT_NEAR(percentMetrics.columns[index].inlineExtent,
                    pixelMetrics.columns[index].inlineExtent, kTolerance);
    }

    EXPECT_NEAR(percentMetrics.minIntrinsicInlineExtent,
                plainMetrics.minIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(percentMetrics.maxIntrinsicInlineExtent,
                plainMetrics.maxIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(pixelMetrics.minIntrinsicInlineExtent,
                plainMetrics.minIntrinsicInlineExtent + advance, kTolerance);
    EXPECT_NEAR(pixelMetrics.maxIntrinsicInlineExtent,
                plainMetrics.maxIntrinsicInlineExtent + advance, kTolerance);

    auto taller = BuildConfiguredParagraph(kText,
                                           PrimaryStyle(),
                                           [](skia_text::ParagraphStyle& style) {
                                               style.setTextIndent(25, true);
                                           });
    taller->layoutWithInlineExtent(advance * 8);
    EXPECT_NEAR(taller->getLayoutMetrics().columns[0].physicalBounds.top(),
                advance * 2, kTolerance);

    auto noWrapPercent = BuildConfiguredParagraph(kText,
                                                  PrimaryStyle(),
                                                  [](skia_text::ParagraphStyle& style) {
                                                      style.setTextIndent(25, true);
                                                  });
    auto noWrapPlain = BuildParagraph(kText, PrimaryStyle(), {});
    noWrapPercent->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    noWrapPlain->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    EXPECT_NEAR(noWrapPercent->getLayoutMetrics().columns[0].physicalBounds.top(), 0, kTolerance);
    EXPECT_NEAR(noWrapPercent->getLayoutMetrics().physicalSize.height(),
                noWrapPlain->getLayoutMetrics().physicalSize.height(), kTolerance);
}

TEST_F(SkiaVerticalParagraph, V47_AlignmentUsesTheFirstColumnAreaAfterIndent) {
    auto unit = BuildParagraph("天", PrimaryStyle(), {});
    unit->layoutWithInlineExtent(300);
    const SkScalar extent = unit->getLayoutMetrics().columns[0].inlineExtent;
    ASSERT_GT(extent, 0);
    constexpr SkScalar kHeight = 300;
    constexpr SkScalar kIndent = 30;

    auto build = [&](skia_text::TextAlign align, SkScalar indent) {
        return BuildConfiguredParagraph("天",
                                        PrimaryStyle(),
                                        [&](skia_text::ParagraphStyle& style) {
                                            style.setTextAlign(align);
                                            style.setTextIndent(indent, false);
                                        });
    };
    auto start = build(skia_text::TextAlign::kStart, kIndent);
    auto end = build(skia_text::TextAlign::kEnd, kIndent);
    auto center = build(skia_text::TextAlign::kCenter, kIndent);
    auto hangingStart = build(skia_text::TextAlign::kStart, -kIndent);
    auto hangingEnd = build(skia_text::TextAlign::kEnd, -kIndent);
    auto hangingCenter = build(skia_text::TextAlign::kCenter, -kIndent);
    for (auto* paragraph: {start.get(), end.get(), center.get(), hangingStart.get(), hangingEnd.get(), hangingCenter.get()}) {
        paragraph->layoutWithInlineExtent(kHeight);
    }

    EXPECT_NEAR(start->getLayoutMetrics().columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(end->getLayoutMetrics().columns[0].physicalBounds.top(),
                kHeight - extent, kTolerance);
    EXPECT_NEAR(center->getLayoutMetrics().columns[0].physicalBounds.top(),
                kIndent + (kHeight - kIndent - extent) * 0.5f, kTolerance);
    EXPECT_NEAR(hangingStart->getLayoutMetrics().columns[0].physicalBounds.top(),
                -kIndent, kTolerance);
    EXPECT_NEAR(hangingEnd->getLayoutMetrics().columns[0].physicalBounds.top(),
                kHeight - extent, kTolerance);
    EXPECT_NEAR(hangingCenter->getLayoutMetrics().columns[0].physicalBounds.top(),
                -kIndent + (kHeight + kIndent - extent) * 0.5f, kTolerance);

    const SkScalar overflowingIndent = kHeight - extent + 10;
    for (auto align: {skia_text::TextAlign::kStart,
                      skia_text::TextAlign::kEnd,
                      skia_text::TextAlign::kCenter}) {
        auto overflowing = build(align, overflowingIndent);
        overflowing->layoutWithInlineExtent(kHeight);
        const auto& metrics = overflowing->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 1u);
        EXPECT_NEAR(metrics.columns[0].physicalBounds.top(), overflowingIndent, kTolerance);
        EXPECT_NEAR(metrics.columns[0].inlineExtent, extent, kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, V48_HardBreaksEmptyColumnsAndDegenerateIndentsKeepColumnStructure) {
    constexpr SkScalar kIndent = 30;
    auto build = [&](const std::string& text,
                     size_t maxLines = std::numeric_limits<size_t>::max(),
                     SkScalar indent = kIndent) {
        return BuildConfiguredParagraph(text,
                                        PrimaryStyle(),
                                        [&](skia_text::ParagraphStyle& style) {
                                            style.setTextIndent(indent, false);
                                            style.setMaxLines(maxLines);
                                        });
    };

    auto hardBreak = build("漢\n字");
    hardBreak->layoutWithInlineExtent(300);
    const auto& hardMetrics = hardBreak->getLayoutMetrics();
    ASSERT_EQ(hardMetrics.columns.size(), 2u);
    EXPECT_NEAR(hardMetrics.columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(hardMetrics.columns[1].physicalBounds.top(), 0, kTolerance);
    const auto afterBreak = TightBoxes(hardBreak.get(), 2, 3);
    ASSERT_EQ(afterBreak.size(), 1u);
    EXPECT_NEAR(afterBreak.front().rect.top(), 0, kTolerance);

    auto leading = build("\n漢");
    leading->layoutWithInlineExtent(300);
    ASSERT_EQ(leading->getLayoutMetrics().columns.size(), 2u);
    EXPECT_NEAR(leading->getLayoutMetrics().columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(leading->getLayoutMetrics().columns[1].physicalBounds.top(), 0, kTolerance);

    auto consecutive = build("\n\n漢");
    consecutive->layoutWithInlineExtent(300);
    const auto& consecutiveMetrics = consecutive->getLayoutMetrics();
    ASSERT_EQ(consecutiveMetrics.columns.size(), 3u);
    EXPECT_NEAR(consecutiveMetrics.columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(consecutiveMetrics.columns[1].physicalBounds.top(), 0, kTolerance);
    EXPECT_NEAR(consecutiveMetrics.columns[2].physicalBounds.top(), 0, kTolerance);

    auto trailing = build("漢\n");
    trailing->layoutWithInlineExtent(300);
    ASSERT_EQ(trailing->getLayoutMetrics().columns.size(), 2u);
    EXPECT_NEAR(trailing->getLayoutMetrics().columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(trailing->getLayoutMetrics().columns[1].physicalBounds.top(), 0, kTolerance);

    auto empty = build("");
    empty->layoutWithInlineExtent(300);
    const auto& emptyMetrics = empty->getLayoutMetrics();
    ASSERT_EQ(emptyMetrics.columns.size(), 1u);
    EXPECT_EQ(emptyMetrics.longestInlineExtent, 0);
    EXPECT_TRUE(emptyMetrics.inkBounds.isEmpty());
    EXPECT_TRUE(emptyMetrics.contentBounds.isEmpty());

    auto truncated = build("天地玄黃", 1);
    truncated->layoutWithInlineExtent(10);
    ASSERT_EQ(truncated->getLayoutMetrics().columns.size(), 1u);
    EXPECT_TRUE(truncated->didExceedMaxLines());
    EXPECT_NEAR(truncated->getLayoutMetrics().columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_GT(truncated->getLayoutMetrics().columns[0].inlineExtent, 0);

    auto zeroHeight = build("天地", std::numeric_limits<size_t>::max(), kIndent);
    zeroHeight->layoutWithInlineExtent(0);
    const auto& zeroMetrics = zeroHeight->getLayoutMetrics();
    ASSERT_EQ(zeroMetrics.columns.size(), 2u);
    EXPECT_NEAR(zeroMetrics.physicalSize.height(), 0, kTolerance);
    EXPECT_NEAR(zeroMetrics.columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(zeroMetrics.columns[1].physicalBounds.top(), 0, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(zeroMetrics));

    auto unit = BuildParagraph("天", PrimaryStyle(), {});
    unit->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = unit->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    auto atOrPastHeight = build("天地", std::numeric_limits<size_t>::max(), advance * 2);
    atOrPastHeight->layoutWithInlineExtent(advance);
    const auto& atOrPastMetrics = atOrPastHeight->getLayoutMetrics();
    ASSERT_EQ(atOrPastMetrics.columns.size(), 2u);
    EXPECT_NEAR(atOrPastMetrics.columns[0].physicalBounds.top(), advance * 2, kTolerance);
    EXPECT_NEAR(atOrPastMetrics.columns[1].physicalBounds.top(), 0, kTolerance);
    EXPECT_NEAR(atOrPastMetrics.columns[0].inlineExtent, advance, kTolerance);

    auto percentZero = BuildConfiguredParagraph("天",
                                                PrimaryStyle(),
                                                [](skia_text::ParagraphStyle& style) {
                                                    style.setTextIndent(200, true);
                                                });
    percentZero->layoutWithInlineExtent(0);
    EXPECT_NEAR(percentZero->getLayoutMetrics().columns[0].physicalBounds.top(), 0, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V49_InvalidIndentValuesFailAndFiniteLayoutsRecover) {
    auto expectInvalid = [](std::unique_ptr<skia_text::Paragraph> paragraph, SkScalar height) {
        EXPECT_THROW(paragraph->layoutWithInlineExtent(height), std::invalid_argument);
        EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
        EXPECT_EQ(paragraph->getHeight(), 0);
        EXPECT_EQ(paragraph->getLongestLine(), 0);
    };

    for (SkScalar invalid: {std::numeric_limits<SkScalar>::quiet_NaN(),
                            std::numeric_limits<SkScalar>::infinity(),
                            -std::numeric_limits<SkScalar>::infinity()}) {
        expectInvalid(BuildConfiguredParagraph("漢",
                                               PrimaryStyle(),
                                               [&](skia_text::ParagraphStyle& style) {
                                                   style.setTextIndent(invalid, false);
                                               }),
                      300);
    }

    const SkScalar maximum = std::numeric_limits<SkScalar>::max();
    expectInvalid(BuildConfiguredParagraph("漢",
                                           PrimaryStyle(),
                                           [](skia_text::ParagraphStyle& style) {
                                               style.setTextIndent(std::numeric_limits<SkScalar>::max(), true);
                                           }),
                  maximum);
    expectInvalid(BuildConfiguredParagraph("漢",
                                           PrimaryStyle(),
                                           [](skia_text::ParagraphStyle& style) {
                                               style.setTextIndent(-std::numeric_limits<SkScalar>::max(), false);
                                           }),
                  maximum);

    auto recovering = BuildConfiguredParagraph("漢",
                                               PrimaryStyle(),
                                               [](skia_text::ParagraphStyle& style) {
                                                   style.setTextIndent(200, true);
                                               });
    EXPECT_THROW(recovering->layoutWithInlineExtent(maximum), std::invalid_argument);
    EXPECT_THROW(recovering->getLayoutMetrics(), std::logic_error);
    EXPECT_EQ(recovering->getHeight(), 0);
    EXPECT_EQ(recovering->getLongestLine(), 0);

    EXPECT_NO_THROW(recovering->layoutWithInlineExtent(300));
    EXPECT_NEAR(recovering->getLayoutMetrics().columns[0].physicalBounds.top(), 600, kTolerance);
    EXPECT_NEAR(recovering->getHeight(), 300, kTolerance);

    EXPECT_THROW(recovering->layoutWithInlineExtent(maximum), std::invalid_argument);
    EXPECT_NO_THROW(recovering->layoutWithInlineExtent(300));
    EXPECT_NEAR(recovering->getLayoutMetrics().columns[0].physicalBounds.top(), 600, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V50_IntrinsicMetricsIncludeIndentOnlyOnTheFirstHardFragmentAndZeroPercent) {
    auto unit = BuildParagraph("天", PrimaryStyle(), {});
    unit->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = unit->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    constexpr SkScalar kIndent = 30;

    auto plain = BuildParagraph("天\n天地玄黃", PrimaryStyle(), {});
    auto indented = BuildConfiguredParagraph("天\n天地玄黃",
                                             PrimaryStyle(),
                                             [](skia_text::ParagraphStyle& style) {
                                                 style.setTextIndent(kIndent, false);
                                             });
    auto percent = BuildConfiguredParagraph("天\n天地玄黃",
                                            PrimaryStyle(),
                                            [](skia_text::ParagraphStyle& style) {
                                                style.setTextIndent(10, true);
                                            });
    plain->layoutWithInlineExtent(300);
    indented->layoutWithInlineExtent(300);
    percent->layoutWithInlineExtent(300);

    const auto& plainMetrics = plain->getLayoutMetrics();
    const auto& indentedMetrics = indented->getLayoutMetrics();
    const auto& percentMetrics = percent->getLayoutMetrics();
    EXPECT_NEAR(indentedMetrics.minIntrinsicInlineExtent,
                plainMetrics.minIntrinsicInlineExtent + kIndent, kTolerance);
    EXPECT_NEAR(indentedMetrics.maxIntrinsicInlineExtent,
                plainMetrics.maxIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(percentMetrics.minIntrinsicInlineExtent,
                plainMetrics.minIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(percentMetrics.maxIntrinsicInlineExtent,
                plainMetrics.maxIntrinsicInlineExtent, kTolerance);

    auto firstLongerPlain = BuildParagraph("天地\n玄黃", PrimaryStyle(), {});
    auto firstLongerIndented = BuildConfiguredParagraph("天地\n玄黃",
                                                        PrimaryStyle(),
                                                        [](skia_text::ParagraphStyle& style) {
                                                            style.setTextIndent(kIndent, false);
                                                        });
    firstLongerPlain->layoutWithInlineExtent(300);
    firstLongerIndented->layoutWithInlineExtent(300);
    EXPECT_NEAR(firstLongerIndented->getLayoutMetrics().longestInlineExtent,
                firstLongerPlain->getLayoutMetrics().longestInlineExtent + kIndent, kTolerance);
    EXPECT_NEAR(firstLongerIndented->getLayoutMetrics().maxIntrinsicInlineExtent,
                firstLongerPlain->getLayoutMetrics().maxIntrinsicInlineExtent + kIndent, kTolerance);
    EXPECT_NEAR(firstLongerIndented->getLayoutMetrics().minIntrinsicInlineExtent,
                firstLongerPlain->getLayoutMetrics().minIntrinsicInlineExtent + kIndent, kTolerance);

    auto noWrapPlain = BuildParagraph("天地", PrimaryStyle(), {});
    auto noWrapIndented = BuildConfiguredParagraph("天地",
                                                   PrimaryStyle(),
                                                   [](skia_text::ParagraphStyle& style) {
                                                       style.setTextIndent(kIndent, false);
                                                   });
    noWrapPlain->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    noWrapIndented->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    EXPECT_NEAR(noWrapIndented->getLayoutMetrics().longestInlineExtent,
                noWrapPlain->getLayoutMetrics().longestInlineExtent + kIndent, kTolerance);
    EXPECT_NEAR(noWrapIndented->getLayoutMetrics().physicalSize.height(),
                noWrapPlain->getLayoutMetrics().physicalSize.height() + kIndent, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V51_CjkSoftWrappedColumnJustifiesAndSourceEndFallsBack) {
    const std::string text = "天地玄黃宇宙洪荒";
    const SkScalar advance = InfiniteInlineExtent("天", PrimaryStyle());
    ASSERT_GT(advance, 0);
    const SkScalar height = advance * 4 + 12;

    auto start = BuildParagraph(text, PrimaryStyle(), {});
    auto justified = BuildParagraph(text, PrimaryStyle(), {.align = skia_text::TextAlign::kJustify});
    start->layoutWithInlineExtent(height);
    justified->layoutWithInlineExtent(height);

    const auto& startMetrics = start->getLayoutMetrics();
    const auto& metrics = justified->getLayoutMetrics();
    ASSERT_EQ(startMetrics.columns.size(), 2u);
    ASSERT_EQ(metrics.columns.size(), 2u);
    ExpectContiguousSourceRanges(metrics, Utf16Length(text));
    EXPECT_NEAR(metrics.physicalSize.height(), height, kTolerance);
    EXPECT_NEAR(metrics.longestInlineExtent, height, kTolerance);
    EXPECT_NEAR(metrics.minIntrinsicInlineExtent, startMetrics.minIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(metrics.maxIntrinsicInlineExtent, startMetrics.maxIntrinsicInlineExtent, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));

    const auto columns = ImplOf(justified.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_EQ(columns[0].justificationOpportunityCount, 3u);
    EXPECT_NEAR(columns[0].justificationStep, 4.0f, kTolerance);
    EXPECT_NEAR(columns[0].inlineContentExtent, advance * 4, kTolerance);
    EXPECT_NEAR(columns[0].inlineExtent, height, kTolerance);
    ASSERT_EQ(columns[0].justificationShifts.size(), 5u);
    for (size_t index = 0; index < 4; ++index) {
        EXPECT_NEAR(columns[0].justificationShifts[index], 4.0f * index, kTolerance);
    }
    EXPECT_NEAR(columns[0].justificationShifts[4], 12.0f, kTolerance);
    EXPECT_FALSE(columns[1].justified);
    EXPECT_EQ(columns[1].justificationOpportunityCount, 0u);
    EXPECT_TRUE(columns[1].justificationShifts.empty());
    EXPECT_NEAR(metrics.columns[0].inlineExtent, height, kTolerance);
    EXPECT_NEAR(metrics.columns[1].inlineExtent, advance * 4, kTolerance);

    const auto startRuns = SnapshotRuns(start.get());
    const auto justifiedRuns = SnapshotRuns(justified.get());
    for (size_t index = 0; index < 4; ++index) {
        const SkPoint startOrigin = VisitorOriginAtUtf8(startRuns, 0, SkToU32(index * 3));
        const SkPoint justifiedOrigin = VisitorOriginAtUtf8(justifiedRuns, 0, SkToU32(index * 3));
        ASSERT_TRUE(SkIsFinite(startOrigin.y())) << index;
        ASSERT_TRUE(SkIsFinite(justifiedOrigin.y())) << index;
        EXPECT_NEAR(justifiedOrigin.y(), startOrigin.y() + 4.0f * index, kTolerance);
        EXPECT_NEAR(justifiedOrigin.x(), startOrigin.x(), kTolerance);
    }

    for (size_t index = 0; index < 4; ++index) {
        const auto startBox = TightBoxes(start.get(), index, index + 1);
        const auto justifiedBox = TightBoxes(justified.get(), index, index + 1);
        ASSERT_EQ(startBox.size(), 1u) << index;
        ASSERT_EQ(justifiedBox.size(), 1u) << index;
        EXPECT_NEAR(justifiedBox[0].rect.top(), startBox[0].rect.top() + 4.0f * index, kTolerance);
        const SkScalar expectedBottom = std::min(startBox[0].rect.bottom() + 4.0f * (index + 1),
                                                 metrics.columns[0].physicalBounds.bottom());
        EXPECT_NEAR(justifiedBox[0].rect.bottom(), expectedBottom, kTolerance);
    }

    const SkRect startPath = CorePath(start.get()).getBounds();
    const SkRect justifiedPath = CorePath(justified.get()).getBounds();
    EXPECT_NEAR(justifiedPath.bottom(), startPath.bottom() + 12.0f, 0.5f);
    EXPECT_NEAR(justifiedPath.top(), startPath.top(), 0.5f);

    const SkBitmap startBitmap = RasterizeText(start.get(), 0, 0, 200, 300);
    const SkBitmap justifiedBitmap = RasterizeText(justified.get(), 0, 0, 200, 300);
    const SkIRect startInk = NonWhiteBounds(startBitmap);
    const SkIRect justifiedInk = NonWhiteBounds(justifiedBitmap);
    EXPECT_FALSE(startInk.isEmpty());
    EXPECT_FALSE(justifiedInk.isEmpty());
    EXPECT_NEAR(justifiedInk.bottom(), startInk.bottom() + 12.0f, 1.0f);
    EXPECT_NEAR(justifiedInk.top(), startInk.top(), 1.0f);

    const SkScalar hitX = metrics.columns[0].physicalBounds.centerX();
    const SkScalar hitY = advance * 3 + advance * 0.5f + 2.0f;
    EXPECT_EQ(start->getGlyphPositionAtCoordinate(hitX, hitY).position, 4);
    EXPECT_EQ(justified->getGlyphPositionAtCoordinate(hitX, hitY).position, 3);
    EXPECT_NEAR(metrics.inkBounds.bottom(), startMetrics.inkBounds.bottom() + 12.0f, 0.5f);
}

TEST_F(SkiaVerticalParagraph, V52_HardBreakColumnsDoNotJustifyButFollowingSoftColumnsDo) {
    const SkScalar advance = InfiniteInlineExtent("天", PrimaryStyle());
    ASSERT_GT(advance, 0);
    const SkScalar height = advance * 4 + 12;
    auto paragraph = BuildParagraph("天地\n玄黃宇宙洪荒",
                                    PrimaryStyle(),
                                    {.align = skia_text::TextAlign::kJustify});
    paragraph->layoutWithInlineExtent(height);

    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 3u);
    EXPECT_TRUE(metrics.columns[0].hardBreak);
    ExpectContiguousSourceRanges(metrics, Utf16Length("天地\n玄黃宇宙洪荒"));

    const auto columns = ImplOf(paragraph.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 3u);
    EXPECT_TRUE(columns[0].hardBreak);
    EXPECT_FALSE(columns[0].justified);
    EXPECT_EQ(columns[0].justificationOpportunityCount, 0u);
    EXPECT_TRUE(columns[0].justificationShifts.empty());
    EXPECT_NEAR(columns[0].inlineExtent, advance * 2, kTolerance);
    EXPECT_TRUE(columns[1].justified);
    EXPECT_EQ(columns[1].justificationOpportunityCount, 3u);
    EXPECT_NEAR(columns[1].justificationStep, 4.0f, kTolerance);
    EXPECT_NEAR(columns[1].inlineExtent, height, kTolerance);
    EXPECT_FALSE(columns[2].justified);
    EXPECT_TRUE(columns[2].justificationShifts.empty());
    EXPECT_NEAR(columns[2].inlineExtent, advance * 2, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));
}

TEST_F(SkiaVerticalParagraph, V53_LatinJustificationExpandsOnlyInteriorWordWhitespace) {
    const std::string text = "one two supercalifragilisticexpialidocious";
    const auto style = LatinStyle(20);
    const SkScalar firstWordsWidth = InfiniteInlineExtent("one two ", style);
    const SkScalar fullWidth = InfiniteInlineExtent(text, style);
    ASSERT_GT(fullWidth, firstWordsWidth + 1);
    const SkScalar height = (firstWordsWidth + fullWidth) * 0.5f;

    auto start = BuildParagraph(text, style, {});
    auto justified = BuildParagraph(text, style, {.align = skia_text::TextAlign::kJustify});
    start->layoutWithInlineExtent(height);
    justified->layoutWithInlineExtent(height);

    const auto& startMetrics = start->getLayoutMetrics();
    const auto& metrics = justified->getLayoutMetrics();
    ASSERT_EQ(startMetrics.columns.size(), 2u);
    ASSERT_EQ(metrics.columns.size(), 2u);
    EXPECT_NEAR(metrics.minIntrinsicInlineExtent, startMetrics.minIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(metrics.maxIntrinsicInlineExtent, startMetrics.maxIntrinsicInlineExtent, kTolerance);

    const auto columns = ImplOf(justified.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_EQ(columns[0].justificationOpportunityCount, 1u);
    const SkScalar step = height - firstWordsWidth;
    EXPECT_NEAR(columns[0].justificationStep, step, kTolerance);
    EXPECT_NEAR(columns[0].inlineContentExtent, firstWordsWidth, kTolerance);
    EXPECT_NEAR(columns[0].inlineExtent, height, kTolerance);
    EXPECT_FALSE(columns[1].justified);

    const auto startRuns = SnapshotRuns(start.get());
    const auto justifiedRuns = SnapshotRuns(justified.get());
    for (uint32_t utf8: {0u, 1u, 2u, 3u}) {
        const SkPoint startOrigin = VisitorOriginAtUtf8(startRuns, 0, utf8);
        const SkPoint justifiedOrigin = VisitorOriginAtUtf8(justifiedRuns, 0, utf8);
        ASSERT_TRUE(SkIsFinite(startOrigin.y())) << utf8;
        EXPECT_NEAR(justifiedOrigin.y(), startOrigin.y(), kTolerance) << utf8;
    }
    for (uint32_t utf8: {4u, 5u, 6u}) {
        const SkPoint startOrigin = VisitorOriginAtUtf8(startRuns, 0, utf8);
        const SkPoint justifiedOrigin = VisitorOriginAtUtf8(justifiedRuns, 0, utf8);
        ASSERT_TRUE(SkIsFinite(startOrigin.y())) << utf8;
        EXPECT_NEAR(justifiedOrigin.y(), startOrigin.y() + step, kTolerance) << utf8;
    }
    EXPECT_TRUE(MetricsAreFinite(metrics));
}

TEST_F(SkiaVerticalParagraph, V54_KanaUsesScriptExtensionsAndPunctuationAddsNoOpportunity) {
    const auto style = PrimaryStyle();
    const SkScalar kanaAdvance = InfiniteInlineExtent("あーい", style);
    ASSERT_GT(kanaAdvance, 0);
    const SkScalar kanaHeight = kanaAdvance + 12;
    auto kana = BuildParagraph("あーいうえお", style, {.align = skia_text::TextAlign::kJustify});
    kana->layoutWithInlineExtent(kanaHeight);
    const auto kanaColumns = ImplOf(kana.get())->verticalColumns();
    ASSERT_GE(kanaColumns.size(), 2u);
    EXPECT_TRUE(kanaColumns[0].justified);
    EXPECT_EQ(kanaColumns[0].justificationOpportunityCount, 2u);
    EXPECT_NEAR(kanaColumns[0].justificationStep, 6.0f, kTolerance);
    EXPECT_NEAR(kanaColumns[0].inlineExtent, kanaHeight, kTolerance);

    const SkScalar punctuationAdvance = InfiniteInlineExtent("天、地", style);
    const SkScalar punctuationHeight = punctuationAdvance + 12;
    auto punctuation = BuildParagraph("天、地宇宙洪荒",
                                      style,
                                      {.align = skia_text::TextAlign::kJustify});
    punctuation->layoutWithInlineExtent(punctuationHeight);
    const auto punctuationColumns = ImplOf(punctuation.get())->verticalColumns();
    ASSERT_FALSE(punctuationColumns.empty());
    EXPECT_FALSE(punctuationColumns[0].justified);
    EXPECT_EQ(punctuationColumns[0].justificationOpportunityCount, 0u);
    EXPECT_TRUE(punctuationColumns[0].justificationShifts.empty());
    EXPECT_NEAR(punctuationColumns[0].inlineExtent, punctuationColumns[0].inlineContentExtent, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(punctuation->getLayoutMetrics()));
}

TEST_F(SkiaVerticalParagraph, V55_FirstColumnIndentReducesOnlyTheFirstJustificationLimit) {
    const SkScalar advance = InfiniteInlineExtent("天", PrimaryStyle());
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃宇宙洪荒天地玄黃";

    constexpr SkScalar kIndent = 24;
    const SkScalar indentedHeight = kIndent + advance * 4 + 12;
    auto indented = BuildConfiguredParagraph(text,
                                             PrimaryStyle(),
                                             [](skia_text::ParagraphStyle& style) {
                                                 style.setTextAlign(skia_text::TextAlign::kJustify);
                                                 style.setTextIndent(kIndent, false);
                                             });
    indented->layoutWithInlineExtent(indentedHeight);
    const auto& indentedMetrics = indented->getLayoutMetrics();
    ASSERT_EQ(indentedMetrics.columns.size(), 3u);
    EXPECT_NEAR(indentedMetrics.columns[0].physicalBounds.top(), kIndent, kTolerance);
    EXPECT_NEAR(indentedMetrics.columns[0].inlineExtent, indentedHeight - kIndent, kTolerance);
    EXPECT_NEAR(indentedMetrics.columns[1].physicalBounds.top(), 0, kTolerance);
    EXPECT_NEAR(indentedMetrics.columns[1].inlineExtent, indentedHeight, kTolerance);
    EXPECT_NEAR(indentedMetrics.longestInlineExtent, indentedHeight, kTolerance);
    const auto indentedColumns = ImplOf(indented.get())->verticalColumns();
    ASSERT_EQ(indentedColumns.size(), 3u);
    EXPECT_TRUE(indentedColumns[0].justified);
    EXPECT_NEAR(indentedColumns[0].justificationStep, 4.0f, kTolerance);
    EXPECT_TRUE(indentedColumns[1].justified);
    EXPECT_NEAR(indentedColumns[1].justificationStep, 12.0f, kTolerance);
    EXPECT_FALSE(indentedColumns[2].justified);

    const SkScalar percentHeight = advance * 4 + 12;
    auto percentage = BuildConfiguredParagraph("天地玄黃宇宙洪荒",
                                               PrimaryStyle(),
                                               [](skia_text::ParagraphStyle& style) {
                                                   style.setTextAlign(skia_text::TextAlign::kJustify);
                                                   style.setTextIndent(10, true);
                                               });
    percentage->layoutWithInlineExtent(percentHeight);
    const auto& percentageMetrics = percentage->getLayoutMetrics();
    const SkScalar percentageIndent = percentHeight * 0.1f;
    ASSERT_EQ(percentageMetrics.columns.size(), 3u);
    EXPECT_NEAR(percentageMetrics.columns[0].physicalBounds.top(), percentageIndent, kTolerance);
    EXPECT_NEAR(percentageMetrics.columns[0].inlineExtent, percentHeight - percentageIndent, kTolerance);
    const auto percentageColumns = ImplOf(percentage.get())->verticalColumns();
    ASSERT_EQ(percentageColumns.size(), 3u);
    EXPECT_TRUE(percentageColumns[0].justified);
    EXPECT_NEAR(percentageColumns[0].justificationStep,
                (percentHeight - percentageIndent - advance * 3) * 0.5f,
                kTolerance);
    EXPECT_TRUE(percentageColumns[1].justified);
    EXPECT_NEAR(percentageColumns[1].justificationStep,
                (percentHeight - advance * 4) / 3.0f,
                kTolerance);

    constexpr SkScalar kNegativeIndent = -30;
    const SkScalar negativeHeight = advance * 4 + 12;
    auto negative = BuildConfiguredParagraph("天地玄黃宇宙洪荒",
                                             PrimaryStyle(),
                                             [](skia_text::ParagraphStyle& style) {
                                                 style.setTextAlign(skia_text::TextAlign::kJustify);
                                                 style.setTextIndent(kNegativeIndent, false);
                                             });
    negative->layoutWithInlineExtent(negativeHeight);
    const auto& negativeMetrics = negative->getLayoutMetrics();
    ASSERT_EQ(negativeMetrics.columns.size(), 2u);
    EXPECT_NEAR(negativeMetrics.columns[0].physicalBounds.top(), kNegativeIndent, kTolerance);
    EXPECT_NEAR(negativeMetrics.columns[0].inlineExtent,
                negativeHeight - kNegativeIndent,
                kTolerance);
    const auto negativeColumns = ImplOf(negative.get())->verticalColumns();
    ASSERT_EQ(negativeColumns.size(), 2u);
    EXPECT_TRUE(negativeColumns[0].justified);
    EXPECT_NEAR(negativeColumns[0].justificationStep,
                (negativeHeight - kNegativeIndent - advance * 4) / 3.0f,
                kTolerance);
    EXPECT_FALSE(negativeColumns[1].justified);
}

TEST_F(SkiaVerticalParagraph, V56_NoWrapZeroHeightMaxLinesAndOverflowFallBackToStart) {
    const SkScalar advance = InfiniteInlineExtent("天", PrimaryStyle());
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃宇宙洪荒";

    auto noWrap = BuildParagraph(text, PrimaryStyle(), {.align = skia_text::TextAlign::kJustify});
    noWrap->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& noWrapMetrics = noWrap->getLayoutMetrics();
    ASSERT_EQ(noWrapMetrics.columns.size(), 1u);
    EXPECT_NEAR(noWrapMetrics.columns[0].inlineExtent, advance * 8, kTolerance);
    const auto noWrapColumns = ImplOf(noWrap.get())->verticalColumns();
    ASSERT_EQ(noWrapColumns.size(), 1u);
    EXPECT_FALSE(noWrapColumns[0].justified);
    EXPECT_TRUE(noWrapColumns[0].justificationShifts.empty());

    auto zero = BuildParagraph("天地玄黃", PrimaryStyle(), {.align = skia_text::TextAlign::kJustify});
    zero->layoutWithInlineExtent(0);
    const auto& zeroMetrics = zero->getLayoutMetrics();
    ASSERT_EQ(zeroMetrics.columns.size(), 4u);
    const auto zeroColumns = ImplOf(zero.get())->verticalColumns();
    for (size_t index = 0; index < zeroColumns.size(); ++index) {
        EXPECT_FALSE(zeroColumns[index].justified) << index;
        EXPECT_TRUE(zeroColumns[index].justificationShifts.empty()) << index;
        EXPECT_NEAR(zeroMetrics.columns[index].inlineExtent, advance, kTolerance) << index;
    }

    auto limited = BuildParagraph(text,
                                  PrimaryStyle(),
                                  {.align = skia_text::TextAlign::kJustify, .maxLines = 1});
    limited->layoutWithInlineExtent(advance * 2 + 12);
    const auto& limitedMetrics = limited->getLayoutMetrics();
    ASSERT_EQ(limitedMetrics.columns.size(), 1u);
    EXPECT_TRUE(limited->didExceedMaxLines());
    const auto limitedColumns = ImplOf(limited.get())->verticalColumns();
    ASSERT_EQ(limitedColumns.size(), 1u);
    EXPECT_TRUE(limitedColumns[0].justified);
    EXPECT_EQ(limitedColumns[0].justificationOpportunityCount, 1u);
    EXPECT_NEAR(limitedColumns[0].justificationStep, 12.0f, kTolerance);
    EXPECT_NEAR(limitedMetrics.columns[0].inlineExtent, advance * 2 + 12, kTolerance);

    const auto latin = LatinStyle(20);
    auto overflow = BuildParagraph("supercalifragilisticexpialidocious",
                                   latin,
                                   {.align = skia_text::TextAlign::kJustify});
    overflow->layoutWithInlineExtent(10);
    const auto& overflowMetrics = overflow->getLayoutMetrics();
    ASSERT_EQ(overflowMetrics.columns.size(), 1u);
    EXPECT_GT(overflowMetrics.columns[0].inlineExtent, 10);
    const auto overflowColumns = ImplOf(overflow.get())->verticalColumns();
    ASSERT_EQ(overflowColumns.size(), 1u);
    EXPECT_FALSE(overflowColumns[0].justified);
    EXPECT_TRUE(overflowColumns[0].justificationShifts.empty());
    EXPECT_TRUE(MetricsAreFinite(overflowMetrics));

    auto singleLine = BuildParagraph(text,
                                     PrimaryStyle(),
                                     {.align = skia_text::TextAlign::kJustify, .maxLines = 1});
    singleLine->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto& singleLineMetrics = singleLine->getLayoutMetrics();
    ASSERT_EQ(singleLineMetrics.columns.size(), 1u);
    EXPECT_FALSE(singleLine->didExceedMaxLines());
    EXPECT_NEAR(singleLineMetrics.columns[0].inlineExtent, advance * 8, kTolerance);
    const auto singleLineColumns = ImplOf(singleLine.get())->verticalColumns();
    ASSERT_EQ(singleLineColumns.size(), 1u);
    EXPECT_FALSE(singleLineColumns[0].justified);
    EXPECT_TRUE(singleLineColumns[0].justificationShifts.empty());

    auto empty = BuildParagraph("", PrimaryStyle(), {.align = skia_text::TextAlign::kJustify});
    empty->layoutWithInlineExtent(advance * 4);
    const auto& emptyMetrics = empty->getLayoutMetrics();
    ASSERT_EQ(emptyMetrics.columns.size(), 1u);
    const auto emptyColumns = ImplOf(empty.get())->verticalColumns();
    ASSERT_EQ(emptyColumns.size(), 1u);
    EXPECT_FALSE(emptyColumns[0].justified);
    EXPECT_TRUE(emptyColumns[0].justificationShifts.empty());
    EXPECT_TRUE(MetricsAreFinite(emptyMetrics));

    auto whitespace = BuildParagraph("   ", PrimaryStyle(), {.align = skia_text::TextAlign::kJustify});
    whitespace->layoutWithInlineExtent(advance * 4);
    const auto& whitespaceMetrics = whitespace->getLayoutMetrics();
    ASSERT_EQ(whitespaceMetrics.columns.size(), 1u);
    const auto whitespaceColumns = ImplOf(whitespace.get())->verticalColumns();
    ASSERT_EQ(whitespaceColumns.size(), 1u);
    EXPECT_FALSE(whitespaceColumns[0].justified);
    EXPECT_TRUE(whitespaceColumns[0].justificationShifts.empty());
    EXPECT_TRUE(MetricsAreFinite(whitespaceMetrics));
}

TEST_F(SkiaVerticalParagraph, V57_AlignUpdatesAndHeightChangesRecomputeJustification) {
    const SkScalar advance = InfiniteInlineExtent("天", PrimaryStyle());
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃宇宙洪荒";
    const SkScalar height = advance * 4 + 12;

    auto paragraph = BuildParagraph(text, PrimaryStyle(), {});
    paragraph->layoutWithInlineExtent(height);
    ASSERT_FALSE(ImplOf(paragraph.get())->verticalColumns()[0].justified);

    paragraph->updateTextAlign(skia_text::TextAlign::kJustify);
    paragraph->layoutWithInlineExtent(height);
    auto columns = ImplOf(paragraph.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_NEAR(columns[0].justificationStep, 4.0f, kTolerance);

    paragraph->updateTextAlign(skia_text::TextAlign::kStart);
    paragraph->layoutWithInlineExtent(height);
    columns = ImplOf(paragraph.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_FALSE(columns[0].justified);
    EXPECT_TRUE(columns[0].justificationShifts.empty());

    paragraph->updateTextAlign(skia_text::TextAlign::kJustify);
    paragraph->layoutWithInlineExtent(height + 12);
    columns = ImplOf(paragraph.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_NEAR(columns[0].justificationStep, 8.0f, kTolerance);
    EXPECT_NEAR(columns[0].inlineExtent, height + 12, kTolerance);

    EXPECT_THROW(paragraph->updateTextAlign(skia_text::TextAlign::kLeft), std::invalid_argument);
}

TEST_F(SkiaVerticalParagraph, V58_WhitespaceRunsCountOnceAndEdgesDoNotCreateOpportunities) {
    const auto style = LatinStyle(20);
    const std::string prefix = "  one  two  three  ";
    const std::string text = prefix + "supercalifragilisticexpialidocious";
    const SkScalar prefixWidth = InfiniteInlineExtent(prefix, style);
    const SkScalar fullWidth = InfiniteInlineExtent(text, style);
    ASSERT_GT(fullWidth, prefixWidth + 1);
    const SkScalar height = (prefixWidth + fullWidth) * 0.5f;

    auto start = BuildParagraph(text, style, {});
    auto justified = BuildParagraph(text, style, {.align = skia_text::TextAlign::kJustify});
    start->layoutWithInlineExtent(height);
    justified->layoutWithInlineExtent(height);
    const auto& metrics = justified->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);

    const auto columns = ImplOf(justified.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_EQ(columns[0].justificationOpportunityCount, 2u);
    const SkScalar step = (height - prefixWidth) / 2.0f;
    EXPECT_NEAR(columns[0].justificationStep, step, kTolerance);
    EXPECT_NEAR(columns[0].inlineContentExtent, prefixWidth, kTolerance);
    EXPECT_NEAR(columns[0].inlineExtent, height, kTolerance);
    EXPECT_FALSE(columns[1].justified);

    ASSERT_EQ(columns[0].justificationShifts.size(), columns[0].clusters.width() + 1);
    EXPECT_NEAR(columns[0].justificationShifts[0], 0.0f, kTolerance);
    // Leading spaces, the two spaces in each interior run, and the trailing
    // soft-break spaces all move only after their containing opportunity.
    EXPECT_NEAR(columns[0].justificationShifts[7], step, kTolerance);
    EXPECT_NEAR(columns[0].justificationShifts[12], step * 2.0f, kTolerance);
    EXPECT_NEAR(columns[0].justificationShifts[columns[0].clusters.width()], height - prefixWidth, kTolerance);

    const auto startRuns = SnapshotRuns(start.get());
    const auto runs = SnapshotRuns(justified.get());
    EXPECT_NEAR(VisitorOriginAtUtf8(runs, 0, 2).y() - VisitorOriginAtUtf8(startRuns, 0, 2).y(),
                0.0f, kTolerance);
    EXPECT_NEAR(VisitorOriginAtUtf8(runs, 0, 7).y() - VisitorOriginAtUtf8(startRuns, 0, 7).y(),
                step, kTolerance);
    EXPECT_NEAR(VisitorOriginAtUtf8(runs, 0, 12).y() - VisitorOriginAtUtf8(startRuns, 0, 12).y(),
                step * 2.0f, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));
}

TEST_F(SkiaVerticalParagraph, V59_AsianLatinBoundariesEachContributeOneOpportunity) {
    const std::string prefix = "天A地B宇";
    const std::string text = prefix + "宙洪荒";
    const auto style = PrimaryStyle();
    const SkScalar prefixWidth = InfiniteInlineExtent(prefix, style);
    ASSERT_GT(prefixWidth, 0);
    const SkScalar height = prefixWidth + 12;

    auto justified = BuildParagraph(text, style, {.align = skia_text::TextAlign::kJustify});
    justified->layoutWithInlineExtent(height);
    const auto& metrics = justified->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);

    const auto columns = ImplOf(justified.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_EQ(columns[0].clusters.width(), 5u);
    EXPECT_EQ(columns[0].justificationOpportunityCount, 4u);
    EXPECT_NEAR(columns[0].justificationStep, 3.0f, kTolerance);
    EXPECT_NEAR(columns[0].inlineContentExtent, prefixWidth, kTolerance);
    ASSERT_EQ(columns[0].justificationShifts.size(), 6u);
    for (size_t index = 0; index < 5; ++index) {
        EXPECT_NEAR(columns[0].justificationShifts[index], 3.0f * index, kTolerance) << index;
    }
    EXPECT_NEAR(columns[0].justificationShifts[5], 12.0f, kTolerance);
    EXPECT_FALSE(columns[1].justified);
    EXPECT_TRUE(MetricsAreFinite(metrics));
}

TEST_F(SkiaVerticalParagraph, V60_EmojiAndPunctuationRemainAtomicAndAddNoBoundaryOpportunities) {
    const std::string prefix = "天😀、地A";
    const std::string text = prefix + "宇宙洪荒";
    const auto style = PrimaryStyle();
    const SkScalar prefixWidth = InfiniteInlineExtent(prefix, style);
    ASSERT_GT(prefixWidth, 0);
    const SkScalar height = prefixWidth + 12;

    auto justified = BuildParagraph(text, style, {.align = skia_text::TextAlign::kJustify});
    justified->layoutWithInlineExtent(height);
    const auto columns = ImplOf(justified.get())->verticalColumns();
    ASSERT_EQ(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_EQ(columns[0].clusters.width(), 5u);
    EXPECT_EQ(columns[0].justificationOpportunityCount, 1u);
    EXPECT_NEAR(columns[0].justificationStep, 12.0f, kTolerance);
    ASSERT_EQ(columns[0].justificationShifts.size(), 6u);
    EXPECT_NEAR(columns[0].justificationShifts[0], 0.0f, kTolerance);
    EXPECT_NEAR(columns[0].justificationShifts[1], 0.0f, kTolerance);
    EXPECT_NEAR(columns[0].justificationShifts[2], 0.0f, kTolerance);
    EXPECT_NEAR(columns[0].justificationShifts[3], 0.0f, kTolerance);
    EXPECT_NEAR(columns[0].justificationShifts[4], 12.0f, kTolerance);
    EXPECT_NEAR(columns[0].justificationShifts[5], 12.0f, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(justified->getLayoutMetrics()));
}

TEST_F(SkiaVerticalParagraph, V61_WhitespaceAfterPunctuationIsAnOpportunityPerPrd62) {
    const std::string prefix = "天、 A ";
    const std::string text = prefix + "B宇宙洪荒";
    const auto style = PrimaryStyle();
    const SkScalar prefixWidth = InfiniteInlineExtent(prefix, style);
    ASSERT_GT(prefixWidth, 0);
    const SkScalar height = prefixWidth + 12;

    auto justified = BuildParagraph(text, style, {.align = skia_text::TextAlign::kJustify});
    justified->layoutWithInlineExtent(height);
    const auto& metrics = justified->getLayoutMetrics();
    ASSERT_GE(metrics.columns.size(), 2u);

    const auto columns = ImplOf(justified.get())->verticalColumns();
    ASSERT_GE(columns.size(), 2u);
    EXPECT_TRUE(columns[0].justified);
    EXPECT_EQ(columns[0].justificationOpportunityCount, 1u);
    EXPECT_NEAR(columns[0].justificationStep, 12.0f, kTolerance);
    EXPECT_NEAR(columns[0].inlineExtent, height, kTolerance);
    EXPECT_NEAR(metrics.columns[0].inlineExtent, height, kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));
}

TEST_F(SkiaVerticalParagraph, V62_EllipsisShapesUprightAndSidewaysAffixesAtClusterBoundaries) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    const std::string text = "天地玄黃";
    auto cjk = BuildEllipsisParagraph(text, style, 1);
    cjk->layoutWithInlineExtent(advance * 2 + 4);
    const auto& cjkMetrics = cjk->getLayoutMetrics();
    ASSERT_EQ(cjkMetrics.columns.size(), 1u);
    EXPECT_TRUE(cjk->didExceedMaxLines());
    EXPECT_EQ(cjkMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(cjkMetrics.columns[0].utf16End, 1u);
    EXPECT_EQ(cjkMetrics.columns[0].utf16EndExcludingWhitespace, 1u);
    EXPECT_GT(cjkMetrics.columns[0].inlineExtent, advance);
    EXPECT_LE(cjkMetrics.columns[0].inlineExtent, advance * 2 + 4 + kTolerance);

    const auto cjkRuns = SnapshotRuns(cjk.get());
    const auto cjkAffix = SyntheticRuns(cjkRuns);
    ASSERT_EQ(cjkAffix.size(), 1u);
    EXPECT_TRUE(IsIdentity(cjkAffix[0].transform));
    EXPECT_FALSE(cjkAffix[0].sideways);
    ASSERT_FALSE(cjkAffix[0].glyphs.empty());
    EXPECT_EQ(cjkAffix[0].starts.size(), cjkAffix[0].glyphs.size() + 1);
    EXPECT_EQ(cjkAffix[0].starts.front(), 3u);
    EXPECT_TRUE(std::all_of(cjkAffix[0].starts.begin(), cjkAffix[0].starts.end(),
                            [&](uint32_t start) { return start == 3u; }));
    EXPECT_NEAR(cjkAffix[0].origin.y(),
                cjkMetrics.columns[0].physicalBounds.top() + advance,
                kTolerance);
    for (const auto& run: cjkRuns) {
        if (IsSyntheticAffixRun(run)) {
            continue;
        }
        for (size_t index = 0; index + 1 < run.starts.size(); ++index) {
            EXPECT_LT(run.starts[index], 3u);
        }
    }

    auto latinAffix = BuildEllipsisParagraph(text, style, 1, "...");
    latinAffix->layoutWithInlineExtent(advance * 2 + 4);
    const auto& latinMetrics = latinAffix->getLayoutMetrics();
    ASSERT_EQ(latinMetrics.columns.size(), 1u);
    EXPECT_TRUE(latinAffix->didExceedMaxLines());
    EXPECT_LT(latinMetrics.columns[0].utf16End, 4u);
    const auto latinRuns = SyntheticRuns(SnapshotRuns(latinAffix.get()));
    ASSERT_EQ(latinRuns.size(), 1u);
    EXPECT_TRUE(IsClockwise(latinRuns[0].transform));
    EXPECT_TRUE(latinRuns[0].sideways);
    EXPECT_EQ(latinRuns[0].glyphs.size(), 3u);
    EXPECT_EQ(latinRuns[0].starts.size(), 4u);
    const uint32_t insertion = SkToU32(latinMetrics.columns[0].utf16End * 3);
    EXPECT_TRUE(std::all_of(latinRuns[0].starts.begin(), latinRuns[0].starts.end(),
                            [&](uint32_t start) { return start == insertion; }));
    for (size_t index = 1; index < latinRuns[0].positions.size(); ++index) {
        EXPECT_GE(latinRuns[0].positions[index].y(),
                  latinRuns[0].positions[index - 1].y() - kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, V63_UnbreakableLatinOverflowTruncatesWholeGraphemesWithoutMaxLineOverflow) {
    auto style = LatinStyle(20);
    style.setHeightOverride(true);
    style.setHeight(2.0f);
    const std::string text = "a\u0301bcdefghijklmnopqrstuvwxyz";

    auto clip = BuildParagraph(text, style, {.maxLines = 16});
    clip->layoutWithInlineExtent(60);
    ASSERT_EQ(clip->getLayoutMetrics().columns.size(), 1u);
    EXPECT_FALSE(clip->didExceedMaxLines());
    EXPECT_GT(clip->getLayoutMetrics().columns[0].inlineExtent, 60 + kTolerance);

    auto ellipsis = BuildEllipsisParagraph(text, style, 16);
    ellipsis->layoutWithInlineExtent(60);
    const auto& metrics = ellipsis->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_TRUE(ellipsis->didExceedMaxLines());
    EXPECT_GT(metrics.columns[0].utf16End, 0u);
    EXPECT_LT(metrics.columns[0].utf16End, Utf16Length(text));
    EXPECT_LE(metrics.columns[0].inlineExtent, 60 + kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));

    skia_text::Paragraph::GlyphInfo lastRetained;
    ASSERT_TRUE(ellipsis->getGlyphInfoAtUTF16Offset(metrics.columns[0].utf16End - 1,
                                                    &lastRetained));
    EXPECT_EQ(lastRetained.fGraphemeClusterTextRange.end,
              metrics.columns[0].utf16End);
    EXPECT_GT(SourceGlyphCount(SnapshotRuns(ellipsis.get())), 0u);
    EXPECT_TRUE(TightBoxes(ellipsis.get(),
                           metrics.columns[0].utf16End,
                           Utf16Length(text))
                        .empty());
}

TEST_F(SkiaVerticalParagraph, V64_ShortContentAndExactFitDoNotCreateAnAffix) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    auto shortClip = BuildParagraph("天地", style, {.maxLines = 8});
    auto shortEllipsis = BuildEllipsisParagraph("天地", style, 8);
    shortClip->layoutWithInlineExtent(advance * 8);
    shortEllipsis->layoutWithInlineExtent(advance * 8);
    EXPECT_FALSE(shortEllipsis->didExceedMaxLines());
    EXPECT_TRUE(SyntheticRuns(SnapshotRuns(shortEllipsis.get())).empty());
    ExpectSnapshotsNear(Capture(shortClip.get()), Capture(shortEllipsis.get()));

    auto exactClip = BuildParagraph("天", style, {.maxLines = 8});
    auto exactEllipsis = BuildEllipsisParagraph("天", style, 8);
    exactClip->layoutWithInlineExtent(advance);
    exactEllipsis->layoutWithInlineExtent(advance);
    EXPECT_FALSE(exactEllipsis->didExceedMaxLines());
    EXPECT_TRUE(SyntheticRuns(SnapshotRuns(exactEllipsis.get())).empty());
    ExpectSnapshotsNear(Capture(exactClip.get()), Capture(exactEllipsis.get()));

    auto exactAffix = BuildEllipsisParagraph("天地玄", style, 1);
    exactAffix->layoutWithInlineExtent(advance * 2);
    const auto& exactAffixMetrics = exactAffix->getLayoutMetrics();
    ASSERT_EQ(exactAffixMetrics.columns.size(), 1u);
    EXPECT_TRUE(exactAffix->didExceedMaxLines());
    EXPECT_EQ(exactAffixMetrics.columns[0].utf16End, 1u);
    EXPECT_NEAR(exactAffixMetrics.columns[0].inlineExtent, advance * 2, kTolerance);
    EXPECT_EQ(SyntheticRuns(SnapshotRuns(exactAffix.get())).size(), 1u);
}

TEST_F(SkiaVerticalParagraph, V65_MaxLinesEllipsizesOnlyTheLastAllowedColumnAndHidesTheRest) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    const std::string text = "天地玄黃宇宙";
    auto clip = BuildParagraph(text, style, {.maxLines = 2});
    clip->layoutWithInlineExtent(advance * 2);
    auto ellipsis = BuildEllipsisParagraph(text, style, 2);
    ellipsis->layoutWithInlineExtent(advance * 2);
    const auto& clipMetrics = clip->getLayoutMetrics();
    const auto& metrics = ellipsis->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    ASSERT_EQ(clipMetrics.columns.size(), 2u);
    EXPECT_TRUE(ellipsis->didExceedMaxLines());
    EXPECT_EQ(metrics.columns[0].utf16End, 2u);
    EXPECT_EQ(metrics.columns[1].utf16End, 3u);
    EXPECT_NEAR(metrics.physicalSize.width(), clipMetrics.physicalSize.width(), kTolerance);
    EXPECT_NEAR(metrics.columns[0].blockThickness,
                clipMetrics.columns[0].blockThickness,
                kTolerance);
    EXPECT_NEAR(metrics.columns[1].inlineExtent, advance * 2, kTolerance);

    const auto runs = SnapshotRuns(ellipsis.get());
    const auto affix = SyntheticRuns(runs);
    ASSERT_EQ(affix.size(), 1u);
    EXPECT_EQ(affix[0].line, 1);
    EXPECT_TRUE(std::none_of(runs.begin(), runs.end(), [](const VisitorRun& run) {
        return IsSyntheticAffixRun(run) && run.line == 0;
    }));

    EXPECT_EQ(ellipsis->getLineNumberAtUTF16Offset(4), -1);
    EXPECT_TRUE(TightBoxes(ellipsis.get(), 3, 6).empty());
    EXPECT_FALSE(TightBoxes(ellipsis.get(), 2, 6).empty());
    skia_text::Paragraph::GlyphInfo hidden;
    EXPECT_FALSE(ellipsis->getGlyphInfoAtUTF16Offset(4, &hidden));
}

TEST_F(SkiaVerticalParagraph, V66_HardBreaksTrailingWhitespaceAndEmptyColumnsUseTheTrimmedInsertionPoint) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);

    for (const std::string text: {std::string("天地 \n"), std::string("天地\n")}) {
        SCOPED_TRACE(text);
        auto paragraph = BuildEllipsisParagraph(text, style, 1);
        paragraph->layoutWithInlineExtent(advance * 8);
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 1u);
        EXPECT_TRUE(paragraph->didExceedMaxLines());
        EXPECT_TRUE(metrics.columns[0].hardBreak);
        EXPECT_EQ(metrics.columns[0].utf16End, 2u);
        EXPECT_EQ(metrics.columns[0].utf16EndExcludingWhitespace, 2u);
        const auto affix = SyntheticRuns(SnapshotRuns(paragraph.get()));
        ASSERT_EQ(affix.size(), 1u);
        EXPECT_EQ(affix[0].starts.front(), 6u);
        EXPECT_NEAR(affix[0].origin.y(), metrics.columns[0].physicalBounds.top() + advance * 2,
                    kTolerance);
        EXPECT_TRUE(TightBoxes(paragraph.get(), 2, 4).empty());
    }

    auto trailingEmpty = BuildEllipsisParagraph("天\n", style, 1);
    trailingEmpty->layoutWithInlineExtent(advance * 8);
    const auto& trailingMetrics = trailingEmpty->getLayoutMetrics();
    ASSERT_EQ(trailingMetrics.columns.size(), 1u);
    EXPECT_TRUE(trailingEmpty->didExceedMaxLines());
    EXPECT_EQ(trailingMetrics.columns[0].utf16End, 1u);
    EXPECT_EQ(SyntheticRuns(SnapshotRuns(trailingEmpty.get())).size(), 1u);

    auto onlyBreak = BuildEllipsisParagraph("\n", style, 1);
    onlyBreak->layoutWithInlineExtent(advance * 8);
    const auto& breakMetrics = onlyBreak->getLayoutMetrics();
    ASSERT_EQ(breakMetrics.columns.size(), 1u);
    EXPECT_TRUE(onlyBreak->didExceedMaxLines());
    EXPECT_EQ(breakMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(breakMetrics.columns[0].utf16End, 0u);
    const auto breakAffix = SyntheticRuns(SnapshotRuns(onlyBreak.get()));
    ASSERT_EQ(breakAffix.size(), 1u);
    EXPECT_EQ(breakAffix[0].starts.front(), 0u);
    EXPECT_NEAR(breakAffix[0].origin.y(), breakMetrics.columns[0].physicalBounds.top(), kTolerance);
    EXPECT_TRUE(TightBoxes(onlyBreak.get(), 0, 1).empty());
}

TEST_F(SkiaVerticalParagraph, V67_IndentReducesOnlyTheAvailableLimitAndNeverMovesTheAffixTwice) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃";
    const SkScalar height = 140;

    struct Case {
        SkScalar value;
        bool percentage;
        SkScalar indent;
    };
    const Case cases[] = {
            {24, false, 24},
            {25, true, 35},
            {-20, false, -20},
    };
    for (const auto& test: cases) {
        SCOPED_TRACE(test.value);
        auto paragraph = BuildConfiguredParagraph(text,
                                                  style,
                                                  [&](skia_text::ParagraphStyle& paragraphStyle) {
                                                      paragraphStyle.setMaxLines(1);
                                                      paragraphStyle.setEllipsis(SkString("\xE2\x80\xA6"));
                                                      paragraphStyle.setTextIndent(test.value,
                                                                                  test.percentage);
                                                  });
        paragraph->layoutWithInlineExtent(height);
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 1u);
        EXPECT_TRUE(paragraph->didExceedMaxLines());
        EXPECT_NEAR(metrics.columns[0].physicalBounds.top(), test.indent, kTolerance);
        const SkScalar limit = height - test.indent;
        EXPECT_LE(metrics.columns[0].inlineExtent, limit + kTolerance);
        const auto affix = SyntheticRuns(SnapshotRuns(paragraph.get()));
        ASSERT_EQ(affix.size(), 1u);
        const SkScalar retained = static_cast<SkScalar>(metrics.columns[0].utf16End) * advance;
        EXPECT_NEAR(affix[0].origin.y(), test.indent + retained, kTolerance);
        if (!SkScalarNearlyZero(test.indent)) {
            EXPECT_GT(std::abs(affix[0].origin.y() - (test.indent * 2 + retained)), 1.0f);
        }
    }
}

TEST_F(SkiaVerticalParagraph, V68_JustifyEllipsizedColumnFallsBackToStartWhileOtherColumnsJustify) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const SkScalar height = advance * 2 + 16;
    const std::string text = "天地玄黃宇宙洪荒";

    auto start = BuildParagraph(text, style, {.align = skia_text::TextAlign::kStart, .maxLines = 2});
    auto justified = BuildEllipsisParagraph(text,
                                            style,
                                            2,
                                            "\xE2\x80\xA6",
                                            skia_text::TextAlign::kJustify);
    start->layoutWithInlineExtent(height);
    justified->layoutWithInlineExtent(height);
    const auto& startMetrics = start->getLayoutMetrics();
    const auto& metrics = justified->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    EXPECT_TRUE(justified->didExceedMaxLines());
    EXPECT_EQ(metrics.columns[0].utf16End, 2u);
    EXPECT_EQ(metrics.columns[1].utf16End, 3u);
    EXPECT_NEAR(metrics.minIntrinsicInlineExtent, startMetrics.minIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(metrics.maxIntrinsicInlineExtent, startMetrics.maxIntrinsicInlineExtent, kTolerance);

    const auto startBoxes = TightBoxes(start.get(), 1, 2);
    const auto justifiedBoxes = TightBoxes(justified.get(), 1, 2);
    ASSERT_EQ(startBoxes.size(), 1u);
    ASSERT_EQ(justifiedBoxes.size(), 1u);
    EXPECT_GT(justifiedBoxes.front().rect.top(), startBoxes.front().rect.top() + 10);

    const auto affix = SyntheticRuns(SnapshotRuns(justified.get()));
    ASSERT_EQ(affix.size(), 1u);
    EXPECT_EQ(affix[0].line, 1);
    EXPECT_NEAR(affix[0].origin.y(), metrics.columns[1].physicalBounds.top() + advance, kTolerance);
    EXPECT_NEAR(metrics.columns[1].inlineExtent, advance * 2, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V69_CustomAtomicEmptyAndFallbackAffixesUseTheInsertionStyle) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃";
    const SkScalar height = advance * 2 + 4;

    auto multi = BuildEllipsisParagraph(text, style, 1, "ab\xE2\x80\xA6");
    multi->layoutWithInlineExtent(advance * 2 + 44);
    const auto& multiMetrics = multi->getLayoutMetrics();
    ASSERT_EQ(multiMetrics.columns.size(), 1u);
    EXPECT_TRUE(multi->didExceedMaxLines());
    EXPECT_LE(multiMetrics.columns[0].inlineExtent, advance * 2 + 44 + kTolerance);
    const auto multiRuns = SyntheticRuns(SnapshotRuns(multi.get()));
    ASSERT_GE(multiRuns.size(), 2u);
    size_t multiGlyphs = 0;
    for (const auto& run: multiRuns) {
        EXPECT_EQ(run.line, 0);
        EXPECT_EQ(run.starts.size(), run.glyphs.size() + 1);
        EXPECT_TRUE(std::all_of(run.starts.begin(), run.starts.end(), [&](uint32_t start) {
            return start == multiRuns.front().starts.front();
        }));
        multiGlyphs += run.glyphs.size();
    }
    EXPECT_EQ(multiGlyphs, 3u);

    auto empty = BuildEllipsisParagraph(text, style, 1, "");
    auto clip = BuildParagraph(text, style, {.maxLines = 1});
    empty->layoutWithInlineExtent(height);
    clip->layoutWithInlineExtent(height);
    EXPECT_TRUE(empty->didExceedMaxLines());
    EXPECT_TRUE(SyntheticRuns(SnapshotRuns(empty.get())).empty());
    ExpectSnapshotsNear(Capture(clip.get()), Capture(empty.get()));

    auto fallback = BuildEllipsisParagraph(text, style, 1, "\U0001F600");
    fallback->layoutWithInlineExtent(advance * 2 + 44);
    const auto fallbackRuns = SyntheticRuns(SnapshotRuns(fallback.get()));
    ASSERT_FALSE(fallbackRuns.empty());
    const auto sourceRuns = SnapshotRuns(fallback.get());
    const auto source = std::find_if(sourceRuns.begin(), sourceRuns.end(), [](const VisitorRun& run) {
        return !IsSyntheticAffixRun(run);
    });
    ASSERT_NE(source, sourceRuns.end());
    ASSERT_NE(source->font.getTypeface(), nullptr);
    ASSERT_NE(fallbackRuns.front().font.getTypeface(), nullptr);
    EXPECT_NE(fallbackRuns.front().font.getTypeface()->uniqueID(),
              source->font.getTypeface()->uniqueID());
    EXPECT_NE(fallbackRuns.front().glyphs.front(), 0);
    EXPECT_NEAR(fallbackRuns.front().font.getSize(), 48, kTolerance);

    auto firstStyle = PrimaryStyle(20);
    auto secondStyle = PrimaryStyle(48);
    auto styled = BuildCustomParagraph(
            {.maxLines = 1, .ellipsis = "\xE2\x80\xA6"},
            [&](skia_text::ParagraphBuilder* builder) {
                builder->pushStyle(firstStyle);
                builder->addText("bb", 2);
                builder->pop();
                builder->pushStyle(secondStyle);
                builder->addText("AA", 2);
                builder->pop();
            });
    styled->layoutWithInlineExtent(65);
    const auto& styledMetrics = styled->getLayoutMetrics();
    ASSERT_EQ(styledMetrics.columns.size(), 1u);
    EXPECT_EQ(styledMetrics.columns[0].utf16End, 1u);
    const auto styledAffix = SyntheticRuns(SnapshotRuns(styled.get()));
    ASSERT_EQ(styledAffix.size(), 1u);
    EXPECT_NEAR(styledAffix[0].font.getSize(), 20, kTolerance);
}

TEST_F(SkiaVerticalParagraph, V70_TinyZeroAndNoSpaceAffixesAreAllOrNothing) {
    const auto style = PrimaryStyle();
    const std::string text = "天地玄黃";
    for (SkScalar height: {SkScalar(0), SkScalar(10)}) {
        SCOPED_TRACE(height);
        auto paragraph = BuildEllipsisParagraph(text, style, 1);
        paragraph->layoutWithInlineExtent(height);
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 1u);
        EXPECT_TRUE(paragraph->didExceedMaxLines());
        EXPECT_EQ(metrics.columns[0].utf16Start, 0u);
        EXPECT_EQ(metrics.columns[0].utf16End, 0u);
        EXPECT_EQ(metrics.columns[0].inlineExtent, 0u);
        EXPECT_GT(metrics.columns[0].blockThickness, 0);
        EXPECT_TRUE(SyntheticRuns(SnapshotRuns(paragraph.get())).empty());
        EXPECT_EQ(SourceGlyphCount(SnapshotRuns(paragraph.get())), 0u);
        EXPECT_TRUE(CorePath(paragraph.get()).isEmpty());
        EXPECT_TRUE(metrics.inkBounds.isEmpty());
        EXPECT_NEAR(metrics.physicalSize.height(), height, kTolerance);
        EXPECT_TRUE(MetricsAreFinite(metrics));
    }

    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    auto exactAffix = BuildEllipsisParagraph("天地", style, 1);
    exactAffix->layoutWithInlineExtent(advance);
    const auto& exactMetrics = exactAffix->getLayoutMetrics();
    ASSERT_EQ(exactMetrics.columns.size(), 1u);
    EXPECT_TRUE(exactAffix->didExceedMaxLines());
    EXPECT_EQ(exactMetrics.columns[0].utf16End, 0u);
    EXPECT_NEAR(exactMetrics.columns[0].inlineExtent, advance, kTolerance);
    EXPECT_EQ(SyntheticRuns(SnapshotRuns(exactAffix.get())).size(), 1u);
    EXPECT_EQ(SourceGlyphCount(SnapshotRuns(exactAffix.get())), 0u);
}

TEST_F(SkiaVerticalParagraph, V71_EllipsisMetricsKeepIntrinsicColumnAndPhysicalWidthContracts) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃";
    const SkScalar height = advance * 2;

    auto clip = BuildParagraph(text, style, {.maxLines = 1});
    auto ellipsis = BuildEllipsisParagraph(text, style, 1);
    clip->layoutWithInlineExtent(height);
    ellipsis->layoutWithInlineExtent(height);
    const auto& clipMetrics = clip->getLayoutMetrics();
    const auto& metrics = ellipsis->getLayoutMetrics();

    EXPECT_NEAR(metrics.physicalSize.width(), clipMetrics.physicalSize.width(), kTolerance);
    EXPECT_NEAR(metrics.physicalSize.height(), height, kTolerance);
    EXPECT_LE(metrics.longestInlineExtent, height + kTolerance);
    EXPECT_NEAR(metrics.minIntrinsicInlineExtent,
                clipMetrics.minIntrinsicInlineExtent,
                kTolerance);
    EXPECT_NEAR(metrics.maxIntrinsicInlineExtent,
                clipMetrics.maxIntrinsicInlineExtent,
                kTolerance);
    EXPECT_NEAR(metrics.columns[0].blockThickness,
                clipMetrics.columns[0].blockThickness,
                kTolerance);
    EXPECT_LE(metrics.contentBounds.height(), height + kTolerance);
    EXPECT_TRUE(MetricsAreFinite(metrics));
}

TEST_F(SkiaVerticalParagraph, V72_PaintPathVisitorRangeAndHitUseTheFinalAffixGeometry) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃";
    auto paragraph = BuildEllipsisParagraph(text, style, 1);
    paragraph->layoutWithInlineExtent(advance * 2);
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    const auto runs = SnapshotRuns(paragraph.get());
    const auto affix = SyntheticRuns(runs);
    ASSERT_EQ(affix.size(), 1u);
    const auto expectedInk = VisitorInkBounds(runs);
    ASSERT_TRUE(expectedInk.has_value());
    const SkPath path = CorePath(paragraph.get());
    ASSERT_FALSE(path.isEmpty());
    const SkRect pathBounds = path.getBounds();
    EXPECT_NEAR(pathBounds.left(), expectedInk->left(), 1.5f);
    EXPECT_NEAR(pathBounds.top(), expectedInk->top(), 1.5f);
    EXPECT_NEAR(pathBounds.right(), expectedInk->right(), 1.5f);
    EXPECT_NEAR(pathBounds.bottom(), expectedInk->bottom(), 1.5f);

    constexpr int width = 140;
    constexpr int height = 220;
    const auto canvasBitmap = RasterizeText(paragraph.get(), 0, 0, width, height);
    EXPECT_GT(NonWhitePixels(canvasBitmap), 0u);
    const auto pathBitmap = RasterizePath(path, width, height);
    const size_t totalPixels = static_cast<size_t>(width) * height;
    EXPECT_LT(DifferingPixels(canvasBitmap, pathBitmap), totalPixels / 20);

    const size_t insertionUtf16 = Utf16OffsetForUtf8(text, 3);
    EXPECT_EQ(metrics.columns[0].utf16End, insertionUtf16);
    EXPECT_TRUE(TightBoxes(paragraph.get(), insertionUtf16, Utf16Length(text)).empty());
    const auto retainedBoxes = TightBoxes(paragraph.get(), 0, Utf16Length(text));
    ASSERT_EQ(retainedBoxes.size(), 1u);
    EXPECT_LE(retainedBoxes.front().rect.bottom(),
              metrics.columns[0].physicalBounds.top() + advance + kTolerance);

    const SkScalar hitX = metrics.columns[0].physicalBounds.centerX();
    const SkScalar hitY = affix[0].origin.y() + 1.0f;
    const auto hit = paragraph->getGlyphPositionAtCoordinate(hitX, hitY);
    EXPECT_EQ(hit.position, insertionUtf16);
    EXPECT_EQ(hit.affinity, skia_text::Affinity::kDownstream);
}

TEST_F(SkiaVerticalParagraph, V73_EllipsisRelayoutAndParagraphCacheNeverReuseStaleAffixes) {
    const auto style = PrimaryStyle();
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃";

    auto paragraph = BuildEllipsisParagraph(text, style, 1);
    paragraph->layoutWithInlineExtent(advance * 2);
    const auto truncated = Capture(paragraph.get());
    ASSERT_EQ(truncated.columns.size(), 1u);
    EXPECT_EQ(truncated.columns[0].utf16End, 1u);
    EXPECT_TRUE(truncated.exceededMaxLines);
    EXPECT_EQ(SyntheticRuns(truncated.runs).size(), 1u);
    EXPECT_EQ(SourceGlyphCount(truncated.runs), 1u);

    paragraph->layoutWithInlineExtent(advance * 8);
    const auto recovered = Capture(paragraph.get());
    ASSERT_EQ(recovered.columns.size(), 1u);
    EXPECT_EQ(recovered.columns[0].utf16End, 4u);
    EXPECT_FALSE(recovered.exceededMaxLines);
    EXPECT_TRUE(SyntheticRuns(recovered.runs).empty());
    EXPECT_EQ(SourceGlyphCount(recovered.runs), 4u);

    paragraph->layoutWithInlineExtent(advance * 2);
    ExpectSnapshotsNear(truncated, Capture(paragraph.get()));
    EXPECT_THROW(paragraph->layoutWithInlineExtent(-1), std::invalid_argument);
    EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    paragraph->layoutWithInlineExtent(advance * 2);
    ExpectSnapshotsNear(truncated, Capture(paragraph.get()));

    auto defaultAffix = BuildEllipsisParagraph(text, style, 1);
    auto latinAffix = BuildEllipsisParagraph(text, style, 1, "...");
    defaultAffix->layoutWithInlineExtent(advance * 2);
    latinAffix->layoutWithInlineExtent(advance * 2);
    const auto defaultRuns = SyntheticRuns(SnapshotRuns(defaultAffix.get()));
    const auto latinRuns = SyntheticRuns(SnapshotRuns(latinAffix.get()));
    ASSERT_FALSE(defaultRuns.empty());
    ASSERT_FALSE(latinRuns.empty());
    EXPECT_NE(CountGlyphs(defaultRuns), CountGlyphs(latinRuns));
    EXPECT_TRUE(IsIdentity(defaultRuns.front().transform));
    EXPECT_TRUE(IsClockwise(latinRuns.front().transform));
}

TEST_F(SkiaVerticalParagraph, V74_CjkUnderlineUsesPhysicalLeftAndActualPixels) {
    constexpr SkScalar kSize = 48;
    auto style = DecorationStyleFor(PrimaryStyle(kSize), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto paragraph = BuildParagraph("天", style, {});
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 1u);
    const auto decorations = RecordDecorations(paragraph.get());
    ASSERT_EQ(decorations.size(), 1u);

    const auto& run = runs.front();
    const SkScalar thickness = DecorationThickness(run, skia_text::TextDecoration::kUnderline, 1);
    ASSERT_GT(thickness, 0);
    const auto cluster = SourceClusterCell(paragraph.get(), 0);
    const SkRect expected = SkRect::MakeLTRB(UnderlineCenterX(run, thickness) - thickness * 0.5f,
                                             cluster.top(),
                                             UnderlineCenterX(run, thickness) + thickness * 0.5f,
                                             cluster.bottom());
    ExpectRectNear(decorations.front().rect, expected);
    EXPECT_EQ(decorations.front().color, SK_ColorRED);
    EXPECT_NEAR(decorations.front().strokeWidth, thickness, kTolerance);
    EXPECT_LT(expected.centerX(), run.origin.x() - kSize * 0.5f);

    const auto& metrics = paragraph->getLayoutMetrics();
    EXPECT_LE(metrics.inkBounds.left(), expected.left() + kTolerance);
    EXPECT_NEAR(metrics.contentBounds.left(), 0, kTolerance);

    constexpr int width = 180;
    constexpr int height = 180;
    const auto bitmap = RasterizeText(paragraph.get(), 40, 20, width, height);
    ASSERT_GT(PixelsMatching(bitmap, IsRedLike), 0u);
    const SkIRect red = MatchingBounds(bitmap, IsRedLike);
    EXPECT_NEAR((red.left() + red.right()) * 0.5f, expected.centerX() + 40, 1.5f);
    EXPECT_LE(red.top(), SkScalarFloorToInt(expected.top() + 20) + 1);
    EXPECT_GE(red.bottom(), SkScalarCeilToInt(expected.bottom() + 20) - 1);
}

TEST_F(SkiaVerticalParagraph, V75_SidewaysUnderlineUsesTheSamePhysicalSideWithoutRotation) {
    auto style = DecorationStyleFor(LatinStyle(48), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto paragraph = BuildParagraph("A", style, {});
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 1u);
    ASSERT_TRUE(runs.front().sideways);
    const auto decorations = RecordDecorations(paragraph.get());
    ASSERT_EQ(decorations.size(), 1u);

    const auto& run = runs.front();
    const SkScalar thickness = DecorationThickness(run, skia_text::TextDecoration::kUnderline, 1);
    const SkScalar center = UnderlineCenterX(run, thickness);
    EXPECT_NEAR(decorations.front().rect.centerX(), center, kTolerance);
    EXPECT_LT(center, DecorationBlockCenter(run) - 48 * 0.5f);
    EXPECT_GT(center, DecorationBlockCenter(run) - 48);
    EXPECT_GT(decorations.front().rect.right(), decorations.front().rect.left());
}

TEST_F(SkiaVerticalParagraph, V76_LineThroughAndCombinedDecorationsKeepIndependentGeometryAndOrder) {
    auto combination = PrimaryStyle(48);
    combination.setDecoration(static_cast<skia_text::TextDecoration>(
            static_cast<int>(skia_text::TextDecoration::kUnderline) |
            static_cast<int>(skia_text::TextDecoration::kLineThrough)));
    combination.setDecorationColor(SK_ColorRED);
    combination.setDecorationThicknessMultiplier(1.5f);
    auto paragraph = BuildParagraph("天", combination, {});
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 1u);

    RecordingPainter painter;
    paragraph->paint(&painter, 0, 0);
    ASSERT_EQ(painter.decorations.size(), 2u);
    EXPECT_GT(painter.textBlobCount, 0);
    ASSERT_FALSE(painter.paintOrder.empty());
    EXPECT_EQ(painter.paintOrder.back(), RecordedPaintKind::Decoration);
    bool sawDecoration = false;
    for (auto kind: painter.paintOrder) {
        if (kind == RecordedPaintKind::Decoration) {
            sawDecoration = true;
        } else {
            EXPECT_FALSE(sawDecoration);
        }
    }

    const auto& run = runs.front();
    const SkScalar underlineThickness =
            DecorationThickness(run, skia_text::TextDecoration::kUnderline, 1.5f);
    const SkScalar throughThickness =
            DecorationThickness(run, skia_text::TextDecoration::kLineThrough, 1.5f);
    EXPECT_NEAR(painter.decorations[0].rect.centerX(), UnderlineCenterX(run, underlineThickness), kTolerance);
    EXPECT_NEAR(painter.decorations[0].strokeWidth, underlineThickness, kTolerance);
    EXPECT_NEAR(painter.decorations[1].rect.centerX(), run.origin.x(), kTolerance);
    EXPECT_NEAR(painter.decorations[1].strokeWidth, throughThickness, kTolerance);
    EXPECT_EQ(painter.decorations[0].color, SK_ColorRED);
    EXPECT_EQ(painter.decorations[1].color, SK_ColorRED);

    auto transparent = DecorationStyleFor(PrimaryStyle(32), skia_text::TextDecoration::kLineThrough);
    transparent.setColor(SK_ColorBLUE);
    auto transparentParagraph = BuildParagraph("天", transparent, {});
    transparentParagraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto transparentDecorations = RecordDecorations(transparentParagraph.get());
    ASSERT_EQ(transparentDecorations.size(), 1u);
    EXPECT_EQ(transparentDecorations.front().color, SK_ColorBLUE);
}

TEST_F(SkiaVerticalParagraph, V77_StylesFontSizesAndFallbackSplitDecorationSegments) {
    auto large = DecorationStyleFor(PrimaryStyle(48), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto plain = PrimaryStyle(24);
    auto small = DecorationStyleFor(PrimaryStyle(24), skia_text::TextDecoration::kUnderline, SK_ColorGREEN);
    auto paragraph = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(large);
        builder->addText("漢", 3);
        builder->pop();
        builder->pushStyle(plain);
        builder->addText("字", 3);
        builder->pop();
        builder->pushStyle(small);
        builder->addText("A", 1);
        builder->pop();
    });
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto runs = SnapshotRuns(paragraph.get());
    const auto decorations = RecordDecorations(paragraph.get());
    ASSERT_EQ(decorations.size(), 2u);

    const VisitorRun* largeRun = FindVisitorRun(runs, 0, 0);
    const VisitorRun* smallRun = FindVisitorRun(runs, 0, 6);
    ASSERT_NE(largeRun, nullptr);
    ASSERT_NE(smallRun, nullptr);
    const SkScalar largeThickness = DecorationThickness(*largeRun, skia_text::TextDecoration::kUnderline, 1);
    const SkScalar smallThickness = DecorationThickness(*smallRun, skia_text::TextDecoration::kUnderline, 1);
    EXPECT_NEAR(decorations[0].rect.centerX(), UnderlineCenterX(*largeRun, largeThickness), kTolerance);
    EXPECT_NEAR(decorations[1].rect.centerX(), decorations[0].rect.centerX(), kTolerance);
    EXPECT_NEAR(decorations[0].strokeWidth, largeThickness, kTolerance);
    EXPECT_NEAR(decorations[1].strokeWidth, smallThickness, kTolerance);
    EXPECT_EQ(decorations[0].color, SK_ColorRED);
    EXPECT_EQ(decorations[1].color, SK_ColorGREEN);
    EXPECT_LT(decorations[0].rect.bottom(), SourceClusterCell(paragraph.get(), 3).top() + kTolerance);
    EXPECT_GT(decorations[1].rect.top(), SourceClusterCell(paragraph.get(), 3).bottom() - kTolerance);

    auto fallbackStyle = DecorationStyleFor(PrimaryStyle(32), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto fallback = BuildParagraph("😀", fallbackStyle, {});
    fallback->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto fallbackDecorations = RecordDecorations(fallback.get());
    ASSERT_EQ(fallbackDecorations.size(), 1u);
    EXPECT_TRUE(std::isfinite(fallbackDecorations.front().rect.left()));
    EXPECT_TRUE(std::isfinite(fallbackDecorations.front().rect.right()));
}

TEST_F(SkiaVerticalParagraph, V78_WhitespaceControlsHardBreaksAndTrimFollowFinalSourceAdvances) {
    auto style = DecorationStyleFor(LatinStyle(32), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    const std::string controls = "A \u200E B";
    auto withControls = BuildParagraph(controls, style, {});
    withControls->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto controlsDecorations = RecordDecorations(withControls.get());
    ASSERT_EQ(controlsDecorations.size(), 1u);
    ExpectRectNear(controlsDecorations.front().rect,
                   SkRect::MakeLTRB(controlsDecorations.front().rect.left(),
                                    SourceClusterCell(withControls.get(), 0).top(),
                                    controlsDecorations.front().rect.right(),
                                    SourceClusterCell(withControls.get(), 6).bottom()));

    auto trailing = BuildParagraph("A ", style, {});
    trailing->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto trailingDecorations = RecordDecorations(trailing.get());
    ASSERT_EQ(trailingDecorations.size(), 1u);
    const auto trailingCluster = SourceClusterCell(trailing.get(), 0);
    EXPECT_NEAR(trailingDecorations.front().rect.top(), trailingCluster.top(), kTolerance);
    EXPECT_NEAR(trailingDecorations.front().rect.bottom(), trailingCluster.bottom(), kTolerance);
    EXPECT_LT(trailingDecorations.front().rect.height(), trailingCluster.height() + 12);

    auto hardBreak = BuildParagraph("A\nB", style, {});
    hardBreak->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto hardBreakDecorations = RecordDecorations(hardBreak.get());
    ASSERT_EQ(hardBreakDecorations.size(), 2u);
    EXPECT_NEAR(hardBreakDecorations[0].rect.top(), SourceClusterCell(hardBreak.get(), 0).top(), kTolerance);
    EXPECT_NEAR(hardBreakDecorations[0].rect.bottom(), SourceClusterCell(hardBreak.get(), 0).bottom(), kTolerance);
    EXPECT_NEAR(hardBreakDecorations[1].rect.top(), SourceClusterCell(hardBreak.get(), 2).top(), kTolerance);
    EXPECT_NEAR(hardBreakDecorations[1].rect.bottom(), SourceClusterCell(hardBreak.get(), 2).bottom(), kTolerance);
    EXPECT_NE(hardBreakDecorations[0].rect.centerX(), hardBreakDecorations[1].rect.centerX());

    auto tabbed = BuildParagraph("A\tB", style, {.replaceTabCharacters = true});
    auto letters = BuildParagraph("AB", style, {});
    tabbed->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    letters->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto tabbedDecoration = RecordDecorations(tabbed.get());
    const auto lettersDecoration = RecordDecorations(letters.get());
    ASSERT_EQ(tabbedDecoration.size(), 1u);
    ASSERT_EQ(lettersDecoration.size(), 1u);
    EXPECT_GT(tabbedDecoration.front().rect.height(), lettersDecoration.front().rect.height() + 5);
}

TEST_F(SkiaVerticalParagraph, V79_AlignmentIndentAndJustifyMoveDecorationWithRetainedGeometry) {
    const auto underlined = DecorationStyleFor(LatinStyle(32), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto indented = BuildConfiguredParagraph("A", underlined, [](skia_text::ParagraphStyle& style) {
        style.setTextIndent(24, false);
    });
    indented->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto indentedDecoration = RecordDecorations(indented.get());
    ASSERT_EQ(indentedDecoration.size(), 1u);
    EXPECT_NEAR(indentedDecoration.front().rect.top(), 24, kTolerance);

    auto natural = BuildParagraph("A", underlined, {});
    natural->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar naturalHeight = natural->getLayoutMetrics().longestInlineExtent;
    constexpr SkScalar kFiniteHeight = 240;
    auto end = BuildConfiguredParagraph("A", underlined, [](skia_text::ParagraphStyle& style) {
        style.setTextAlign(skia_text::TextAlign::kEnd);
    });
    auto center = BuildConfiguredParagraph("A", underlined, [](skia_text::ParagraphStyle& style) {
        style.setTextAlign(skia_text::TextAlign::kCenter);
    });
    end->layoutWithInlineExtent(kFiniteHeight);
    center->layoutWithInlineExtent(kFiniteHeight);
    const auto endDecoration = RecordDecorations(end.get());
    const auto centerDecoration = RecordDecorations(center.get());
    ASSERT_EQ(endDecoration.size(), 1u);
    ASSERT_EQ(centerDecoration.size(), 1u);
    EXPECT_NEAR(endDecoration.front().rect.top(), kFiniteHeight - naturalHeight, 0.5f);
    EXPECT_NEAR(centerDecoration.front().rect.top(), (kFiniteHeight - naturalHeight) * 0.5f, 0.5f);

    const auto justifiedStyle = DecorationStyleFor(PrimaryStyle(48),
                                                    skia_text::TextDecoration::kUnderline,
                                                    SK_ColorRED);
    const SkScalar advance = InfiniteInlineExtent("天", justifiedStyle);
    ASSERT_GT(advance, 0);
    const SkScalar justifiedHeight = advance * 4 + 12;
    auto justifiedStart = BuildParagraph("天地玄黃宇宙洪荒", justifiedStyle, {});
    auto justified = BuildParagraph("天地玄黃宇宙洪荒",
                                    justifiedStyle,
                                    {.align = skia_text::TextAlign::kJustify});
    justifiedStart->layoutWithInlineExtent(justifiedHeight);
    justified->layoutWithInlineExtent(justifiedHeight);
    ASSERT_EQ(justified->getLayoutMetrics().columns.size(), 2u);
    const auto startDecorations = RecordDecorations(justifiedStart.get());
    const auto justifiedDecorations = RecordDecorations(justified.get());
    ASSERT_EQ(startDecorations.size(), 2u);
    ASSERT_EQ(justifiedDecorations.size(), 2u);
    EXPECT_NEAR(justifiedDecorations[0].rect.top(), startDecorations[0].rect.top(), kTolerance);
    EXPECT_NEAR(justifiedDecorations[0].rect.bottom(),
                startDecorations[0].rect.bottom() + 12,
                0.5f);
    EXPECT_NEAR(justifiedDecorations[0].rect.height(),
                SourceClusterCell(justified.get(), 9).bottom(),
                kTolerance);
}

TEST_F(SkiaVerticalParagraph, V80_LineHeightAndMixedRunsKeepDecorationOnRunGeometry) {
    auto underlined = DecorationStyleFor(PrimaryStyle(40), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    underlined.setHeightOverride(true);
    underlined.setHeight(2.2f);
    auto plain = PrimaryStyle(40);
    plain.setHeightOverride(true);
    plain.setHeight(2.2f);

    auto decorated = BuildParagraph("漢", underlined, {});
    auto undecorated = BuildParagraph("漢", plain, {});
    decorated->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    undecorated->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    ExpectSnapshotsNear(Capture(undecorated.get()), Capture(decorated.get()));
    EXPECT_NEAR(decorated->getLayoutMetrics().contentBounds.left(),
                undecorated->getLayoutMetrics().contentBounds.left(),
                kTolerance);
    EXPECT_NEAR(decorated->getLayoutMetrics().columns[0].blockThickness,
                undecorated->getLayoutMetrics().columns[0].blockThickness,
                kTolerance);

    const auto runs = SnapshotRuns(decorated.get());
    ASSERT_EQ(runs.size(), 1u);
    const auto decoration = RecordDecorations(decorated.get());
    ASSERT_EQ(decoration.size(), 1u);
    const SkScalar thickness = DecorationThickness(runs.front(), skia_text::TextDecoration::kUnderline, 1);
    EXPECT_NEAR(decoration.front().rect.centerX(), UnderlineCenterX(runs.front(), thickness), kTolerance);
    const auto& column = decorated->getLayoutMetrics().columns[0];
    EXPECT_GT(decoration.front().rect.left(), column.physicalBounds.left());
    EXPECT_LT(decoration.front().rect.right(), column.physicalBounds.right());

    auto mixedDecorated = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(underlined);
        builder->addText("漢", 3);
        builder->pop();
        builder->pushStyle(plain);
        builder->addText("A", 1);
        builder->pop();
    });
    auto mixedPlain = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(plain);
        builder->addText("漢A", 4);
        builder->pop();
    });
    mixedDecorated->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    mixedPlain->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    EXPECT_NEAR(mixedDecorated->getLayoutMetrics().columns[0].blockThickness,
                mixedPlain->getLayoutMetrics().columns[0].blockThickness,
                kTolerance);
    EXPECT_FALSE(RecordDecorations(mixedDecorated.get()).empty());
}

TEST_F(SkiaVerticalParagraph, V81_MaxLinesAndEllipsisNeverDecorateHiddenOrSyntheticSource) {
    auto style = DecorationStyleFor(PrimaryStyle(40), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto measure = BuildParagraph("天", style, {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const std::string text = "天地玄黃";

    auto clipped = BuildParagraph(text, style, {.maxLines = 1});
    clipped->layoutWithInlineExtent(advance * 2);
    ASSERT_EQ(clipped->getLayoutMetrics().columns.size(), 1u);
    const auto clippedDecorations = RecordDecorations(clipped.get());
    ASSERT_EQ(clippedDecorations.size(), 1u);
    EXPECT_LE(clippedDecorations.front().rect.bottom(),
              SourceClusterCell(clipped.get(), 3).bottom() + kTolerance);
    EXPECT_NEAR(clippedDecorations.front().rect.bottom(),
                SourceClusterCell(clipped.get(), 3).bottom(),
                kTolerance);
    EXPECT_TRUE(TightBoxes(clipped.get(), 6, 7).empty());
    EXPECT_TRUE(clipped->didExceedMaxLines());

    auto ellipsized = BuildEllipsisParagraph(text, style, 1);
    ellipsized->layoutWithInlineExtent(advance * 2);
    const auto truncated = SnapshotRuns(ellipsized.get());
    ASSERT_EQ(SyntheticRuns(truncated).size(), 1u);
    const auto ellipsizedDecorations = RecordDecorations(ellipsized.get());
    ASSERT_EQ(ellipsizedDecorations.size(), 1u);
    const auto firstCell = SourceClusterCell(ellipsized.get(), 0);
    EXPECT_NEAR(ellipsizedDecorations.front().rect.top(), firstCell.top(), kTolerance);
    EXPECT_NEAR(ellipsizedDecorations.front().rect.bottom(), firstCell.bottom(), kTolerance);
    EXPECT_LT(ellipsizedDecorations.front().rect.bottom(), ellipsized->getLayoutMetrics().physicalSize.height());

    ellipsized->layoutWithInlineExtent(advance * 8);
    const auto recovered = RecordDecorations(ellipsized.get());
    ASSERT_EQ(recovered.size(), 1u);
    EXPECT_GT(recovered.front().rect.height(), ellipsizedDecorations.front().rect.height() * 3);

    auto customAffix = BuildEllipsisParagraph(text, style, 1, "...");
    customAffix->layoutWithInlineExtent(advance * 2);
    ASSERT_FALSE(SyntheticRuns(SnapshotRuns(customAffix.get())).empty());
    const auto customDecorations = RecordDecorations(customAffix.get());
    ASSERT_EQ(customDecorations.size(), 1u);
    EXPECT_LE(customDecorations.front().rect.bottom(),
              SourceClusterCell(customAffix.get(), 0).bottom() + kTolerance);
}

TEST_F(SkiaVerticalParagraph, V82_DecorationOnlyExtendsInkAndNotLayoutRangesOrPaths) {
    const std::string text = "「漢字ABCかな」";
    auto plain = BuildParagraph(text, PrimaryStyle(40), {});
    auto decoratedStyle = DecorationStyleFor(PrimaryStyle(40),
                                             static_cast<skia_text::TextDecoration>(
                                                     static_cast<int>(skia_text::TextDecoration::kUnderline) |
                                                     static_cast<int>(skia_text::TextDecoration::kLineThrough)),
                                             SK_ColorRED);
    auto decorated = BuildParagraph(text, decoratedStyle, {});
    const SkScalar height = 180;
    plain->layoutWithInlineExtent(height);
    decorated->layoutWithInlineExtent(height);

    const auto plainSnapshot = Capture(plain.get());
    const auto decoratedSnapshot = Capture(decorated.get());
    ExpectSnapshotsNear(plainSnapshot, decoratedSnapshot);
    ExpectRectNear(decoratedSnapshot.contentBounds, plainSnapshot.contentBounds);
    EXPECT_NEAR(decoratedSnapshot.physicalSize.width(), plainSnapshot.physicalSize.width(), kTolerance);
    EXPECT_NEAR(decoratedSnapshot.physicalSize.height(), plainSnapshot.physicalSize.height(), kTolerance);
    EXPECT_NE(decoratedSnapshot.inkBounds, plainSnapshot.inkBounds);
    EXPECT_LE(decoratedSnapshot.inkBounds.left(), plainSnapshot.inkBounds.left());
    EXPECT_GE(decoratedSnapshot.inkBounds.right(), plainSnapshot.inkBounds.right());
    EXPECT_EQ(decorated->didExceedMaxLines(), plain->didExceedMaxLines());
    EXPECT_EQ(decorated->lineNumber(), plain->lineNumber());

    const auto plainBoxes = TightBoxes(plain.get(), 0, Utf16Length(text));
    const auto decoratedBoxes = TightBoxes(decorated.get(), 0, Utf16Length(text));
    ASSERT_EQ(decoratedBoxes.size(), plainBoxes.size());
    for (size_t index = 0; index < plainBoxes.size(); ++index) {
        ExpectRectNear(decoratedBoxes[index].rect, plainBoxes[index].rect);
    }

    const SkPath plainPath = CorePath(plain.get());
    const SkPath decoratedPath = CorePath(decorated.get());
    ExpectRectNear(decoratedPath.getBounds(), plainPath.getBounds());
    EXPECT_LT(decoratedSnapshot.inkBounds.left(), decoratedPath.getBounds().left());
}

TEST_F(SkiaVerticalParagraph, V83_CanvasAndParagraphPainterAgreeAndClipOnlyAffectsPixels) {
    auto style = DecorationStyleFor(PrimaryStyle(44), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto paragraph = BuildParagraph("漢字", style, {});
    paragraph->layoutWithInlineExtent(260);
    const auto before = Capture(paragraph.get());
    const auto decorations = RecordDecorations(paragraph.get());
    ASSERT_EQ(decorations.size(), 1u);

    constexpr int width = 240;
    constexpr int height = 520;
    const auto canvasBitmap = RasterizeText(paragraph.get(), 45, 25, width, height);
    SkBitmap painterBitmap;
    painterBitmap.allocN32Pixels(width, height);
    painterBitmap.eraseColor(SK_ColorWHITE);
    {
        SkCanvas canvas(painterBitmap);
        CanvasPainter painter(&canvas);
        paragraph->paint(&painter, 45, 25);
    }
    EXPECT_EQ(DifferingPixels(canvasBitmap, painterBitmap), 0u);
    ASSERT_GT(PixelsMatching(canvasBitmap, IsRedLike), 0u);

    const auto offsetDecorations = RecordDecorations(paragraph.get(), 17, 23);
    ASSERT_EQ(offsetDecorations.size(), 1u);
    EXPECT_NEAR(offsetDecorations.front().rect.left(), decorations.front().rect.left() + 17, kTolerance);
    EXPECT_NEAR(offsetDecorations.front().rect.top(), decorations.front().rect.top() + 23, kTolerance);

    const SkRect redBounds = SkRect::Make(MatchingBounds(canvasBitmap, IsRedLike).makeOutset(2, 2));
    const SkRect clip = SkRect::MakeLTRB(redBounds.left(), redBounds.top(), redBounds.right(),
                                         redBounds.centerY());
    const auto clipped = RasterizeText(paragraph.get(), 45, 25, width, height, &clip);
    EXPECT_GT(NonWhitePixels(clipped, &clip), 0u);
    size_t outsideRed = 0;
    for (int y = 0; y < height; ++y) {
        for (int x = 0; x < width; ++x) {
            if (!clip.contains(x + 0.5f, y + 0.5f) && IsRedLike(clipped.getColor(x, y))) {
                ++outsideRed;
            }
        }
    }
    EXPECT_EQ(outsideRed, 0u);
    ExpectSnapshotsNear(before, Capture(paragraph.get()));
}

std::vector<RecordedBackground> RecordBackgrounds(skia_text::Paragraph* paragraph) {
    RecordingPainter painter;
    paragraph->paint(&painter, 0, 0);
    return painter.backgrounds;
}

skia_text::TextStyle BackgroundStyle(skia_text::TextStyle style, SkColor color = SK_ColorRED) {
    SkPaint paint;
    paint.setColor(color);
    style.setBackgroundPaint(paint);
    return style;
}

skia_text::TextStyle ShadowStyle(skia_text::TextStyle style,
                                  SkPoint offset = SkPoint::Make(65, 17),
                                  double blurSigma = 0,
                                  SkColor color = SK_ColorRED) {
    style.addShadow(skia_text::TextShadow(color, offset, blurSigma));
    return style;
}

RecordingPainter RecordGlyphPaints(skia_text::Paragraph* paragraph, SkScalar paintX = 0, SkScalar paintY = 0) {
    RecordingPainter painter;
    paragraph->paint(&painter, paintX, paintY);
    return painter;
}

void ExpectSharedShadowGeometry(const RecordingPainter& painter,
                                 const std::vector<skia_text::TextShadow>& shadows) {
    ASSERT_FALSE(painter.texts.empty());
    const size_t active = std::count_if(shadows.begin(), shadows.end(), [](const auto& shadow) {
        return shadow.hasShadow();
    });
    ASSERT_EQ(painter.shadows.size(), painter.texts.size() * active);
    size_t shadowIndex = 0;
    for (const auto& text: painter.texts) {
        for (const auto& shadow: shadows) {
            if (!shadow.hasShadow()) {
                continue;
            }
            const auto& actual = painter.shadows[shadowIndex++];
            EXPECT_EQ(actual.blob.get(), text.blob.get());
            EXPECT_NEAR(actual.origin.x(), text.origin.x() + shadow.fOffset.x(), kTolerance);
            EXPECT_NEAR(actual.origin.y(), text.origin.y() + shadow.fOffset.y(), kTolerance);
            EXPECT_EQ(actual.transform.getScaleX(), text.transform.getScaleX());
            EXPECT_EQ(actual.transform.getSkewX(), text.transform.getSkewX());
            EXPECT_EQ(actual.transform.getSkewY(), text.transform.getSkewY());
            EXPECT_EQ(actual.color, shadow.fColor);
            EXPECT_FLOAT_EQ(actual.blurSigma, SkDoubleToScalar(shadow.fBlurSigma));
        }
    }
}

TEST_F(SkiaVerticalParagraph, RB01_RB08_RB10_AdjacentGroupsUseFinalColumnAndGlobalEffectsOrder) {
    auto style = BackgroundStyle(ShadowStyle(PrimaryStyle(40), {8, 5}));
    style.setBaselineShift(-12);
    style.setDecoration(skia_text::TextDecoration::kUnderline);
    auto ruby = BuildCoreParagraph("漢字天地", style, {}, [](auto* builder) {
        builder->addRuby(skia_text::RubyAnnotation({0, 6}, "かなABC"));
        skia_text::RubyStyle second;
        second.gap = 7;
        second.fontSizeMultiplier = 0.75f;
        builder->addRuby(skia_text::RubyAnnotation({6, 12}, "てんち", second));
    });
    auto plain = BuildCoreParagraph("漢字天地", style);
    ruby->layoutWithInlineExtent(400);
    plain->layoutWithInlineExtent(400);
    EXPECT_FLOAT_EQ(ruby->getLayoutMetrics().columns[0].physicalBaseline,
                    plain->getLayoutMetrics().columns[0].physicalBaseline);
    const auto painted = RecordGlyphPaints(ruby.get());
    ExpectSharedShadowGeometry(painted, style.getShadows());
    ASSERT_FALSE(painted.backgrounds.empty());
    ASSERT_FALSE(painted.decorations.empty());
    EXPECT_TRUE(std::is_sorted(painted.paintOrder.begin(), painted.paintOrder.end(),
            [](auto first, auto second) { return static_cast<int>(first) < static_cast<int>(second); }));
    const float baseRight = plain->getLayoutMetrics().columns[0].physicalBounds.right();
    for (const auto& run : SnapshotRuns(ruby.get())) {
        if (run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag) {
            const float gap = run.starts[0] == 0 ? 2 : 7;
            EXPECT_GE(run.origin.x(), baseRight + gap);
        }
    }
}

SkBitmap ShadowPixelOracle(const RecordingPainter& plain,
                            const std::vector<skia_text::TextShadow>& shadows) {
    SkBitmap bitmap;
    bitmap.allocN32Pixels(640, 480);
    bitmap.eraseColor(SK_ColorWHITE);
    SkCanvas canvas(bitmap);
    for (const auto& background: plain.backgrounds) {
        canvas.drawRect(background.rect, std::get<SkPaint>(background.paint));
    }
    auto draw = [&](const RecordedGlyphPaint& glyph, SkPoint offset, SkColor color, SkScalar sigma) {
        SkPaint paint;
        paint.setColor(color);
        if (sigma != 0) {
            paint.setMaskFilter(SkMaskFilter::MakeBlur(kNormal_SkBlurStyle, sigma, false));
        }
        canvas.save();
        canvas.translate(glyph.origin.x() + offset.x(), glyph.origin.y() + offset.y());
        if (IsClockwise(glyph.transform)) {
            canvas.rotate(90);
        }
        canvas.drawTextBlob(glyph.blob, 0, 0, paint);
        canvas.restore();
    };
    for (const auto& glyph: plain.texts) {
        for (const auto& shadow: shadows) {
            if (shadow.hasShadow()) {
                draw(glyph, shadow.fOffset, shadow.fColor, SkDoubleToScalar(shadow.fBlurSigma));
            }
        }
    }
    for (const auto& glyph: plain.texts) {
        draw(glyph, {0, 0}, SK_ColorBLACK, 0);
    }
    for (const auto& decoration: plain.decorations) {
        const skia_text::ParagraphPainter::DecorationStyle style(
                decoration.color, decoration.strokeWidth, std::nullopt);
        SkPaint paint(style.skPaint());
        paint.setStroke(false);
        canvas.drawRect(decoration.rect, paint);
    }
    return bitmap;
}

TEST_F(SkiaVerticalParagraph, SH01_FinalBlobsPhysicalOffsetsBlurLayersAndCanvasPixels) {
    for (const std::string text: {"天地", "Abgj", "天地Ab玄黃"}) {
        for (const SkPoint offset: {SkPoint::Make(65, 17), SkPoint::Make(-55, -13),
                                    SkPoint::Make(0, 0)}) {
            auto style = ShadowStyle(PrimaryStyle(40), offset, 2.5);
            style.addShadow({SK_ColorBLUE, offset, 0});
            style.addShadow({SK_ColorTRANSPARENT, {3, -5}, 1});
            style.addShadow({SK_ColorGREEN, {0, 0}, 0});
            auto paragraph = BuildParagraph(text, style);
            auto plain = BuildParagraph(text, PrimaryStyle(40));
            paragraph->layoutWithInlineExtent(105);
            plain->layoutWithInlineExtent(105);
            const auto painted = RecordGlyphPaints(paragraph.get(), 140, 60);
            ExpectSharedShadowGeometry(painted, style.getShadows());
            const auto bitmap = RasterizeText(paragraph.get(), 140, 60, 640, 480);
            const auto oracle = ShadowPixelOracle(RecordGlyphPaints(plain.get(), 140, 60),
                                                  style.getShadows());
            EXPECT_EQ(DifferingPixels(bitmap, oracle), 0u) << text;
            EXPECT_GT(DifferingPixels(bitmap, RasterizeText(plain.get(), 140, 60, 640, 480)), 0u);
        }
    }
    auto noEffect = BuildParagraph("天地ABC", ShadowStyle(PrimaryStyle(40), {0, 0}, 0));
    auto plain = BuildParagraph("天地ABC", PrimaryStyle(40));
    noEffect->layoutWithInlineExtent(100);
    plain->layoutWithInlineExtent(100);
    EXPECT_TRUE(RecordGlyphPaints(noEffect.get()).shadows.empty());
    EXPECT_EQ(DifferingPixels(RasterizeText(noEffect.get(), 20, 20, 240, 240),
                              RasterizeText(plain.get(), 20, 20, 240, 240)), 0u);
}

TEST_F(SkiaVerticalParagraph, SH02_MultipleStylesAndGlobalPaintOrder) {
    auto style = ShadowStyle(BackgroundStyle(DecorationStyleFor(PrimaryStyle(40),
            static_cast<skia_text::TextDecoration>(
                    static_cast<int>(skia_text::TextDecoration::kUnderline) |
                    static_cast<int>(skia_text::TextDecoration::kLineThrough)), SK_ColorBLUE)));
    auto second = ShadowStyle(LatinStyle(30), {-43, 9}, 3, SK_ColorGREEN);
    auto paragraph = BuildCoreParagraph("天地", style, {}, [&](auto* builder) {
        builder->pushStyle(second);
        builder->addText("Abcd");
        builder->pushStyle(PrimaryStyle(24));
        builder->addText("玄黃");
    });
    paragraph->layoutWithInlineExtent(90);
    const auto painter = RecordGlyphPaints(paragraph.get());
    ASSERT_GT(paragraph->lineNumber(), 1u);
    ASSERT_FALSE(painter.backgrounds.empty());
    ASSERT_FALSE(painter.shadows.empty());
    ASSERT_FALSE(painter.decorations.empty());
    auto previous = RecordedPaintKind::Background;
    for (const auto kind: painter.paintOrder) {
        EXPECT_GE(static_cast<int>(kind), static_cast<int>(previous));
        previous = kind;
    }
    bool green = false;
    for (const auto& shadow: painter.shadows) {
        const auto text = std::find_if(painter.texts.begin(), painter.texts.end(), [&](const auto& glyph) {
            return glyph.blob == shadow.blob;
        });
        ASSERT_NE(text, painter.texts.end());
        const auto expected = shadow.color == SK_ColorGREEN ? second.getShadows()[0] : style.getShadows()[0];
        green |= shadow.color == SK_ColorGREEN;
        EXPECT_NEAR(shadow.origin.x() - text->origin.x(), expected.fOffset.x(), kTolerance);
        EXPECT_NEAR(shadow.origin.y() - text->origin.y(), expected.fOffset.y(), kTolerance);
    }
    EXPECT_TRUE(green);
    EXPECT_LT(painter.shadows.size(), painter.texts.size());
}

TEST_F(SkiaVerticalParagraph, SH03_JustifyIndentLineHeightMixedSizesAndIsolation) {
    for (auto align: {skia_text::TextAlign::kStart, skia_text::TextAlign::kEnd,
                      skia_text::TextAlign::kCenter, skia_text::TextAlign::kJustify}) {
        auto style = PrimaryStyle(40);
        style.setHeight(0.8f);
        auto build = [&](bool shadow) {
            return BuildConfiguredParagraph("天地玄黃宇宙", shadow ? ShadowStyle(style) : style,
                    [&](auto& settings) {
                        settings.setTextIndent(17, false);
                        settings.setTextAlign(align);
                    }, [&](auto* builder) {
                        auto latin = LatinStyle(26);
                        latin.setHeight(1.5f);
                        builder->pushStyle(shadow ? ShadowStyle(latin) : latin);
                        builder->addText("Ab cd 天地");
                    });
        };
        auto plain = build(false);
        auto shadow = build(true);
        plain->layoutWithInlineExtent(135);
        shadow->layoutWithInlineExtent(135);
        ExpectSnapshotsNear(Capture(plain.get()), Capture(shadow.get()));
        EXPECT_EQ(plain->getLayoutMetrics().inkBounds, shadow->getLayoutMetrics().inkBounds);
        EXPECT_EQ(plain->getLayoutMetrics().contentBounds, shadow->getLayoutMetrics().contentBounds);
        const auto plainRuns = SnapshotRuns(plain.get());
        const auto shadowRuns = SnapshotRuns(shadow.get());
        ASSERT_EQ(plainRuns.size(), shadowRuns.size());
        for (size_t index = 0; index < plainRuns.size(); ++index) {
            EXPECT_EQ(plainRuns[index].origin, shadowRuns[index].origin);
            EXPECT_EQ(plainRuns[index].positions, shadowRuns[index].positions);
            EXPECT_EQ(plainRuns[index].starts, shadowRuns[index].starts);
            EXPECT_EQ(plainRuns[index].glyphs, shadowRuns[index].glyphs);
        }
        for (size_t index = 0; index < plain->lineNumber(); ++index) {
            SkPath plainPath;
            SkPath shadowPath;
            EXPECT_EQ(plain->getPath(index, &plainPath), shadow->getPath(index, &shadowPath));
            EXPECT_EQ(plainPath, shadowPath);
        }
        const auto plainRects = TightBoxes(plain.get(), 0, 6);
        const auto shadowRects = TightBoxes(shadow.get(), 0, 6);
        ASSERT_EQ(plainRects.size(), shadowRects.size());
        for (size_t index = 0; index < plainRects.size(); ++index) {
            EXPECT_EQ(plainRects[index].rect, shadowRects[index].rect);
        }
        for (SkScalar coordinateX = -40; coordinateX < 240; coordinateX += 20) {
            for (SkScalar coordinateY = -20; coordinateY < 220; coordinateY += 20) {
                EXPECT_EQ(plain->getGlyphPositionAtCoordinate(coordinateX, coordinateY).position,
                          shadow->getGlyphPositionAtCoordinate(coordinateX, coordinateY).position);
            }
        }
        ExpectSharedShadowGeometry(RecordGlyphPaints(shadow.get()), ShadowStyle(style).getShadows());
        EXPECT_EQ(DifferingPixels(RasterizeText(shadow.get(), 90, 40, 640, 480),
                ShadowPixelOracle(RecordGlyphPaints(plain.get(), 90, 40), ShadowStyle(style).getShadows())), 0u);
    }
}

TEST_F(SkiaVerticalParagraph, SH04_MaxLinesEllipsisAndAffixStyleOwnership) {
    for (const char* affix: {"", "…", "Ab…"}) {
        auto style = ShadowStyle(PrimaryStyle(40));
        auto paragraph = BuildParagraph("天地玄黃宇宙洪荒", style, {.maxLines = 1, .ellipsis = affix});
        auto plain = BuildParagraph("天地玄黃宇宙洪荒", PrimaryStyle(40), {.maxLines = 1, .ellipsis = affix});
        paragraph->layoutWithInlineExtent(130);
        plain->layoutWithInlineExtent(130);
        ASSERT_TRUE(paragraph->didExceedMaxLines());
        ExpectSharedShadowGeometry(RecordGlyphPaints(paragraph.get()), style.getShadows());
        EXPECT_EQ(DifferingPixels(RasterizeText(paragraph.get(), 100, 40, 640, 480),
                ShadowPixelOracle(RecordGlyphPaints(plain.get(), 100, 40), style.getShadows())), 0u);
        EXPECT_TRUE(TightBoxes(paragraph.get(), 6, 8).empty());
    }
    for (bool affixShadow: {false, true}) {
        auto paragraph = BuildEllipsisParagraph("天", PrimaryStyle(40), 1, "Ab…", skia_text::TextAlign::kStart,
                [&](auto* builder) {
                    builder->pushStyle(affixShadow ? ShadowStyle(PrimaryStyle(40)) : PrimaryStyle(40));
                    builder->addText("玄黃宇宙洪荒");
                });
        paragraph->layoutWithInlineExtent(210);
        const auto runs = SnapshotRuns(paragraph.get());
        const auto synthetic = SyntheticRuns(runs);
        ASSERT_FALSE(synthetic.empty());
        const auto painter = RecordGlyphPaints(paragraph.get());
        if (affixShadow) {
            EXPECT_GE(painter.shadows.size(), synthetic.size());
        } else {
            EXPECT_TRUE(painter.shadows.empty());
        }
    }
}

TEST_F(SkiaVerticalParagraph, SH05_ClipPaintOffsetDirtyCacheAndFailureRecovery) {
    const std::string text = "天地ABC玄黃";
    auto style = ShadowStyle(PrimaryStyle(40), {55, -9}, 3);
    auto paragraph = BuildParagraph(text, style);
    paragraph->layoutWithInlineExtent(130);
    const auto bitmap = RasterizeText(paragraph.get(), 70, 40, 640, 480);
    const SkRect clip = SkRect::MakeLTRB(100, 50, 200, 120);
    const auto clipped = RasterizeText(paragraph.get(), 70, 40, 640, 480, &clip);
    for (int pixelY = 0; pixelY < 480; ++pixelY) {
        for (int pixelX = 0; pixelX < 640; ++pixelX) {
            EXPECT_EQ(clipped.getColor(pixelX, pixelY), clip.contains(pixelX + 0.5f, pixelY + 0.5f)
                    ? bitmap.getColor(pixelX, pixelY) : SK_ColorWHITE);
        }
    }
    const auto initial = RecordGlyphPaints(paragraph.get());
    const auto shifted = RecordGlyphPaints(paragraph.get(), 19, -11);
    ASSERT_EQ(initial.shadows.size(), shifted.shadows.size());
    for (size_t index = 0; index < initial.shadows.size(); ++index) {
        EXPECT_EQ(shifted.shadows[index].origin, initial.shadows[index].origin + SkPoint::Make(19, -11));
    }
    paragraph->layoutWithInlineExtent(130);
    EXPECT_EQ(DifferingPixels(bitmap, RasterizeText(paragraph.get(), 70, 40, 640, 480)), 0u);
    paragraph->markDirty();
    EXPECT_THROW(paragraph->layoutWithInlineExtent(-1), std::invalid_argument);
    RecordingPainter failed;
    EXPECT_THROW(paragraph->paint(&failed, 0, 0), std::logic_error);
    EXPECT_TRUE(failed.shadows.empty());
    paragraph->layoutWithInlineExtent(130);
    EXPECT_EQ(DifferingPixels(bitmap, RasterizeText(paragraph.get(), 70, 40, 640, 480)), 0u);
    paragraph->updateFontSize(0, text.size(), 30);
    paragraph->updateTextAlign(skia_text::TextAlign::kEnd);
    paragraph->layoutWithInlineExtent(180);
    auto reference = BuildParagraph(text, ShadowStyle(PrimaryStyle(30), {55, -9}, 3),
                                     {.align = skia_text::TextAlign::kEnd});
    reference->layoutWithInlineExtent(180);
    EXPECT_EQ(DifferingPixels(RasterizeText(paragraph.get(), 70, 40, 640, 480),
                              RasterizeText(reference.get(), 70, 40, 640, 480)), 0u);
    auto* cache = TestFontCollection()->getParagraphCache();
    cache->reset();
    cache->turnOn(true);
    for (SkColor color: {SK_ColorRED, SK_ColorTRANSPARENT, SK_ColorBLUE}) {
        auto colored = BuildParagraph(text, ShadowStyle(PrimaryStyle(40), {55, -9}, 3, color));
        auto plain = BuildParagraph(text, PrimaryStyle(40));
        colored->layoutWithInlineExtent(130);
        plain->layoutWithInlineExtent(130);
        const auto recorded = RecordGlyphPaints(colored.get());
        ASSERT_FALSE(recorded.shadows.empty());
        EXPECT_EQ(recorded.shadows.front().color, color);
        EXPECT_TRUE(RecordGlyphPaints(plain.get()).shadows.empty());
        auto* impl = static_cast<skia_text::ParagraphImpl*>(colored.get());
        for (auto& block: impl->styles()) {
            block.fStyle.resetShadows();
        }
        colored->markDirty();
        colored->layoutWithInlineExtent(130);
        EXPECT_TRUE(RecordGlyphPaints(colored.get()).shadows.empty());
    }
    cache->reset();
}

TEST_F(SkiaVerticalParagraph, SH06_HorizontalShadowRegression) {
    auto style = ShadowStyle(PrimaryStyle(40), {55, 9}, 2);
    auto paragraph = BuildCoreParagraph("天地Ab", style,
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    auto plain = BuildCoreParagraph("天地Ab", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    paragraph->layout(400);
    plain->layout(400);
    const auto recorded = RecordGlyphPaints(paragraph.get(), 30, 60);
    ASSERT_FALSE(recorded.shadows.empty());
    for (const auto& shadow: recorded.shadows) {
        EXPECT_EQ(shadow.color, SK_ColorRED);
        EXPECT_EQ(shadow.blurSigma, 2);
    }
    const auto bitmap = RasterizeText(paragraph.get(), 30, 60, 640, 480);
    EXPECT_GT(PixelsMatching(bitmap, IsRedLike), 0u);
    EXPECT_GT(DifferingPixels(bitmap, RasterizeText(plain.get(), 30, 60, 640, 480)), 0u);
    SkBitmap custom;
    custom.allocN32Pixels(640, 480);
    custom.eraseColor(SK_ColorWHITE);
    SkCanvas canvas(custom);
    CanvasPainter painter(&canvas);
    paragraph->paint(&painter, 30, 60);
    EXPECT_EQ(DifferingPixels(bitmap, custom), 0u);
}

TEST_F(SkiaVerticalParagraph, SH07_ShadowOnlyStyleChangesWithinOneShapedRun) {
    for (const std::string text: {"天地", "AB"}) {
        const auto style = PrimaryStyle(40);
        const auto red = ShadowStyle(style);
        const auto blue = ShadowStyle(style, {-55, 9}, 1, SK_ColorBLUE);
        auto paragraph = BuildCoreParagraph(text, style, {}, [&](auto* builder) {
            builder->pushStyle(red);
            builder->addText(text.c_str());
            builder->pushStyle(blue);
            builder->addText(text.c_str());
            builder->pushStyle(style);
            builder->addText(text.c_str());
        });
        paragraph->layoutWithInlineExtent(500);
        ASSERT_EQ(SnapshotRuns(paragraph.get()).size(), 1u);
        const auto painter = RecordGlyphPaints(paragraph.get());
        ASSERT_EQ(painter.texts.size(), 4u);
        ASSERT_EQ(painter.shadows.size(), 2u);
        EXPECT_EQ(painter.shadows[0].blob, painter.texts[1].blob);
        EXPECT_EQ(painter.shadows[1].blob, painter.texts[2].blob);
        EXPECT_EQ(painter.shadows[0].color, SK_ColorRED);
        EXPECT_EQ(painter.shadows[1].color, SK_ColorBLUE);
        EXPECT_NEAR(painter.shadows[0].origin.x() - painter.texts[1].origin.x(), 65, kTolerance);
        EXPECT_NEAR(painter.shadows[1].origin.x() - painter.texts[2].origin.x(), -55, kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, SH08_BackgroundShadowGlyphDecorationPixelsAcrossColumns) {
    auto style = BackgroundStyle(DecorationStyleFor(PrimaryStyle(40),
            static_cast<skia_text::TextDecoration>(
                    static_cast<int>(skia_text::TextDecoration::kUnderline) |
                    static_cast<int>(skia_text::TextDecoration::kLineThrough)), SK_ColorGREEN), SK_ColorYELLOW);
    style.setHeight(0.5f);
    auto shadow = ShadowStyle(style, {25, -7}, 3, SK_ColorRED);
    auto paragraph = BuildParagraph("天地AB玄黃宇宙", shadow);
    auto plain = BuildParagraph("天地AB玄黃宇宙", style);
    paragraph->layoutWithInlineExtent(90);
    plain->layoutWithInlineExtent(90);
    ASSERT_GT(paragraph->lineNumber(), 1u);
    const auto actual = RasterizeText(paragraph.get(), 140, 50, 640, 480);
    const auto expected = ShadowPixelOracle(RecordGlyphPaints(plain.get(), 140, 50), shadow.getShadows());
    EXPECT_EQ(DifferingPixels(actual, expected), 0u);
    EXPECT_GT(DifferingPixels(actual, RasterizeText(plain.get(), 140, 50, 640, 480)), 0u);
}

TEST_F(SkiaVerticalParagraph, BS01_OriginsPathsAndPixelsUsePhysicalShift) {
    for (const std::string text: {"天地", "Abcd", "天Ab地"}) {
        for (SkScalar shift: {-60.f, -12.f, 12.f, 60.f}) {
            auto style = PrimaryStyle(40);
            auto plain = BuildParagraph(text, style);
            style.setBaselineShift(shift);
            auto shifted = BuildParagraph(text, style);
            plain->layoutWithInlineExtent(400);
            shifted->layoutWithInlineExtent(400);
            const auto before = Capture(plain.get());
            const auto after = Capture(shifted.get());
            ASSERT_EQ(before.runs.size(), after.runs.size());
            const SkScalar baselineDelta = after.columns[0].baseline - before.columns[0].baseline;
            for (size_t index = 0; index < before.runs.size(); ++index) {
                EXPECT_NEAR(after.runs[index].origin.x() - before.runs[index].origin.x(),
                            baselineDelta - shift, kTolerance);
                EXPECT_EQ(after.runs[index].origin.y(), before.runs[index].origin.y());
                EXPECT_EQ(after.runs[index].positions, before.runs[index].positions);
                EXPECT_EQ(after.runs[index].advance.height(), before.runs[index].advance.height());
            }
            const auto expectedPath = CorePath(plain.get(), baselineDelta - shift);
            const auto actualPath = CorePath(shifted.get());
            EXPECT_NEAR(expectedPath.getBounds().left(), actualPath.getBounds().left(), kTolerance);
            EXPECT_NEAR(expectedPath.getBounds().right(), actualPath.getBounds().right(), kTolerance);
            EXPECT_EQ(expectedPath.getBounds().top(), actualPath.getBounds().top());
            EXPECT_EQ(expectedPath.getBounds().bottom(), actualPath.getBounds().bottom());
            const auto expected = RasterizeText(plain.get(), 120 + baselineDelta - shift, 40, 640, 520);
            const auto actual = RasterizeText(shifted.get(), 120, 40, 640, 520);
            EXPECT_GT(NonWhitePixels(actual), 0u);
            EXPECT_EQ(DifferingPixels(expected, actual), 0u);
        }
    }
}

TEST_F(SkiaVerticalParagraph, BS04_MixedMetricsLineHeightAndOrdinaryRunAnchor) {
    for (SkScalar height: {0.f, 0.5f, 2.f}) {
        auto build = [&](SkScalar shift) {
            return BuildCustomParagraph({}, [&](auto* builder) {
                auto normal = PrimaryStyle(60);
                normal.setHeight(height);
                builder->pushStyle(normal);
                builder->addText("天");
                auto small = LatinStyle(20);
                small.setHeight(height);
                small.setBaselineShift(shift);
                builder->pushStyle(small);
                builder->addText("Ab");
            });
        };
        auto plain = build(0);
        auto shifted = build(-90);
        plain->layoutWithInlineExtent(300);
        shifted->layoutWithInlineExtent(300);
        const auto before = Capture(plain.get());
        const auto after = Capture(shifted.get());
        ASSERT_EQ(before.runs.size(), 2u);
        ASSERT_EQ(after.runs.size(), 2u);
        const auto delta = after.columns[0].baseline - before.columns[0].baseline;
        EXPECT_NEAR(after.runs[0].origin.x() - before.runs[0].origin.x(), delta, kTolerance);
        EXPECT_NEAR(after.runs[1].origin.x() - before.runs[1].origin.x(), delta + 90, kTolerance);
        EXPECT_GT(after.physicalSize.width(), before.physicalSize.width());
    }
}

TEST_F(SkiaVerticalParagraph, BS05_ColumnSidesGrowAndInlineGeometryIsIsolated) {
    for (auto align: {skia_text::TextAlign::kStart, skia_text::TextAlign::kEnd,
                      skia_text::TextAlign::kCenter, skia_text::TextAlign::kJustify}) {
        auto build = [&](SkScalar shift) {
            auto style = PrimaryStyle(40);
            style.setBaselineShift(shift);
            return BuildConfiguredParagraph("天地玄黃宇宙洪荒", style, [&](auto& settings) {
                settings.setTextAlign(align);
                settings.setTextIndent(15, false);
            });
        };
        auto plain = build(0);
        plain->layoutWithInlineExtent(137);
        const auto before = Capture(plain.get());
        for (SkScalar shift: {-60.f, 60.f}) {
            auto shifted = build(shift);
            shifted->layoutWithInlineExtent(137);
            const auto after = Capture(shifted.get());
            ASSERT_EQ(after.columns.size(), before.columns.size());
            EXPECT_EQ(after.longest, before.longest);
            EXPECT_EQ(after.minIntrinsic, before.minIntrinsic);
            EXPECT_EQ(after.maxIntrinsic, before.maxIntrinsic);
            EXPECT_EQ(after.physicalSize.height(), before.physicalSize.height());
            for (size_t index = 0; index < before.columns.size(); ++index) {
                const auto& original = before.columns[index];
                const auto& column = after.columns[index];
                EXPECT_EQ(column.utf16Start, original.utf16Start);
                EXPECT_EQ(column.utf16End, original.utf16End);
                EXPECT_EQ(column.inlineExtent, original.inlineExtent);
                EXPECT_EQ(column.bounds.top(), original.bounds.top());
                EXPECT_NEAR(column.blockThickness, original.blockThickness + 60, kTolerance);
                EXPECT_NEAR(column.bounds.right() - column.baseline,
                            original.bounds.right() - original.baseline + (shift < 0 ? 60 : 0), kTolerance);
                EXPECT_NEAR(column.baseline - column.bounds.left(),
                            original.baseline - original.bounds.left() + (shift > 0 ? 60 : 0), kTolerance);
            }
            const auto boxes = TightBoxes(shifted.get(), 0, 8);
            ASSERT_EQ(boxes.size(), after.columns.size());
            for (size_t index = 0; index < boxes.size(); ++index) {
                EXPECT_EQ(boxes[index].rect.left(), after.columns[index].bounds.left());
                EXPECT_EQ(boxes[index].rect.right(), after.columns[index].bounds.right());
            }
        }
    }
}

TEST_F(SkiaVerticalParagraph, BS08_EffectsFollowShiftOnceAndRetainPaintOrder) {
    for (const std::string text: {"天地", "Abcd", "天Ab地"}) {
        for (SkScalar shift: {-60.f, 60.f}) {
            auto style = BackgroundStyle(DecorationStyleFor(PrimaryStyle(40),
                    static_cast<skia_text::TextDecoration>(skia_text::TextDecoration::kUnderline |
                                                          skia_text::TextDecoration::kLineThrough)));
            style = ShadowStyle(style, {17, -9}, 2, SK_ColorRED);
            auto plain = BuildParagraph(text, style);
            style.setBaselineShift(shift);
            auto shifted = BuildParagraph(text, style);
            plain->layoutWithInlineExtent(300);
            shifted->layoutWithInlineExtent(300);
            const auto delta = shifted->getLayoutMetrics().columns[0].physicalBaseline -
                               plain->getLayoutMetrics().columns[0].physicalBaseline - shift;
            EXPECT_EQ(DifferingPixels(RasterizeText(plain.get(), 120 + delta, 40, 640, 520),
                                     RasterizeText(shifted.get(), 120, 40, 640, 520)), 0u);
            const auto before = RecordBackgrounds(plain.get());
            const auto after = RecordBackgrounds(shifted.get());
            ASSERT_EQ(before.size(), after.size());
            for (size_t index = 0; index < before.size(); ++index) {
                EXPECT_NEAR(before[index].rect.left() + delta, after[index].rect.left(), kTolerance);
                EXPECT_NEAR(before[index].rect.right() + delta, after[index].rect.right(), kTolerance);
                EXPECT_EQ(before[index].rect.top(), after[index].rect.top());
                EXPECT_EQ(before[index].rect.bottom(), after[index].rect.bottom());
            }
            const auto painter = RecordGlyphPaints(shifted.get());
            ExpectSharedShadowGeometry(painter, style.getShadows());
            auto previous = RecordedPaintKind::Background;
            for (const auto kind: painter.paintOrder) {
                EXPECT_GE(static_cast<int>(kind), static_cast<int>(previous));
                previous = kind;
            }
        }
    }
}

TEST_F(SkiaVerticalParagraph, BS10_HiddenAndDeletedSourcesDoNotExpandColumns) {
    for (const char* affix: {"", "…", "Ab…"}) {
        auto build = [&](SkScalar shift) {
            return BuildCoreParagraph("天地玄黃", PrimaryStyle(40), {.maxLines = 1, .ellipsis = affix},
                    [&](auto* builder) {
                        auto hidden = PrimaryStyle(40);
                        hidden.setBaselineShift(shift);
                        builder->pushStyle(hidden);
                        builder->addText("宇宙洪荒");
                    });
        };
        auto plain = build(0);
        auto shifted = build(1000);
        plain->layoutWithInlineExtent(130);
        shifted->layoutWithInlineExtent(130);
        auto before = Capture(plain.get());
        auto after = Capture(shifted.get());
        ASSERT_EQ(before.runs.size(), after.runs.size());
        for (size_t index = 0; index < before.runs.size(); ++index) {
            ASSERT_FALSE(before.runs[index].starts.empty());
            ASSERT_FALSE(after.runs[index].starts.empty());
            before.runs[index].starts.back() = after.runs[index].starts.back();
        }
        ExpectSnapshotsNear(before, after);
        EXPECT_EQ(CorePath(plain.get()), CorePath(shifted.get()));
        EXPECT_TRUE(TightBoxes(shifted.get(), 4, 8).empty());
    }
}

TEST_F(SkiaVerticalParagraph, BS10b_AffixUsesOwnShiftWithoutChangingTruncation) {
    auto build = [&](SkScalar shift) {
        auto style = ShadowStyle(PrimaryStyle(40), {17, -9}, 2, SK_ColorRED);
        style.setBaselineShift(shift);
        return BuildParagraph("天地玄黃宇宙洪荒", style, {.maxLines = 1, .ellipsis = "Ab…"});
    };
    auto plain = build(0);
    auto shifted = build(-60);
    plain->layoutWithInlineExtent(130);
    shifted->layoutWithInlineExtent(130);
    EXPECT_EQ(plain->getLayoutMetrics().columns[0].utf16End,
              shifted->getLayoutMetrics().columns[0].utf16End);
    const auto before = SyntheticRuns(SnapshotRuns(plain.get()));
    const auto after = SyntheticRuns(SnapshotRuns(shifted.get()));
    ASSERT_FALSE(before.empty());
    ASSERT_EQ(before.size(), after.size());
    const auto delta = shifted->getLayoutMetrics().columns[0].physicalBaseline -
                       plain->getLayoutMetrics().columns[0].physicalBaseline + 60;
    for (size_t index = 0; index < before.size(); ++index) {
        EXPECT_NEAR(after[index].origin.x() - before[index].origin.x(), delta, kTolerance);
        EXPECT_EQ(after[index].origin.y(), before[index].origin.y());
        EXPECT_EQ(after[index].starts, before[index].starts);
    }
    EXPECT_EQ(CorePath(plain.get(), delta), CorePath(shifted.get()));
    EXPECT_EQ(DifferingPixels(RasterizeText(plain.get(), 120 + delta, 40, 640, 520),
                             RasterizeText(shifted.get(), 120, 40, 640, 520)), 0u);
}

TEST_F(SkiaVerticalParagraph, BS05b_OppositeShiftsGrowBothSidesOfOneColumn) {
    auto paragraph = BuildCustomParagraph({}, [](auto* builder) {
        for (SkScalar shift: {-60.f, 90.f, 0.f}) {
            auto style = PrimaryStyle(40);
            style.setBaselineShift(shift);
            builder->pushStyle(style);
            builder->addText("天");
        }
    });
    paragraph->layoutWithInlineExtent(300);
    const auto& column = paragraph->getLayoutMetrics().columns[0];
    EXPECT_NEAR(column.blockThickness, 190, kTolerance);
    EXPECT_NEAR(column.physicalBaseline - column.physicalBounds.left(), 110, kTolerance);
    EXPECT_NEAR(column.physicalBounds.right() - column.physicalBaseline, 80, kTolerance);
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 3u);
    EXPECT_NEAR(runs[0].origin.x() - runs[2].origin.x(), 60, kTolerance);
    EXPECT_NEAR(runs[1].origin.x() - runs[2].origin.x(), -90, kTolerance);
}

TEST_F(SkiaVerticalParagraph, BS05c_TinyHalfLeadingShiftNeverShrinksTheBaseColumn) {
    auto style = LatinStyle(32);
    style.setHeightOverride(true);
    style.setHeight(1e-9f);
    style.setHalfLeading(true);
    auto plain = BuildParagraph("j", style);
    plain->layoutWithInlineExtent(300);
    const auto thickness = plain->getLayoutMetrics().columns[0].blockThickness;
    for (SkScalar shift: {-1e-20f, 1e-20f, -1e-9f, 1e-9f}) {
        style.setBaselineShift(shift);
        auto shifted = BuildParagraph("j", style);
        ASSERT_NO_THROW(shifted->layoutWithInlineExtent(300));
        EXPECT_GE(shifted->getLayoutMetrics().columns[0].blockThickness, thickness);
    }
}

TEST_F(SkiaVerticalParagraph, BS10c_DeletedSourceAndNonFittingAffixHaveNoShiftContribution) {
    auto style = PrimaryStyle(40);
    auto plain = BuildParagraph("天地玄黃", style, {.maxLines = 1, .ellipsis = "Ab…"});
    style.setBaselineShift(1000);
    auto shifted = BuildParagraph("天地玄黃", style, {.maxLines = 1, .ellipsis = "Ab…"});
    plain->layoutWithInlineExtent(10);
    shifted->layoutWithInlineExtent(10);
    ExpectSnapshotsNear(Capture(plain.get()), Capture(shifted.get()));
    EXPECT_TRUE(SnapshotRuns(shifted.get()).empty());
    EXPECT_TRUE(CorePath(shifted.get()).isEmpty());
}

TEST_F(SkiaVerticalParagraph, BS11_ClosestQueriesAndRangesUseFinalColumns) {
    for (SkScalar shift: {-60.f, 60.f}) {
        auto style = PrimaryStyle(40);
        style.setBaselineShift(shift);
        auto paragraph = BuildParagraph("天地玄黃", style);
        paragraph->layoutWithInlineExtent(85);
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 2u);
        for (size_t index = 0; index < metrics.columns.size(); ++index) {
            const auto& column = metrics.columns[index];
            const auto boxes = TightBoxes(paragraph.get(), index * 2, index * 2 + 1);
            ASSERT_EQ(boxes.size(), 1u);
            EXPECT_EQ(boxes[0].rect.left(), column.physicalBounds.left());
            EXPECT_EQ(boxes[0].rect.right(), column.physicalBounds.right());
            const auto point = BoxCenter(boxes[0]);
            skia_text::Paragraph::GlyphClusterInfo cluster;
            ASSERT_TRUE(paragraph->getClosestGlyphClusterAt(point.x(), point.y(), &cluster));
            EXPECT_EQ(cluster.fClusterTextRange.start, index * 6);
            EXPECT_EQ(cluster.fBounds.left(), column.physicalBounds.left());
            EXPECT_EQ(cluster.fBounds.right(), column.physicalBounds.right());
            EXPECT_GE(paragraph->getGlyphPositionAtCoordinate(point.x(), point.y()).position,
                      static_cast<int>(index * 2));
        }
    }
}

TEST_F(SkiaVerticalParagraph, BS16b_NonFiniteAndDerivedOverflowFailAndRecover) {
    for (SkScalar shift: {std::numeric_limits<SkScalar>::quiet_NaN(),
                          std::numeric_limits<SkScalar>::infinity(),
                          -std::numeric_limits<SkScalar>::infinity(),
                          std::numeric_limits<SkScalar>::max()}) {
        auto paragraph = BuildCustomParagraph({}, [&](auto* builder) {
            for (SkScalar value: {shift, -shift}) {
                auto style = PrimaryStyle(40);
                style.setBaselineShift(value);
                builder->pushStyle(style);
                builder->addText("天");
            }
        });
        EXPECT_THROW(paragraph->layoutWithInlineExtent(300), std::invalid_argument);
        auto* impl = static_cast<skia_text::ParagraphImpl*>(paragraph.get());
        for (auto& block: impl->styles()) block.fStyle.setBaselineShift(0);
        paragraph->markDirty();
        paragraph->layoutWithInlineExtent(300);
        auto reference = BuildParagraph("天天", PrimaryStyle(40));
        reference->layoutWithInlineExtent(300);
        ExpectSnapshotsNear(Capture(reference.get()), Capture(paragraph.get()));
    }
}

TEST_F(SkiaVerticalParagraph, BS16c_HorizontalBaselineShiftStillMovesPhysicalY) {
    auto style = PrimaryStyle(40);
    auto plain = BuildParagraph("天Ab地", style, {.writingMode = skia_text::WritingMode::kHorizontalTb});
    style.setBaselineShift(20);
    auto shifted = BuildParagraph("天Ab地", style, {.writingMode = skia_text::WritingMode::kHorizontalTb});
    plain->layout(400);
    shifted->layout(400);
    const auto before = SnapshotRuns(plain.get());
    const auto after = SnapshotRuns(shifted.get());
    ASSERT_EQ(before.size(), after.size());
    for (size_t index = 0; index < before.size(); ++index) {
        EXPECT_EQ(before[index].origin.x(), after[index].origin.x());
        EXPECT_EQ(before[index].glyphs, after[index].glyphs);
        EXPECT_EQ(before[index].positions, after[index].positions);
    }
    EXPECT_GT(NonWhitePixels(RasterizeText(shifted.get(), 50, 50, 640, 300)), 0u);
}

TEST_F(SkiaVerticalParagraph, BS16_DirtyCacheFontUpdateAndFailureRecovery) {
    const std::string text = "天地Ab玄黃";
    auto style = PrimaryStyle(40);
    style.setBaselineShift(-55);
    auto paragraph = BuildParagraph(text, style);
    paragraph->layoutWithInlineExtent(135);
    const auto initial = Capture(paragraph.get());
    paragraph->layoutWithInlineExtent(135);
    ExpectSnapshotsNear(initial, Capture(paragraph.get()));
    paragraph->markDirty();
    paragraph->layoutWithInlineExtent(135);
    ExpectSnapshotsNear(initial, Capture(paragraph.get()));
    paragraph->updateFontSize(0, text.size(), 30);
    paragraph->layoutWithInlineExtent(135);
    style.setFontSize(30);
    auto reference = BuildParagraph(text, style);
    reference->layoutWithInlineExtent(135);
    ExpectSnapshotsNear(Capture(reference.get()), Capture(paragraph.get()));
    auto* impl = static_cast<skia_text::ParagraphImpl*>(paragraph.get());
    for (auto& block: impl->styles()) {
        block.fStyle.setBaselineShift(std::numeric_limits<SkScalar>::infinity());
    }
    paragraph->markDirty();
    EXPECT_THROW(paragraph->layoutWithInlineExtent(135), std::invalid_argument);
    for (auto& block: impl->styles()) {
        block.fStyle.setBaselineShift(-55);
    }
    paragraph->markDirty();
    paragraph->layoutWithInlineExtent(135);
    ExpectSnapshotsNear(Capture(reference.get()), Capture(paragraph.get()));
    auto zero = BuildParagraph(text, PrimaryStyle(30));
    zero->layoutWithInlineExtent(135);
    EXPECT_NE(zero->getLayoutMetrics().physicalSize, paragraph->getLayoutMetrics().physicalSize);
}

TEST_F(SkiaVerticalParagraph, BG01_CjkRunBoxPaintAndCanvasPixels) {
    auto paragraph = BuildParagraph("天地", BackgroundStyle(PrimaryStyle(40)), {});
    paragraph->layoutWithInlineExtent(240);
    const auto rects = RecordBackgrounds(paragraph.get());
    ASSERT_EQ(rects.size(), 1u);
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 1u);
    const auto& run = static_cast<skia_text::ParagraphImpl*>(paragraph.get())->runs().front();
    EXPECT_NEAR(rects[0].rect.left(), runs[0].origin.x() - run.blockDescent(), kTolerance);
    EXPECT_NEAR(rects[0].rect.right(), runs[0].origin.x() + run.blockAscent(), kTolerance);
    EXPECT_NEAR(rects[0].rect.top(), 0, kTolerance);
    EXPECT_NEAR(rects[0].rect.bottom(), 80, kTolerance);
    EXPECT_EQ(std::get<SkPaint>(rects[0].paint).getColor(), SK_ColorRED);
    const auto bitmap = RasterizeText(paragraph.get(), 30, 20, 180, 280);
    EXPECT_GT(PixelsMatching(bitmap, IsRedLike), 100u);
    const auto pixels = MatchingBounds(bitmap, IsRedLike);
    EXPECT_NEAR(pixels.left(), rects[0].rect.left() + 30, 1);
    EXPECT_NEAR(pixels.bottom(), rects[0].rect.bottom() + 20, 1);
}

TEST_F(SkiaVerticalParagraph, BG02_SidewaysMixedCombiningEmojiAndFallback) {
    for (const std::string text: {"ABC", "漢ABCかな", "Á天😀"}) {
        auto paragraph = BuildParagraph(text, BackgroundStyle(PrimaryStyle(40)), {});
        paragraph->layoutWithInlineExtent(400);
        const auto rects = RecordBackgrounds(paragraph.get());
        ASSERT_FALSE(rects.empty());
        const auto runs = SnapshotRuns(paragraph.get());
        for (const auto& rect: rects) {
            EXPECT_TRUE(rect.rect.isFinite());
            EXPECT_FALSE(rect.rect.isEmpty());
            EXPECT_LE(rect.rect.bottom(), paragraph->getLayoutMetrics().longestInlineExtent + kTolerance);
        }
        EXPECT_FALSE(runs.empty());
    }
    auto paragraph = BuildParagraph("ABC", BackgroundStyle(LatinStyle(40)), {});
    paragraph->layoutWithInlineExtent(400);
    const auto runs = SnapshotRuns(paragraph.get());
    ASSERT_EQ(runs.size(), 1u);
    ASSERT_TRUE(runs[0].sideways);
    const auto rects = RecordBackgrounds(paragraph.get());
    ASSERT_EQ(rects.size(), 1u);
    const auto& run = static_cast<skia_text::ParagraphImpl*>(paragraph.get())->runs().front();
    EXPECT_NEAR(rects[0].rect.left(), runs[0].origin.x() - run.blockDescent(), kTolerance);
    EXPECT_NEAR(rects[0].rect.right(), runs[0].origin.x() + run.blockAscent(), kTolerance);
}

TEST_F(SkiaVerticalParagraph, BG03_SpanPaintIDPaintPropertiesAndGaps) {
    auto red = BackgroundStyle(PrimaryStyle(40));
    auto id = PrimaryStyle(40);
    id.setBackgroundPaintID(73);
    SkPaint richPaint;
    richPaint.setColor(SK_ColorBLUE);
    richPaint.setAlphaf(0.4f);
    richPaint.setStyle(SkPaint::kStroke_Style);
    richPaint.setStrokeWidth(3);
    richPaint.setBlendMode(SkBlendMode::kSrc);
    richPaint.setShader(SkShaders::Color(SK_ColorGREEN));
    auto rich = PrimaryStyle(40);
    rich.setBackgroundPaint(richPaint);
    auto paragraph = BuildCoreParagraph("天", red, {}, [&](auto* builder) {
        builder->pushStyle(PrimaryStyle(40));
        builder->addText("地");
        builder->pop();
        builder->pushStyle(id);
        builder->addText("玄");
        builder->pop();
        builder->pushStyle(rich);
        builder->addText("黃");
    });
    paragraph->layoutWithInlineExtent(400);
    const auto rects = RecordBackgrounds(paragraph.get());
    ASSERT_EQ(rects.size(), 3u);
    EXPECT_LT(rects[0].rect.bottom(), rects[1].rect.top());
    EXPECT_EQ(std::get<skia_text::ParagraphPainter::PaintID>(rects[1].paint), 73u);
    EXPECT_EQ(std::get<SkPaint>(rects[2].paint), richPaint);
    auto shaderParagraph = BuildParagraph("天", rich, {});
    shaderParagraph->layoutWithInlineExtent(200);
    const auto bitmap = RasterizeText(shaderParagraph.get(), 30, 20, 160, 240);
    EXPECT_GT(PixelsMatching(bitmap, [](SkColor color) {
        return SkColorGetG(color) > SkColorGetR(color) + 30 &&
               SkColorGetG(color) > SkColorGetB(color) + 30;
    }), 0u);
}

TEST_F(SkiaVerticalParagraph, BG04_WhitespaceControlsTrimAndHardBreaks) {
    for (const std::string text: {"天 地", "天\t地", "天\u00ad地", "天\u200e地"}) {
        auto paragraph = BuildParagraph(text, BackgroundStyle(PrimaryStyle(40)), {});
        paragraph->layoutWithInlineExtent(400);
        const auto rects = RecordBackgrounds(paragraph.get());
        ASSERT_FALSE(rects.empty());
        SkScalar bottom = rects.front().rect.top();
        for (const auto& rect: rects) {
            EXPECT_NEAR(rect.rect.top(), bottom, kTolerance);
            bottom = rect.rect.bottom();
        }
        EXPECT_NEAR(bottom, SourceClusterCell(paragraph.get(), text.rfind("地")).bottom(), kTolerance);
    }
    auto paragraph = BuildParagraph("天  \n\n地  ", BackgroundStyle(PrimaryStyle(40)), {});
    paragraph->layoutWithInlineExtent(400);
    const auto rects = RecordBackgrounds(paragraph.get());
    ASSERT_EQ(rects.size(), 2u);
    for (const auto& rect: rects) {
        EXPECT_NEAR(rect.rect.height(), 40, kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, BG05_AlignmentIndentJustifyAndColumnsUseFinalGeometry) {
    auto indented = BuildConfiguredParagraph("天地", BackgroundStyle(PrimaryStyle(40)), [](auto& style) {
        style.setTextIndent(25, false);
    });
    indented->layoutWithInlineExtent(200);
    const auto indentedRects = RecordBackgrounds(indented.get());
    ASSERT_EQ(indentedRects.size(), 1u);
    EXPECT_NEAR(indentedRects[0].rect.top(), 25, kTolerance);
    for (auto align: {skia_text::TextAlign::kStart, skia_text::TextAlign::kCenter,
                     skia_text::TextAlign::kEnd, skia_text::TextAlign::kJustify}) {
        auto paragraph = BuildParagraph("天地玄黃宇宙洪荒", BackgroundStyle(PrimaryStyle(40)), {.align = align});
        paragraph->layoutWithInlineExtent(170);
        const auto rects = RecordBackgrounds(paragraph.get());
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(rects.size(), metrics.columns.size());
        for (size_t index = 0; index < rects.size(); ++index) {
            const auto& column = metrics.columns[index];
            const auto first = SourceClusterCell(paragraph.get(), column.utf8Range.start);
            const auto last = SourceClusterCell(paragraph.get(), column.utf8Range.end - 3);
            EXPECT_NEAR(rects[index].rect.top(), first.top(), kTolerance);
            EXPECT_NEAR(rects[index].rect.bottom(), last.bottom(), kTolerance);
        }
    }
}

TEST_F(SkiaVerticalParagraph, BG06_LineHeightAndMixedRunBoxes) {
    auto small = BackgroundStyle(PrimaryStyle(20));
    auto large = BackgroundStyle(PrimaryStyle(60), SK_ColorBLUE);
    small.setHeight(2);
    small.setHeightOverride(true);
    auto paragraph = BuildCoreParagraph("天", small, {}, [&](auto* builder) {
        builder->pushStyle(large);
        builder->addText("地");
    });
    paragraph->layoutWithInlineExtent(300);
    const auto rects = RecordBackgrounds(paragraph.get());
    ASSERT_EQ(rects.size(), 2u);
    EXPECT_NEAR(rects[0].rect.width(), 40, kTolerance);
    EXPECT_GT(rects[1].rect.width(), rects[0].rect.width());
    EXPECT_NE(rects[0].rect.left(), rects[1].rect.left());
}

TEST_F(SkiaVerticalParagraph, BG07_MaxLinesAndEllipsisExcludeHiddenSourceAndAffix) {
    for (const char* affix: {static_cast<const char*>(nullptr), "…", "ABCDE"}) {
        auto paragraph = BuildParagraph("天地玄黃宇宙洪荒", BackgroundStyle(PrimaryStyle(40)),
                                        {.maxLines = 1, .ellipsis = affix});
        paragraph->layoutWithInlineExtent(130);
        const auto rects = RecordBackgrounds(paragraph.get());
        const auto* impl = static_cast<skia_text::ParagraphImpl*>(paragraph.get());
        ASSERT_EQ(impl->verticalColumns().size(), 1u);
        const auto& column = impl->verticalColumns()[0];
        EXPECT_TRUE(paragraph->didExceedMaxLines());
        for (const auto& rect: rects) {
            EXPECT_LE(rect.rect.bottom(), column.inlineContentExtent + kTolerance);
            if (column.ellipsized) {
                EXPECT_LE(rect.rect.bottom(), column.affix.inlineStart + kTolerance);
            }
        }
        EXPECT_TRUE(paragraph->getRectsForRange(7, 8, skia_text::RectHeightStyle::kTight,
                                               skia_text::RectWidthStyle::kTight).empty());
    }
}

TEST_F(SkiaVerticalParagraph, BG08_OnlyInkChangesAndSourcePathHitRemainIdentical) {
    auto plain = BuildParagraph("天地ABC玄黃", PrimaryStyle(40), {});
    auto background = BuildParagraph("天地ABC玄黃", BackgroundStyle(PrimaryStyle(40)), {});
    plain->layoutWithInlineExtent(170);
    background->layoutWithInlineExtent(170);
    const auto& before = plain->getLayoutMetrics();
    const auto& after = background->getLayoutMetrics();
    ExpectRectNear(before.contentBounds, after.contentBounds);
    EXPECT_EQ(before.physicalSize, after.physicalSize);
    EXPECT_EQ(before.minIntrinsicInlineExtent, after.minIntrinsicInlineExtent);
    EXPECT_EQ(before.maxIntrinsicInlineExtent, after.maxIntrinsicInlineExtent);
    const auto plainRuns = SnapshotRuns(plain.get());
    const auto backgroundRuns = SnapshotRuns(background.get());
    ASSERT_EQ(plainRuns.size(), backgroundRuns.size());
    for (size_t index = 0; index < plainRuns.size(); ++index) {
        EXPECT_EQ(plainRuns[index].origin, backgroundRuns[index].origin);
        EXPECT_EQ(plainRuns[index].positions, backgroundRuns[index].positions);
        EXPECT_EQ(plainRuns[index].starts, backgroundRuns[index].starts);
        EXPECT_EQ(plainRuns[index].glyphs, backgroundRuns[index].glyphs);
    }
    ASSERT_EQ(before.columns.size(), after.columns.size());
    for (size_t index = 0; index < before.columns.size(); ++index) {
        ExpectRectNear(before.columns[index].physicalBounds, after.columns[index].physicalBounds);
        SkPath plainPath;
        SkPath backgroundPath;
        EXPECT_EQ(plain->getPath(index, &plainPath), background->getPath(index, &backgroundPath));
        EXPECT_EQ(plainPath, backgroundPath);
    }
    const auto plainRects = plain->getRectsForRange(0, 4, skia_text::RectHeightStyle::kTight,
                                                   skia_text::RectWidthStyle::kTight);
    const auto backgroundRects = background->getRectsForRange(0, 4, skia_text::RectHeightStyle::kTight,
                                                             skia_text::RectWidthStyle::kTight);
    ASSERT_EQ(plainRects.size(), backgroundRects.size());
    for (size_t index = 0; index < plainRects.size(); ++index) {
        ExpectRectNear(plainRects[index].rect, backgroundRects[index].rect);
    }
    EXPECT_EQ(plain->getGlyphPositionAtCoordinate(5, 10).position,
              background->getGlyphPositionAtCoordinate(5, 10).position);
    for (const auto& rect: RecordBackgrounds(background.get())) {
        EXPECT_TRUE(after.inkBounds.contains(rect.rect));
    }
}

TEST_F(SkiaVerticalParagraph, BG09_CanvasPainterClipAndOffsets) {
    auto paragraph = BuildParagraph("天地玄黃", BackgroundStyle(PrimaryStyle(40)), {});
    paragraph->layoutWithInlineExtent(90);
    RecordingPainter recording;
    paragraph->paint(&recording, 30, 20);
    const auto rects = RecordBackgrounds(paragraph.get());
    ASSERT_EQ(rects.size(), recording.backgrounds.size());
    for (size_t index = 0; index < rects.size(); ++index) {
        auto expected = rects[index].rect;
        expected.offset(30, 20);
        ExpectRectNear(expected, recording.backgrounds[index].rect);
    }
    SkBitmap direct;
    direct.allocN32Pixels(240, 240);
    direct.eraseColor(SK_ColorWHITE);
    SkCanvas canvas(direct);
    canvas.clipRect(SkRect::MakeLTRB(30, 30, 200, 70));
    paragraph->paint(&canvas, 60, 20);
    EXPECT_GT(PixelsMatching(direct, IsRedLike), 0u);
    const auto bounds = MatchingBounds(direct, IsRedLike);
    EXPECT_GE(bounds.top(), 30);
    EXPECT_LE(bounds.bottom(), 70);
    EXPECT_EQ(rects.size(), RecordBackgrounds(paragraph.get()).size());
    const auto directBitmap = RasterizeText(paragraph.get(), 60, 20, 240, 240);
    SkBitmap painterBitmap;
    painterBitmap.allocN32Pixels(240, 240);
    painterBitmap.eraseColor(SK_ColorWHITE);
    SkCanvas painterCanvas(painterBitmap);
    CanvasPainter painter(&painterCanvas);
    paragraph->paint(&painter, 60, 20);
    EXPECT_EQ(DifferingPixels(directBitmap, painterBitmap), 0u);
}

TEST_F(SkiaVerticalParagraph, BG10_AllBackgroundsBeforeGlyphsThenDecorations) {
    auto style = BackgroundStyle(DecorationStyleFor(PrimaryStyle(40),
            static_cast<skia_text::TextDecoration>(
                    static_cast<int>(skia_text::TextDecoration::kUnderline) |
                    static_cast<int>(skia_text::TextDecoration::kLineThrough)), SK_ColorBLUE));
    auto paragraph = BuildParagraph("天地玄黃", style, {});
    paragraph->layoutWithInlineExtent(90);
    RecordingPainter painter;
    paragraph->paint(&painter, 0, 0);
    EXPECT_EQ(painter.backgrounds.size(), 2u);
    EXPECT_EQ(painter.decorations.size(), 4u);
    ASSERT_GT(painter.textBlobCount, 0);
    auto previous = RecordedPaintKind::Background;
    for (auto kind: painter.paintOrder) {
        EXPECT_GE(static_cast<int>(kind), static_cast<int>(previous));
        previous = kind;
    }
}

TEST_F(SkiaVerticalParagraph, BG12_UpdateDirtyFailureRecoveryAndCacheIsolation) {
    const std::string text = "天地玄黃";
    auto paragraph = BuildParagraph(text, BackgroundStyle(PrimaryStyle(40)), {});
    paragraph->layoutWithInlineExtent(170);
    const auto initial = RecordBackgrounds(paragraph.get());
    paragraph->layoutWithInlineExtent(170);
    const auto sameExtent = RecordBackgrounds(paragraph.get());
    ASSERT_EQ(initial.size(), sameExtent.size());
    for (size_t index = 0; index < initial.size(); ++index) {
        ExpectRectNear(initial[index].rect, sameExtent[index].rect);
    }
    SkPaint blue;
    blue.setColor(SK_ColorBLUE);
    paragraph->updateBackgroundPaint(0, text.size(), blue);
    RecordingPainter painter;
    EXPECT_THROW(paragraph->paint(&painter, 0, 0), std::logic_error);
    paragraph->layoutWithInlineExtent(170);
    for (const auto& rect: RecordBackgrounds(paragraph.get())) {
        EXPECT_EQ(std::get<SkPaint>(rect.paint), blue);
    }
    paragraph->markDirty();
    EXPECT_THROW(paragraph->layoutWithInlineExtent(-1), std::invalid_argument);
    EXPECT_THROW(paragraph->paint(&painter, 0, 0), std::logic_error);
    paragraph->layoutWithInlineExtent(170);
    ASSERT_FALSE(RecordBackgrounds(paragraph.get()).empty());
    paragraph->updateTextAlign(skia_text::TextAlign::kEnd);
    paragraph->layoutWithInlineExtent(230);
    paragraph->updateFontSize(0, text.size(), 30);
    paragraph->layoutWithInlineExtent(230);
    auto reference = BuildParagraph(text, BackgroundStyle(PrimaryStyle(30), SK_ColorBLUE),
                                    {.align = skia_text::TextAlign::kEnd});
    reference->layoutWithInlineExtent(230);
    const auto updated = RecordBackgrounds(paragraph.get());
    const auto expected = RecordBackgrounds(reference.get());
    ASSERT_EQ(updated.size(), expected.size());
    for (size_t index = 0; index < updated.size(); ++index) {
        ExpectRectNear(updated[index].rect, expected[index].rect);
    }
    auto* impl = static_cast<skia_text::ParagraphImpl*>(paragraph.get());
    for (auto& block: impl->styles()) {
        block.fStyle.clearBackgroundColor();
    }
    paragraph->markDirty();
    paragraph->layoutWithInlineExtent(230);
    EXPECT_TRUE(RecordBackgrounds(paragraph.get()).empty());
    paragraph->updateBackgroundPaint(0, text.size(), blue);
    paragraph->layoutWithInlineExtent(230);
    EXPECT_FALSE(RecordBackgrounds(paragraph.get()).empty());
    auto* cache = TestFontCollection()->getParagraphCache();
    cache->reset();
    cache->turnOn(true);
    for (const auto color: {SK_ColorRED, SK_ColorTRANSPARENT, SK_ColorBLUE}) {
        auto colored = BuildParagraph(text, BackgroundStyle(PrimaryStyle(40), color), {});
        auto plain = BuildParagraph(text, PrimaryStyle(40), {});
        colored->layoutWithInlineExtent(170);
        plain->layoutWithInlineExtent(170);
        const auto rects = RecordBackgrounds(colored.get());
        ASSERT_FALSE(rects.empty());
        EXPECT_EQ(std::get<SkPaint>(rects[0].paint).getColor(), color);
        EXPECT_TRUE(RecordBackgrounds(plain.get()).empty());
    }
    EXPECT_EQ(cache->count(), 1);
    cache->reset();
    cache->turnOn(true);
}

TEST_F(SkiaVerticalParagraph, BG13_HorizontalBackgroundStillPaints) {
    auto paragraph = BuildCoreParagraph("天地ABC", BackgroundStyle(PrimaryStyle(40)),
                                         {.writingMode = skia_text::WritingMode::kHorizontalTb});
    paragraph->layout(300);
    const auto rects = RecordBackgrounds(paragraph.get());
    EXPECT_FALSE(rects.empty());
    EXPECT_GT(PixelsMatching(RasterizeText(paragraph.get(), 10, 10, 300, 120), IsRedLike), 0u);
}

TEST_F(SkiaVerticalParagraph, BG11_LinkRangeDoesNotAcquireBackgroundOnlyArea) {
    auto style = BackgroundStyle(PrimaryStyle(40));
    style.setHeight(3);
    style.setHeightOverride(true);
    auto background = BuildParagraph("天地", style, {});
    auto plainStyle = style;
    plainStyle.clearBackgroundColor();
    auto plain = BuildParagraph("天地", plainStyle, {});
    plain->layoutWithInlineExtent(200);
    background->layoutWithInlineExtent(200);
    const auto before = plain->getRectsForRange(0, 1, skia_text::RectHeightStyle::kTight,
                                               skia_text::RectWidthStyle::kTight);
    const auto after = background->getRectsForRange(0, 1, skia_text::RectHeightStyle::kTight,
                                                   skia_text::RectWidthStyle::kTight);
    ASSERT_EQ(before.size(), 1u);
    ASSERT_EQ(after.size(), 1u);
    ExpectRectNear(before[0].rect, after[0].rect);
    const auto rects = RecordBackgrounds(background.get());
    ASSERT_EQ(rects.size(), 1u);
    const SkPoint outsideLink = SkPoint::Make(rects[0].rect.centerX(), rects[0].rect.bottom() - 1);
    EXPECT_TRUE(rects[0].rect.contains(outsideLink.x(), outsideLink.y()));
    EXPECT_FALSE(after[0].rect.contains(outsideLink.x(), outsideLink.y()));
}

TEST_F(SkiaVerticalParagraph, BG08b_NoWrapBackgroundRetainsOverflowWithoutChangingLayout) {
    auto build = [](bool background) {
        auto style = PrimaryStyle(40);
        if (background) {
            style = BackgroundStyle(style);
        }
        return BuildParagraph("天地玄黃宇宙", style, {});
    };
    auto plain = build(false);
    auto background = build(true);
    plain->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    background->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const auto rects = RecordBackgrounds(background.get());
    ASSERT_EQ(rects.size(), 1u);
    EXPECT_GT(rects[0].rect.bottom(), 90);
    EXPECT_EQ(plain->getLayoutMetrics().physicalSize, background->getLayoutMetrics().physicalSize);
    ExpectRectNear(plain->getLayoutMetrics().contentBounds, background->getLayoutMetrics().contentBounds);
}

TEST_F(SkiaVerticalParagraph, V84_UpdatesFailuresAndCacheIsolationNeverReuseStaleDecorations) {
    auto style = DecorationStyleFor(PrimaryStyle(40), skia_text::TextDecoration::kUnderline, SK_ColorRED);
    auto paragraph = BuildParagraph("天地玄黃", style, {});
    paragraph->layoutWithInlineExtent(360);
    const auto startDecorations = RecordDecorations(paragraph.get());
    ASSERT_EQ(startDecorations.size(), 1u);
    const SkScalar startTop = startDecorations.front().rect.top();

    paragraph->updateTextAlign(skia_text::TextAlign::kCenter);
    paragraph->layoutWithInlineExtent(360);
    const auto centered = RecordDecorations(paragraph.get());
    ASSERT_EQ(centered.size(), 1u);
    EXPECT_NEAR(centered.front().rect.top(), SourceClusterCell(paragraph.get(), 0).top(), kTolerance);
    EXPECT_GT(centered.front().rect.top(), startTop);

    paragraph->updateFontSize(0, std::string("天地玄黃").size(), 72);
    paragraph->layoutWithInlineExtent(360);
    const auto enlarged = RecordDecorations(paragraph.get());
    auto referenceStyle = style;
    referenceStyle.setFontSize(72);
    auto reference = BuildParagraph("天地玄黃", referenceStyle, {.align = skia_text::TextAlign::kCenter});
    reference->layoutWithInlineExtent(360);
    const auto referenceDecorations = RecordDecorations(reference.get());
    ASSERT_FALSE(enlarged.empty());
    ASSERT_EQ(enlarged.size(), referenceDecorations.size());
    for (size_t index = 0; index < enlarged.size(); ++index) {
        ExpectRectNear(enlarged[index].rect, referenceDecorations[index].rect);
        EXPECT_NEAR(enlarged[index].strokeWidth, referenceDecorations[index].strokeWidth, kTolerance);
        EXPECT_EQ(enlarged[index].color, referenceDecorations[index].color);
    }
    const auto enlargedRuns = SnapshotRuns(paragraph.get());
    ASSERT_FALSE(enlargedRuns.empty());
    const SkScalar enlargedThickness =
            DecorationThickness(enlargedRuns.front(), skia_text::TextDecoration::kUnderline, 1);
    EXPECT_NEAR(enlarged.front().strokeWidth, enlargedThickness, kTolerance);
    EXPECT_GT(enlarged.front().strokeWidth, centered.front().strokeWidth);
    EXPECT_NE(enlarged.front().rect.centerX(), centered.front().rect.centerX());

    paragraph->markDirty();
    paragraph->layoutWithInlineExtent(360);
    const auto rebuilt = RecordDecorations(paragraph.get());
    ASSERT_EQ(rebuilt.size(), enlarged.size());
    for (size_t index = 0; index < rebuilt.size(); ++index) {
        ExpectRectNear(rebuilt[index].rect, enlarged[index].rect);
    }

    EXPECT_THROW(paragraph->layoutWithInlineExtent(-1), std::invalid_argument);
    EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    paragraph->layoutWithInlineExtent(360);
    const auto recovered = RecordDecorations(paragraph.get());
    ASSERT_EQ(recovered.size(), enlarged.size());
    for (size_t index = 0; index < recovered.size(); ++index) {
        ExpectRectNear(recovered[index].rect, enlarged[index].rect);
    }

    auto* cache = TestFontCollection()->getParagraphCache();
    ASSERT_NE(cache, nullptr);
    cache->reset();
    cache->turnOn(true);
    auto plainStyle = PrimaryStyle(40);
    auto cachedPlain = BuildParagraph("天地", plainStyle, {});
    auto cachedDecorated = BuildParagraph("天地", style, {});
    cachedPlain->layoutWithInlineExtent(360);
    cachedDecorated->layoutWithInlineExtent(360);
    EXPECT_TRUE(RecordDecorations(cachedPlain.get()).empty());
    EXPECT_EQ(RecordDecorations(cachedDecorated.get()).size(), 1u);
    cache->reset();
    cache->turnOn(false);
}


TEST_F(SkiaVerticalParagraph, TC08b_SqueezedVisitorPathAndHitTestShareCell) {
    auto paragraph = BuildVerticalTateChuYoko("ABC", {{0, 3}});
    paragraph->layoutWithInlineExtent(400);
    const auto boxes = TightBoxes(paragraph.get(), 0, 3);
    ASSERT_EQ(boxes.size(), 1u);
    const SkRect cell = boxes[0].rect;
    const auto runs = TateChuYokoRuns(SnapshotRuns(paragraph.get()));
    ASSERT_EQ(runs.size(), 1u);
    EXPECT_LT(runs[0].transform.getScaleX(), 1);
    for (const auto& position: AbsoluteGlyphOrigins(runs[0])) {
        EXPECT_GE(position.x(), cell.left() - 1);
        EXPECT_LE(position.x(), cell.right() + 1);
        EXPECT_GE(position.y(), cell.top() - 1);
        EXPECT_LE(position.y(), cell.bottom() + 1);
    }

    SkPath path;
    EXPECT_EQ(paragraph->getPath(0, &path), 0);
    ASSERT_FALSE(path.isEmpty());
    const SkRect pathBounds = path.getBounds();
    EXPECT_GE(pathBounds.left(), cell.left() - 1);
    EXPECT_LE(pathBounds.right(), cell.right() + 1);
    EXPECT_GE(pathBounds.top(), cell.top() - 1);
    EXPECT_LE(pathBounds.bottom(), cell.bottom() + 1);

    const auto start = paragraph->getGlyphPositionAtCoordinate(cell.centerX(), cell.top() + 2);
    const auto end = paragraph->getGlyphPositionAtCoordinate(cell.centerX(), cell.bottom() - 2);
    EXPECT_EQ(start.position, 0u);
    EXPECT_EQ(end.position, 3u);
}

TEST_F(SkiaVerticalParagraph, TC07b_IndentSpacingBaselineShiftAndFallbackRuns) {
    auto spacing = PrimaryStyle(40);
    spacing.setLetterSpacing(5);
    spacing.setWordSpacing(7);
    auto plain = BuildCoreParagraph("天 12", spacing, {.indent = 30});
    auto tcy = BuildVerticalTateChuYoko("天 12", {{4, 6}}, spacing, {.indent = 30});
    plain->layoutWithInlineExtent(400);
    tcy->layoutWithInlineExtent(400);
    const auto reference = SourceClusterCell(plain.get(), 4);
    const auto cell = TightBoxes(tcy.get(), 2, 4);
    ASSERT_EQ(cell.size(), 1u);
    EXPECT_NEAR(cell[0].rect.top(), reference.top(), kTolerance);
    EXPECT_NEAR(cell[0].rect.height(), 40, kTolerance);

    auto shiftedStyle = PrimaryStyle(40);
    shiftedStyle.setBaselineShift(12);
    auto plainShifted = BuildCoreParagraph("天12", shiftedStyle);
    auto shifted = BuildVerticalTateChuYoko("天12", {{3, 5}}, shiftedStyle);
    plainShifted->layoutWithInlineExtent(400);
    shifted->layoutWithInlineExtent(400);
    const auto referenceRuns = SnapshotRuns(plainShifted.get());
    ASSERT_FALSE(referenceRuns.empty());
    const auto shiftedCell = TightBoxes(shifted.get(), 1, 3);
    ASSERT_EQ(shiftedCell.size(), 1u);
    EXPECT_NEAR(shiftedCell[0].rect.centerX(), referenceRuns[0].origin.x(), kTolerance);

    auto fallback = BuildVerticalTateChuYoko("天AB", {{3, 5}});
    fallback->layoutWithInlineExtent(400);
    const auto fallbackRuns = TateChuYokoRuns(SnapshotRuns(fallback.get()));
    ASSERT_EQ(fallbackRuns.size(), 1u);
    EXPECT_EQ(fallbackRuns[0].starts, (std::vector<uint32_t>{3, 4, 5}));
    EXPECT_EQ(TightBoxes(fallback.get(), 1, 3).size(), 1u);
}

TEST_F(SkiaVerticalParagraph, TC11b_HorizontalInvalidRangeIsNoOpAndWhitespaceControlsAreAccepted) {
    auto plain = BuildCoreParagraph("12", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    auto annotated = BuildCoreParagraph("12", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb},
            [](auto* builder) { builder->addTateChuYoko({0, 99}); });
    plain->layout(400);
    annotated->layout(400);
    ExpectSnapshotsNear(Capture(plain.get()), Capture(annotated.get()));

    auto trailing = BuildVerticalTateChuYoko("12 ", {{0, 3}});
    trailing->layoutWithInlineExtent(400);
    EXPECT_EQ(TightBoxes(trailing.get(), 0, 3).size(), 1u);
    EXPECT_EQ(TateChuYokoRuns(SnapshotRuns(trailing.get())).size(), 1u);

    auto controls = BuildVerticalTateChuYoko("\xE2\x80\x8E" "12", {{0, 5}});
    controls->layoutWithInlineExtent(400);
    EXPECT_EQ(TightBoxes(controls.get(), 0, 3).size(), 1u);
    EXPECT_EQ(TateChuYokoRuns(SnapshotRuns(controls.get())).size(), 1u);
}

TEST_F(SkiaVerticalParagraph, TC17_GrowUsesNaturalCellWidthWithoutCompression) {
    auto style = PrimaryStyle(40);
    auto reference = BuildCoreParagraph("ABC", style,
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    reference->layout(10000);
    const SkScalar natural = reference->getMaxIntrinsicWidth();
    EXPECT_GT(natural, 40);

    auto grow = BuildVerticalTateChuYokoModes("天ABC",
            {{{3, 6}, skia_text::TateChuYokoSizing::kGrow}}, style);
    auto squeeze = BuildVerticalTateChuYoko("天ABC", {{3, 6}}, style);
    grow->layoutWithInlineExtent(400);
    squeeze->layoutWithInlineExtent(400);

    const auto growBoxes = TightBoxes(grow.get(), 1, 4);
    const auto squeezeBoxes = TightBoxes(squeeze.get(), 1, 4);
    ASSERT_EQ(growBoxes.size(), 1u);
    ASSERT_EQ(squeezeBoxes.size(), 1u);
    EXPECT_NEAR(growBoxes[0].rect.width(), natural, kTolerance);
    EXPECT_NEAR(squeezeBoxes[0].rect.width(), 40, kTolerance);
    EXPECT_NEAR(growBoxes[0].rect.height(), 40, kTolerance);
    EXPECT_NEAR(growBoxes[0].rect.top(), squeezeBoxes[0].rect.top(), kTolerance);
    EXPECT_NEAR(growBoxes[0].rect.height(), squeezeBoxes[0].rect.height(), kTolerance);

    const auto growRuns = TateChuYokoRuns(SnapshotRuns(grow.get()));
    ASSERT_EQ(growRuns.size(), 1u);
    EXPECT_NEAR(growRuns[0].transform.getScaleX(), 1, 0.001);
    EXPECT_NEAR(growRuns[0].advance.height(), 40, kTolerance);

    const auto& growColumn = grow->getLayoutMetrics().columns[0];
    const auto& squeezeColumn = squeeze->getLayoutMetrics().columns[0];
    EXPECT_NEAR(growColumn.blockThickness, natural, kTolerance);
    EXPECT_NEAR(growBoxes[0].rect.centerX(), growColumn.physicalBounds.centerX(), kTolerance);
    const SkScalar growAscent = growColumn.physicalBaseline - growColumn.physicalBounds.left();
    const SkScalar growDescent = growColumn.physicalBounds.right() - growColumn.physicalBaseline;
    const SkScalar squeezeAscent =
            squeezeColumn.physicalBaseline - squeezeColumn.physicalBounds.left();
    const SkScalar squeezeDescent =
            squeezeColumn.physicalBounds.right() - squeezeColumn.physicalBaseline;
    EXPECT_NEAR(growAscent - squeezeAscent, growDescent - squeezeDescent, kTolerance);
    EXPECT_NEAR(grow->getLayoutMetrics().longestInlineExtent,
                squeeze->getLayoutMetrics().longestInlineExtent, kTolerance);
}

TEST_F(SkiaVerticalParagraph, TC18_MixedSizingModesResolvePerGroup) {
    auto style = PrimaryStyle(40);
    auto reference = BuildCoreParagraph("ABC", style,
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    reference->layout(10000);
    const SkScalar natural = reference->getMaxIntrinsicWidth();

    auto mixed = BuildVerticalTateChuYokoModes("天ABC12地",
            {{{3, 6}, skia_text::TateChuYokoSizing::kGrow},
             {{6, 8}, skia_text::TateChuYokoSizing::kSqueeze}}, style);
    mixed->layoutWithInlineExtent(400);
    const auto growBoxes = TightBoxes(mixed.get(), 1, 4);
    const auto squeezeBoxes = TightBoxes(mixed.get(), 4, 6);
    ASSERT_EQ(growBoxes.size(), 1u);
    ASSERT_EQ(squeezeBoxes.size(), 1u);
    EXPECT_NEAR(growBoxes[0].rect.width(), natural, kTolerance);
    EXPECT_NEAR(squeezeBoxes[0].rect.width(), 40, kTolerance);
    EXPECT_NEAR(squeezeBoxes[0].rect.top() - growBoxes[0].rect.top(), 40, kTolerance);

    const auto runs = TateChuYokoRuns(SnapshotRuns(mixed.get()));
    ASSERT_EQ(runs.size(), 2u);
    EXPECT_NEAR(runs[0].transform.getScaleX(), 1, 0.001);
    EXPECT_LT(runs[1].transform.getScaleX(), 1);
    EXPECT_FLOAT_EQ(runs[0].advance.height(), 40);
    EXPECT_FLOAT_EQ(runs[1].advance.height(), 40);
}

TEST_F(SkiaVerticalParagraph, TC19_GrowColumnSizeAndAtomicWrap) {
    auto style = PrimaryStyle(40);
    auto plain = BuildCoreParagraph("天", style);
    auto grow = BuildVerticalTateChuYokoModes("天ABC",
            {{{3, 6}, skia_text::TateChuYokoSizing::kGrow}}, style);
    plain->layoutWithInlineExtent(400);
    grow->layoutWithInlineExtent(400);

    const auto& plainMetrics = plain->getLayoutMetrics();
    const auto& growMetrics = grow->getLayoutMetrics();
    ASSERT_EQ(growMetrics.columns.size(), 1u);
    EXPECT_GT(growMetrics.physicalSize.width(), plainMetrics.physicalSize.width());
    EXPECT_NEAR(growMetrics.physicalSize.width(), growMetrics.columns[0].blockThickness, kTolerance);
    EXPECT_NEAR(growMetrics.physicalSize.width(),
                TightBoxes(grow.get(), 1, 4)[0].rect.width(), kTolerance);

    auto wrapped = BuildVerticalTateChuYokoModes("天ABC地",
            {{{3, 6}, skia_text::TateChuYokoSizing::kGrow}}, style);
    wrapped->layoutWithInlineExtent(50);
    const auto& wrappedMetrics = wrapped->getLayoutMetrics();
    ASSERT_EQ(wrappedMetrics.columns.size(), 3u);
    EXPECT_EQ(wrappedMetrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(wrappedMetrics.columns[0].utf16End, 1u);
    EXPECT_EQ(wrappedMetrics.columns[1].utf16Start, 1u);
    EXPECT_EQ(wrappedMetrics.columns[1].utf16End, 4u);
    EXPECT_EQ(wrappedMetrics.columns[2].utf16Start, 4u);
    EXPECT_EQ(wrappedMetrics.columns[2].utf16End, 5u);
    EXPECT_GT(wrappedMetrics.physicalSize.width(), growMetrics.physicalSize.width());
}

TEST_F(SkiaVerticalParagraph, TC20_GrowQueryPathAndVisitorUseNaturalGeometry) {
    auto style = PrimaryStyle(40);
    auto reference = BuildCoreParagraph("ABC", style,
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    reference->layout(10000);
    const SkScalar natural = reference->getMaxIntrinsicWidth();

    auto grow = BuildVerticalTateChuYokoModes("天ABC地",
            {{{3, 6}, skia_text::TateChuYokoSizing::kGrow}}, style);
    grow->layoutWithInlineExtent(400);
    const auto boxes = TightBoxes(grow.get(), 1, 4);
    ASSERT_EQ(boxes.size(), 1u);
    const SkRect cell = boxes[0].rect;
    EXPECT_NEAR(cell.width(), natural, kTolerance);
    EXPECT_NEAR(cell.height(), 40, kTolerance);

    const auto runs = TateChuYokoRuns(SnapshotRuns(grow.get()));
    ASSERT_EQ(runs.size(), 1u);
    EXPECT_EQ(runs[0].starts, (std::vector<uint32_t>{3, 4, 5, 6}));
    EXPECT_NEAR(runs[0].transform.getScaleX(), 1, 0.001);
    EXPECT_NEAR(runs[0].advance.height(), 40, kTolerance);
    EXPECT_NEAR(runs[0].origin.x(), cell.left(), kTolerance);

    const auto start = grow->getGlyphPositionAtCoordinate(cell.centerX(), cell.top() + 2);
    const auto end = grow->getGlyphPositionAtCoordinate(cell.centerX(), cell.bottom() - 2);
    EXPECT_EQ(start.position, 1u);
    EXPECT_EQ(end.position, 4u);

    auto onlyGroup = BuildVerticalTateChuYokoModes("ABC",
            {{{0, 3}, skia_text::TateChuYokoSizing::kGrow}}, style);
    onlyGroup->layoutWithInlineExtent(400);
    const auto onlyBoxes = TightBoxes(onlyGroup.get(), 0, 3);
    ASSERT_EQ(onlyBoxes.size(), 1u);
    const SkRect onlyCell = onlyBoxes[0].rect;
    EXPECT_NEAR(onlyCell.width(), natural, kTolerance);
    SkPath path;
    EXPECT_EQ(onlyGroup->getPath(0, &path), 0);
    ASSERT_FALSE(path.isEmpty());
    const auto pathBounds = path.getBounds();
    EXPECT_GE(pathBounds.left(), onlyCell.left() - 1);
    EXPECT_LE(pathBounds.right(), onlyCell.right() + 1);
    EXPECT_GE(pathBounds.top(), onlyCell.top() - 1);
    EXPECT_LE(pathBounds.bottom(), onlyCell.bottom() + 1);
}

TEST_F(SkiaVerticalParagraph, TC21_GrowEllipsisAdjacentGroupsAndFontSizeUpdate) {
    auto style = PrimaryStyle(40);
    auto reference = BuildCoreParagraph("ABC", style,
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    reference->layout(10000);
    const SkScalar natural = reference->getMaxIntrinsicWidth();

    auto adjacent = BuildVerticalTateChuYokoModes("ABC12",
            {{{0, 3}, skia_text::TateChuYokoSizing::kGrow},
             {{3, 5}, skia_text::TateChuYokoSizing::kSqueeze}}, style);
    adjacent->layoutWithInlineExtent(400);
    const auto adjacentRuns = TateChuYokoRuns(SnapshotRuns(adjacent.get()));
    ASSERT_EQ(adjacentRuns.size(), 2u);
    EXPECT_NEAR(adjacentRuns[0].transform.getScaleX(), 1, 0.001);
    EXPECT_LT(adjacentRuns[1].transform.getScaleX(), 1);
    const auto growBoxes = TightBoxes(adjacent.get(), 0, 3);
    const auto squeezeBoxes = TightBoxes(adjacent.get(), 3, 5);
    ASSERT_EQ(growBoxes.size(), 1u);
    ASSERT_EQ(squeezeBoxes.size(), 1u);
    EXPECT_NEAR(growBoxes[0].rect.width(), natural, kTolerance);
    EXPECT_NEAR(squeezeBoxes[0].rect.width(), 40, kTolerance);

    auto removed = BuildEllipsisParagraph("天地ABC", style, 1, "\xE2\x80\xA6",
            skia_text::TextAlign::kStart,
            [](auto* builder) {
                builder->addTateChuYoko({6, 9}, skia_text::TateChuYokoSizing::kGrow);
            });
    removed->layoutWithInlineExtent(90);
    EXPECT_TRUE(removed->didExceedMaxLines());
    EXPECT_TRUE(TightBoxes(removed.get(), 2, 5).empty());
    EXPECT_TRUE(TateChuYokoRuns(SnapshotRuns(removed.get())).empty());

    auto resized = BuildVerticalTateChuYokoModes("天ABC",
            {{{3, 6}, skia_text::TateChuYokoSizing::kGrow}}, style);
    resized->layoutWithInlineExtent(400);
    const auto before = TightBoxes(resized.get(), 1, 4);
    ASSERT_EQ(before.size(), 1u);
    resized->updateFontSize(0, 6, 80);
    resized->layoutWithInlineExtent(800);
    const auto after = TightBoxes(resized.get(), 1, 4);
    ASSERT_EQ(after.size(), 1u);
    EXPECT_GT(after[0].rect.width(), before[0].rect.width());
    const auto resizedRuns = TateChuYokoRuns(SnapshotRuns(resized.get()));
    ASSERT_EQ(resizedRuns.size(), 1u);
    EXPECT_FLOAT_EQ(resizedRuns[0].font.getSize(), 80);
    EXPECT_NEAR(resizedRuns[0].transform.getScaleX(), 1, 0.001);
}

std::unique_ptr<skia_text::Paragraph>
BuildVerticalOrientation(
        const std::string& text,
        std::vector<skia_text::OrientationOverride> overrides,
        const skia_text::TextStyle& textStyle = PrimaryStyle(40),
        const BuildOptions& options = {},
        const std::function<void(skia_text::ParagraphBuilder*)>& extra = {}) {
    return BuildCoreParagraph(text, textStyle, options, [&](auto* builder) {
        for (const auto& override : overrides) {
            builder->addOrientationOverride(override.range, override.orientation);
        }
        if (extra) {
            extra(builder);
        }
    });
}

TEST_F(SkiaVerticalParagraph, OR01_OR02_ParagraphUprightAndSidewaysOverrideEveryGrapheme) {
    const std::string text = "ABC漢12";
    const skia_text::TextRange all{0, text.size()};
    auto upright = BuildVerticalOrientation(
            text, {{all, skia_text::VerticalTextOrientation::kUpright}});
    auto sideways = BuildVerticalOrientation(
            text, {{all, skia_text::VerticalTextOrientation::kSideways}});
    auto mixed = BuildCoreParagraph(text, PrimaryStyle(40));
    upright->layoutWithInlineExtent(1000);
    sideways->layoutWithInlineExtent(1000);
    mixed->layoutWithInlineExtent(1000);

    const auto uprightRuns = SnapshotRuns(upright.get());
    const auto sidewaysRuns = SnapshotRuns(sideways.get());
    ASSERT_EQ(SourceGlyphCount(uprightRuns), Utf16Length(text));
    ASSERT_EQ(SourceGlyphCount(sidewaysRuns), Utf16Length(text));
    for (const auto& run : uprightRuns) {
        EXPECT_FALSE(run.sideways);
        EXPECT_TRUE(IsIdentity(run.transform));
    }
    for (const auto& run : sidewaysRuns) {
        EXPECT_TRUE(run.sideways);
        EXPECT_TRUE(IsClockwise(run.transform));
    }

    bool mixedHasUpright = false;
    bool mixedHasSideways = false;
    for (const auto& run : SnapshotRuns(mixed.get())) {
        mixedHasUpright |= !run.sideways;
        mixedHasSideways |= run.sideways;
    }
    EXPECT_TRUE(mixedHasUpright);
    EXPECT_TRUE(mixedHasSideways);
}

TEST_F(SkiaVerticalParagraph, OR03_OR04_SpanBoundariesAndMixedRestoreIcuClassification) {
    auto span = BuildVerticalOrientation("A漢B",
            {{{0, 1}, skia_text::VerticalTextOrientation::kUpright},
             {{1, 4}, skia_text::VerticalTextOrientation::kMixed},
             {{4, 5}, skia_text::VerticalTextOrientation::kSideways}});
    span->layoutWithInlineExtent(1000);
    const auto runs = SnapshotRuns(span.get());
    const auto* latinA = FindVisitorRun(runs, 0, 0);
    const auto* han = FindVisitorRun(runs, 0, 1);
    const auto* latinB = FindVisitorRun(runs, 0, 4);
    ASSERT_NE(latinA, nullptr);
    ASSERT_NE(han, nullptr);
    ASSERT_NE(latinB, nullptr);
    EXPECT_FALSE(latinA->sideways);
    EXPECT_FALSE(han->sideways);
    EXPECT_TRUE(latinB->sideways);

    auto sidewaysHan = BuildVerticalOrientation(
            "漢", {{{0, 3}, skia_text::VerticalTextOrientation::kSideways}});
    sidewaysHan->layoutWithInlineExtent(400);
    const auto sidewaysHanRuns = SnapshotRuns(sidewaysHan.get());
    const auto* forced = FindVisitorRun(sidewaysHanRuns, 0, 0);
    ASSERT_NE(forced, nullptr);
    EXPECT_TRUE(forced->sideways);

    auto restored = BuildVerticalOrientation("A漢",
            {{{0, 1}, skia_text::VerticalTextOrientation::kSideways},
             {{1, 4}, skia_text::VerticalTextOrientation::kMixed}});
    restored->layoutWithInlineExtent(400);
    const auto restoredRuns = SnapshotRuns(restored.get());
    const auto* restoredA = FindVisitorRun(restoredRuns, 0, 0);
    const auto* restoredHan = FindVisitorRun(restoredRuns, 0, 1);
    ASSERT_NE(restoredA, nullptr);
    ASSERT_NE(restoredHan, nullptr);
    EXPECT_TRUE(restoredA->sideways);
    EXPECT_FALSE(restoredHan->sideways);
}

TEST_F(SkiaVerticalParagraph, OR05_OR06_GeometryJustifyAndEllipsisKeepMixedRules) {
    auto style = PrimaryStyle(40);
    style.setHeightOverride(true);
    style.setHeight(1.5f);
    const std::string text = "天地";
    const skia_text::TextRange all{0, text.size()};
    for (auto orientation : {skia_text::VerticalTextOrientation::kMixed,
                             skia_text::VerticalTextOrientation::kUpright,
                             skia_text::VerticalTextOrientation::kSideways}) {
        auto paragraph = BuildVerticalOrientation(
                text,
                orientation == skia_text::VerticalTextOrientation::kMixed
                        ? std::vector<skia_text::OrientationOverride>{}
                        : std::vector<skia_text::OrientationOverride>{{all, orientation}},
                style, {.align = skia_text::TextAlign::kCenter, .indent = 10});
        paragraph->layoutWithInlineExtent(50);
        const auto& metrics = paragraph->getLayoutMetrics();
        ASSERT_EQ(metrics.columns.size(), 2u);
        for (const auto& column : metrics.columns) {
            EXPECT_NEAR(column.blockThickness, 60, kTolerance);
        }
        EXPECT_NEAR(metrics.physicalSize.height(), 50, kTolerance);
    }

    const std::string justifiedText = "天地玄黃宇宙";
    for (auto orientation : {skia_text::VerticalTextOrientation::kUpright,
                             skia_text::VerticalTextOrientation::kSideways}) {
        auto paragraph = BuildVerticalOrientation(justifiedText,
                {{{0, justifiedText.size()}, orientation}},
                PrimaryStyle(40), {.align = skia_text::TextAlign::kJustify});
        paragraph->layoutWithInlineExtent(172);
        const auto columns = ImplOf(paragraph.get())->verticalColumns();
        ASSERT_EQ(columns.size(), 2u);
        EXPECT_TRUE(columns[0].justified);
        EXPECT_EQ(columns[0].justificationOpportunityCount, 3u);
    }

    auto measure = BuildParagraph("天", PrimaryStyle(40), {});
    measure->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    auto ellipsized = BuildEllipsisParagraph("天地玄黃", PrimaryStyle(40), 1, "\xE2\x80\xA6",
            skia_text::TextAlign::kStart,
            [](auto* builder) {
                builder->addOrientationOverride(
                        {0, 12}, skia_text::VerticalTextOrientation::kSideways);
            });
    ellipsized->layoutWithInlineExtent(advance * 2 + 4);
    EXPECT_TRUE(ellipsized->didExceedMaxLines());
    const auto affix = SyntheticRuns(SnapshotRuns(ellipsized.get()));
    ASSERT_EQ(affix.size(), 1u);
    EXPECT_FALSE(affix[0].sideways);
    EXPECT_TRUE(IsIdentity(affix[0].transform));
}

TEST_F(SkiaVerticalParagraph, OR07_EffectsAndBaselineShiftComposeWithBothOrientations) {
    auto style = PrimaryStyle(40);
    SkPaint background;
    background.setColor(SK_ColorGREEN);
    style.setBackgroundPaint(background);
    style.setDecoration(skia_text::TextDecoration::kUnderline);
    style.addShadow(skia_text::TextShadow(SK_ColorBLUE, {4, 6}, 2));
    style.setBaselineShift(12);
    for (auto orientation : {skia_text::VerticalTextOrientation::kUpright,
                             skia_text::VerticalTextOrientation::kSideways}) {
        auto paragraph = BuildVerticalOrientation("天A",
                {{{0, 4}, orientation}}, style);
        paragraph->layoutWithInlineExtent(400);
        RecordingPainter painter;
        paragraph->paint(&painter, 0, 0);
        EXPECT_GT(painter.backgrounds.size(), 0u);
        EXPECT_GT(painter.decorations.size(), 0u);
        EXPECT_GT(painter.shadows.size(), 0u);
        EXPECT_GT(painter.textBlobCount, 0);
        EXPECT_FALSE(TightBoxes(paragraph.get(), 0, 3).empty());
        SkPath path;
        EXPECT_EQ(paragraph->getPath(0, &path), 0);
        EXPECT_FALSE(path.isEmpty());
    }
}

TEST_F(SkiaVerticalParagraph, OR08_RubyAnnotationStaysMixedWhileBaseFollowsOverride) {
    const std::string text = "漢ABC";
    auto ruby = BuildVerticalOrientation(text,
            {{{0, 3}, skia_text::VerticalTextOrientation::kMixed},
             {{3, 6}, skia_text::VerticalTextOrientation::kSideways}},
            PrimaryStyle(40),
            {},
            [&](auto* builder) {
                builder->addRuby(skia_text::RubyAnnotation({0, text.size()}, "か"));
            });
    ruby->layoutWithInlineExtent(1000);
    const auto runs = SnapshotRuns(ruby.get());
    const auto* baseHan = FindVisitorRun(runs, 0, 0);
    const auto* baseLatin = FindVisitorRun(runs, 0, 3);
    ASSERT_NE(baseHan, nullptr);
    ASSERT_NE(baseLatin, nullptr);
    EXPECT_FALSE(baseHan->sideways);
    EXPECT_TRUE(baseLatin->sideways);

    bool hasRuby = false;
    for (const auto& run : runs) {
        if ((run.flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag) == 0) {
            continue;
        }
        hasRuby = true;
        EXPECT_FALSE(run.sideways);
        EXPECT_TRUE(IsIdentity(run.transform));
    }
    EXPECT_TRUE(hasRuby);
}

TEST_F(SkiaVerticalParagraph, OR09_SidewaysDisablesTateChuYokoWhileMixedAndUprightKeepIt) {
    auto sideways = BuildVerticalOrientation("ABC",
            {{{0, 3}, skia_text::VerticalTextOrientation::kSideways}},
            PrimaryStyle(40), {},
            [](auto* builder) { builder->addTateChuYoko({0, 3}); });
    sideways->layoutWithInlineExtent(400);
    EXPECT_TRUE(TateChuYokoRuns(SnapshotRuns(sideways.get())).empty());
    for (const auto& run : SnapshotRuns(sideways.get())) {
        EXPECT_TRUE(run.sideways);
    }

    for (auto orientation : {skia_text::VerticalTextOrientation::kMixed,
                             skia_text::VerticalTextOrientation::kUpright}) {
        auto paragraph = BuildVerticalOrientation("ABC",
                orientation == skia_text::VerticalTextOrientation::kMixed
                        ? std::vector<skia_text::OrientationOverride>{}
                        : std::vector<skia_text::OrientationOverride>{{{0, 3}, orientation}},
                PrimaryStyle(40), {},
                [](auto* builder) { builder->addTateChuYoko({0, 3}); });
        paragraph->layoutWithInlineExtent(400);
        const auto tcy = TateChuYokoRuns(SnapshotRuns(paragraph.get()));
        ASSERT_EQ(tcy.size(), 1u);
        EXPECT_FALSE(tcy[0].sideways);
        EXPECT_GT(tcy[0].transform.getScaleX(), 0);
    }
}

TEST_F(SkiaVerticalParagraph, OR10_OR11_QueriesValidationPlaceholderControlsAndHorizontalNoOp) {
    for (auto orientation : {skia_text::VerticalTextOrientation::kUpright,
                             skia_text::VerticalTextOrientation::kSideways}) {
        auto paragraph = BuildVerticalOrientation("A漢",
                {{{0, 4}, orientation}});
        paragraph->layoutWithInlineExtent(400);
        const auto boxes = TightBoxes(paragraph.get(), 0, 4);
        ASSERT_FALSE(boxes.empty());
        SkPath path;
        EXPECT_EQ(paragraph->getPath(0, &path), 0);
        EXPECT_FALSE(path.isEmpty());
        const auto position = paragraph->getGlyphPositionAtCoordinate(
                boxes.front().rect.centerX(), boxes.front().rect.centerY());
        EXPECT_GE(position.position, 0u);
        EXPECT_LE(position.position, Utf16Length("A漢"));
        for (const auto& run : SnapshotRuns(paragraph.get())) {
            EXPECT_EQ(run.flags & (skia_text::Paragraph::kRubyAnnotation_VisitorFlag |
                                   skia_text::Paragraph::kTateChuYoko_VisitorFlag),
                      0u);
        }
    }

    auto expectInvalid = [](const std::string& text,
                            std::vector<skia_text::OrientationOverride> overrides) {
        auto paragraph = BuildVerticalOrientation(text, std::move(overrides));
        EXPECT_THROW(paragraph->layoutWithInlineExtent(400), std::invalid_argument);
        EXPECT_THROW(paragraph->getLayoutMetrics(), std::logic_error);
    };
    expectInvalid("", {{{0, 0}, skia_text::VerticalTextOrientation::kUpright}});
    expectInvalid("A", {{{0, 2}, skia_text::VerticalTextOrientation::kUpright}});
    expectInvalid("e\xCC\x81", {{{0, 1}, skia_text::VerticalTextOrientation::kUpright}});
    expectInvalid("ABC",
            {{{0, 2}, skia_text::VerticalTextOrientation::kUpright},
             {{1, 3}, skia_text::VerticalTextOrientation::kSideways}});
    expectInvalid("A", {{{0, 1}, static_cast<skia_text::VerticalTextOrientation>(3)}});

    auto placeholder = BuildCustomParagraph({}, [](auto* builder) {
        builder->pushStyle(PrimaryStyle(40));
        builder->addText("1", 1);
        builder->addPlaceholder(MiddlePlaceholder(20, 20));
        builder->addText("2", 1);
        builder->pop();
        builder->addOrientationOverride({0, 5}, skia_text::VerticalTextOrientation::kSideways);
    });
    EXPECT_NO_THROW(placeholder->layoutWithInlineExtent(400));
    EXPECT_NEAR(placeholder->getLayoutMetrics().physicalSize.width(), 40, kTolerance);

    auto controls = BuildVerticalOrientation("\xE2\x80\x8E" "A",
            {{{0, 4}, skia_text::VerticalTextOrientation::kSideways}});
    EXPECT_NO_THROW(controls->layoutWithInlineExtent(400));

    auto plain = BuildCoreParagraph("12", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb});
    auto ignored = BuildCoreParagraph("12", PrimaryStyle(40),
            {.writingMode = skia_text::WritingMode::kHorizontalTb},
            [](auto* builder) {
                builder->addOrientationOverride(
                        {0, 99}, skia_text::VerticalTextOrientation::kSideways);
            });
    plain->layout(400);
    ignored->layout(400);
    ExpectSnapshotsNear(Capture(plain.get()), Capture(ignored.get()));
}

TEST_F(SkiaVerticalParagraph, OR13_OrientationCacheIsolationAndFailureRecovery) {
    auto* cache = TestFontCollection()->getParagraphCache();
    ASSERT_NE(cache, nullptr);
    cache->reset();
    cache->turnOn(true);

    auto upright = BuildVerticalOrientation("ABC",
            {{{0, 3}, skia_text::VerticalTextOrientation::kUpright}});
    auto sideways = BuildVerticalOrientation("ABC",
            {{{0, 3}, skia_text::VerticalTextOrientation::kSideways}});
    auto mixed = BuildCoreParagraph("ABC", PrimaryStyle(40));
    upright->layoutWithInlineExtent(400);
    sideways->layoutWithInlineExtent(400);
    mixed->layoutWithInlineExtent(400);
    for (const auto& run : SnapshotRuns(upright.get())) EXPECT_FALSE(run.sideways);
    for (const auto& run : SnapshotRuns(sideways.get())) EXPECT_TRUE(run.sideways);
    bool mixedHasSideways = false;
    for (const auto& run : SnapshotRuns(mixed.get())) mixedHasSideways |= run.sideways;
    EXPECT_TRUE(mixedHasSideways);

    EXPECT_THROW(upright->layoutWithInlineExtent(-1), std::invalid_argument);
    upright->layoutWithInlineExtent(400);
    for (const auto& run : SnapshotRuns(upright.get())) EXPECT_FALSE(run.sideways);
    upright->updateFontSize(0, 3, 60);
    upright->layoutWithInlineExtent(600);
    for (const auto& run : SnapshotRuns(upright.get())) {
        EXPECT_FALSE(run.sideways);
        EXPECT_FLOAT_EQ(run.font.getSize(), 60);
    }

    cache->reset();
    cache->turnOn(false);
}


TEST_F(SkiaVerticalParagraph, HorizontalJustifiedSoftHyphenFitsLine) {
    auto make = [&](const std::string& text, skia_text::TextDirection direction) {
        return BuildConfiguredParagraph(text, PrimaryStyle(20), [&](auto& style) {
            style.setWritingMode(skia_text::WritingMode::kHorizontalTb);
            style.setRenderSoftHyphens(true);
            style.setApplyRoundingHack(false);
            style.setTextAlign(skia_text::TextAlign::kJustify);
            style.setTextDirection(direction);
        });
    };
    auto measure = make("xy abcd-", skia_text::TextDirection::kLtr);
    measure->layout(10000);
    const auto width = measure->getMaxIntrinsicWidth() + 5;
    for (auto direction : {skia_text::TextDirection::kLtr, skia_text::TextDirection::kRtl}) {
        auto paragraph = make("xy abcd\u00ADefgh", direction);
        paragraph->layout(width);
        auto lines = static_cast<skia_text::ParagraphImpl*>(paragraph.get())->lines();
        ASSERT_EQ(lines.size(), 2u);
        ASSERT_NE(lines[0].hyphen(), nullptr);
        auto boxes = paragraph->getRectsForRange(0, 8, skia_text::RectHeightStyle::kTight,
                                                 skia_text::RectWidthStyle::kTight);
        ASSERT_FALSE(boxes.empty());
        SkRect extent = SkRect::MakeEmpty();
        for (const auto& box : boxes) extent.join(box.rect);
        EXPECT_NEAR(extent.left(), 0, 0.01f);
        EXPECT_NEAR(extent.right(), width, 0.01f);
    }
}

TEST_F(SkiaVerticalParagraph, HorizontalSoftHyphenAndEllipsisHitsStayUpstream) {
    for (bool ellipsis : {false, true}) {
        auto make = [&](const std::string& text) {
            return BuildConfiguredParagraph(text, PrimaryStyle(20), [&](auto& style) {
                style.setWritingMode(skia_text::WritingMode::kHorizontalTb);
                style.setRenderSoftHyphens(true);
                style.setApplyRoundingHack(false);
                if (ellipsis) {
                    style.setMaxLines(1);
                    style.setEllipsis(SkString("..."));
                }
            });
        };
        auto measure = make(ellipsis ? "abc..." : "inter-");
        measure->layout(10000);
        const auto width = measure->getMaxIntrinsicWidth() + 1;
        auto paragraph = make(ellipsis ? "abcdef" : "inter\u00ADnational");
        paragraph->layout(width);
        auto lines = static_cast<skia_text::ParagraphImpl*>(paragraph.get())->lines();
        if (ellipsis) {
            ASSERT_EQ(lines.size(), 1u);
        } else {
            ASSERT_GE(lines.size(), 2u);
        }
        auto& line = lines[0];
        ASSERT_NE(ellipsis ? line.ellipsis() : line.hyphen(), nullptr);
        const auto x = line.offset().fX + (line.widthWithoutEllipsis() + line.width()) / 2;
        const auto y = line.offset().fY + line.height() / 2;
        auto hit = paragraph->getGlyphPositionAtCoordinate(x, y);
        EXPECT_EQ(hit.position, ellipsis ? 3 : 6);
        EXPECT_EQ(hit.affinity, skia_text::Affinity::kUpstream);
        skia_text::Paragraph::GlyphInfo glyph;
        ASSERT_TRUE(paragraph->getClosestUTF16GlyphInfoAt(x, y, &glyph));
        EXPECT_EQ(glyph.fGraphemeClusterTextRange,
                  skia_text::TextRange(ellipsis ? 2 : 5, ellipsis ? 3 : 6));
    }
}

TEST_F(SkiaVerticalParagraph, V85_MixedCjkAndSidewaysLineThroughShareColumnAxis) {
    const skia_text::TextDecoration decoration = skia_text::TextDecoration::kLineThrough;
    auto cjk = DecorationStyleFor(PrimaryStyle(48), decoration, SK_ColorRED);
    auto latin = DecorationStyleFor(LatinStyle(48), decoration, SK_ColorRED);
    auto paragraph = BuildCustomParagraph({}, [&](skia_text::ParagraphBuilder* builder) {
        builder->pushStyle(cjk);
        builder->addText("天", 3);
        builder->pop();
        builder->pushStyle(latin);
        builder->addText("test", 4);
        builder->pop();
    });
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

    const auto runs = SnapshotRuns(paragraph.get());
    const VisitorRun* cjkRun = FindVisitorRun(runs, 0, 0);
    const VisitorRun* latinRun = FindVisitorRun(runs, 0, 3);
    ASSERT_NE(cjkRun, nullptr);
    ASSERT_NE(latinRun, nullptr);
    ASSERT_TRUE(latinRun->sideways);

    const auto decorations = RecordDecorations(paragraph.get());
    ASSERT_FALSE(decorations.empty());
    EXPECT_NEAR(LineThroughCenterX(*cjkRun), LineThroughCenterX(*latinRun), kTolerance);
    for (const auto& decoration: decorations) {
        EXPECT_NEAR(decoration.rect.centerX(), LineThroughCenterX(*cjkRun), kTolerance);
        EXPECT_NEAR(decoration.rect.centerX(), LineThroughCenterX(*latinRun), kTolerance);
    }
}

TEST_F(SkiaVerticalParagraph, V86_VerticalPunctuationFallbackStaysOnInlineAxis) {
    constexpr SkScalar kSize = 48;
    auto style = PrimaryStyle(kSize);
    style.addFontFeature(SkString("chws"), 1);
    auto paragraph = BuildParagraph("，，，，", style, {});
    paragraph->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

    const auto runs = SnapshotRuns(paragraph.get());
    std::vector<SkPoint> origins;
    for (const auto& run: runs) {
        const auto runOrigins = AbsoluteGlyphOrigins(run);
        origins.insert(origins.end(), runOrigins.begin(), runOrigins.end());
    }
    ASSERT_EQ(origins.size(), 4u);
    for (size_t index = 1; index < origins.size(); ++index) {
        EXPECT_NEAR(origins[index].x(), origins.front().x(), kTolerance) << index;
        EXPECT_GT(origins[index].y(), origins[index - 1].y());
        EXPECT_LT(origins[index].y() - origins[index - 1].y(), kSize - 1) << index;
    }
}

TEST_F(SkiaVerticalParagraph, V87_VerticalPunctuationUsesVchwWhenAvailable) {
    constexpr SkScalar kSize = 48;
    auto enabledStyle = FamilyStyle("LXGW WenKai", kSize);
    enabledStyle.addFontFeature(SkString("chws"), 1);
    auto disabledStyle = enabledStyle;
    disabledStyle.resetFontFeatures();
    disabledStyle.addFontFeature(SkString("chws"), 0);

    auto enabled = BuildParagraph("、「", enabledStyle, {});
    auto disabled = BuildParagraph("、「", disabledStyle, {});
    enabled->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());
    disabled->layoutWithInlineExtent(std::numeric_limits<SkScalar>::infinity());

    const auto enabledExtent = enabled->getLayoutMetrics().longestInlineExtent;
    const auto disabledExtent = disabled->getLayoutMetrics().longestInlineExtent;
    EXPECT_GT(disabledExtent - enabledExtent, 10) << "enabled=" << enabledExtent
                                                  << " disabled=" << disabledExtent;

    const auto runs = SnapshotRuns(enabled.get());
    std::vector<SkPoint> origins;
    for (const auto& run: runs) {
        const auto runOrigins = AbsoluteGlyphOrigins(run);
        origins.insert(origins.end(), runOrigins.begin(), runOrigins.end());
    }
    ASSERT_EQ(origins.size(), 2u);
    EXPECT_NEAR(origins[1].x(), origins[0].x(), kTolerance);
    EXPECT_LT(origins[1].y() - origins[0].y(), kSize - 1);
}

TEST_F(SkiaVerticalParagraph, V88_TateChuYokoIgnoresInheritedLineHeightBaseline) {
    auto build = [](SkScalar height) {
        auto style = PrimaryStyle(40);
        style.setHeightOverride(true);
        style.setHeight(height);
        auto paragraph = BuildVerticalTateChuYoko("漢12漢", {{3, 5}}, style);
        paragraph->layoutWithInlineExtent(400);
        return paragraph;
    };
    auto natural = build(1);
    auto expanded = build(2.2f);

    const auto naturalRuns = TateChuYokoRuns(SnapshotRuns(natural.get()));
    const auto expandedRuns = TateChuYokoRuns(SnapshotRuns(expanded.get()));
    ASSERT_EQ(naturalRuns.size(), 1u);
    ASSERT_EQ(expandedRuns.size(), 1u);
    EXPECT_NEAR(expandedRuns[0].origin.y(), naturalRuns[0].origin.y(), kTolerance);
    ASSERT_EQ(expandedRuns[0].positions.size(), naturalRuns[0].positions.size());
    for (size_t index = 0; index < expandedRuns[0].positions.size(); ++index) {
        EXPECT_NEAR(expandedRuns[0].positions[index].y(),
                    naturalRuns[0].positions[index].y(), kTolerance);
    }
    ASSERT_EQ(natural->getLayoutMetrics().columns.size(), 1u);
    ASSERT_EQ(expanded->getLayoutMetrics().columns.size(), 1u);
    EXPECT_NEAR(expanded->getLayoutMetrics().columns[0].blockThickness, 88, kTolerance);

    const auto boxes = TightBoxes(expanded.get(), 1, 3);
    ASSERT_EQ(boxes.size(), 1u);
    SkRect ink = SkRect::MakeEmpty();
    for (size_t index = 0; index < expandedRuns[0].glyphs.size(); ++index) {
        const SkRect bounds = expandedRuns[0].transform.mapRect(expandedRuns[0].bounds[index]);
        const SkPoint origin = expandedRuns[0].origin + expandedRuns[0].positions[index];
        const SkRect glyph = SkRect::MakeLTRB(bounds.left() + origin.x(),
                                              bounds.top() + origin.y(),
                                              bounds.right() + origin.x(),
                                              bounds.bottom() + origin.y());
        if (ink.isEmpty()) {
            ink = glyph;
        } else {
            ink.join(glyph);
        }
    }
    EXPECT_GE(ink.top(), boxes[0].rect.top() - 1);
    EXPECT_LE(ink.bottom(), boxes[0].rect.bottom() + 1);
}

} // namespace
