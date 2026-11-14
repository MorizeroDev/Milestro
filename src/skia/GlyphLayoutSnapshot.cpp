#include "Milestro/skia/GlyphLayoutSnapshot.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "include/core/SkBitmap.h"
#include "include/core/SkTypeface.h"
#include "modules/skparagraph/include/ParagraphPainter.h"
#include "src/core/SkTextBlobPriv.h"
#include <cmath>
#include <cstring>
#include <limits>

namespace milestro::skia {
namespace {

uint32_t FontFlags(const SkFont& font) {
    return uint32_t(font.isForceAutoHinting()) | (uint32_t(font.isEmbeddedBitmaps()) << 1) |
           (uint32_t(font.isSubpixel()) << 2) | (uint32_t(font.isLinearMetrics()) << 3) |
           (uint32_t(font.isEmbolden()) << 4) | (uint32_t(font.isBaselineSnap()) << 5) |
           (uint32_t(font.getEdging()) << 8) | (uint32_t(font.getHinting()) << 16);
}

bool SupportsFont(const SkFont& font) {
    const auto* face = font.getTypeface();
    if (face == nullptr || !std::isfinite(font.getSize()) || font.getSize() < 0 ||
        !std::isfinite(font.getScaleX()) || !std::isfinite(font.getSkewX())) {
        return false;
    }
    return face->getTableSize(SkSetFourByteTag('C', 'O', 'L', 'R')) == 0 &&
           face->getTableSize(SkSetFourByteTag('C', 'B', 'D', 'T')) == 0 &&
           face->getTableSize(SkSetFourByteTag('s', 'b', 'i', 'x')) == 0 &&
           face->getTableSize(SkSetFourByteTag('S', 'V', 'G', ' ')) == 0;
}

class GlyphCapturePainter final : public ::skia::textlayout::ParagraphPainter {
public:
    explicit GlyphCapturePainter(GlyphLayoutSnapshot& target) : target(target) {}
    bool supported = true;

    void drawTextBlob(const sk_sp<SkTextBlob>& blob, SkScalar originX, SkScalar baselineY,
                      const SkPaintOrID& paintValue) override {
        const auto* paint = std::get_if<SkPaint>(&paintValue);
        if (paint == nullptr || paint->getStyle() != SkPaint::kFill_Style ||
            paint->getShader() || paint->getColorFilter() || paint->getMaskFilter() ||
            paint->getImageFilter() || paint->getPathEffect() ||
            paint->asBlendMode() != SkBlendMode::kSrcOver) {
            supported = false;
            return;
        }
        if (!blob) {
            return;
        }
        for (SkTextBlobRunIterator iterator(blob.get()); !iterator.done(); iterator.next()) {
            GlyphLayoutSnapshot::Run run;
            run.font = iterator.font();
            run.color = paint->getColor();
            run.glyphs.assign(iterator.glyphs(), iterator.glyphs() + iterator.glyphCount());
            run.positions.resize(run.glyphs.size());
            const SkPoint origin = iterator.offset() + SkPoint::Make(originX, baselineY) + translation;
            switch (iterator.positioning()) {
                case SkTextBlobRunIterator::kDefault_Positioning:
                    run.font.getPos(run.glyphs, run.positions, origin);
                    break;
                case SkTextBlobRunIterator::kHorizontal_Positioning:
                    for (size_t index = 0; index < run.positions.size(); ++index) {
                        run.positions[index] = origin + SkPoint::Make(iterator.pos()[index], 0);
                    }
                    break;
                case SkTextBlobRunIterator::kFull_Positioning:
                    for (size_t index = 0; index < run.positions.size(); ++index) {
                        run.positions[index] = origin + iterator.points()[index];
                    }
                    break;
                default:
                    supported = false;
                    return;
            }
            target.appendRun(std::move(run));
        }
    }
    void drawTextShadow(const sk_sp<SkTextBlob>&, SkScalar, SkScalar, SkColor, SkScalar) override { supported = false; }
    void drawRect(const SkRect&, const SkPaintOrID&) override { supported = false; }
    void drawFilledRect(const SkRect&, const DecorationStyle&) override { supported = false; }
    void drawPath(const SkPath&, const DecorationStyle&) override { supported = false; }
    void drawLine(SkScalar, SkScalar, SkScalar, SkScalar, const DecorationStyle&) override { supported = false; }
    void clipRect(const SkRect&) override { supported = false; }
    void translate(SkScalar offsetX, SkScalar offsetY) override { translation += SkPoint::Make(offsetX, offsetY); }
    void scale(SkScalar, SkScalar) override { supported = false; }
    void rotate(SkScalar) override { supported = false; }
    void save() override { translations.push_back(translation); }
    void restore() override {
        if (translations.empty()) { supported = false; return; }
        translation = translations.back();
        translations.pop_back();
    }
private:
    GlyphLayoutSnapshot& target;
    SkPoint translation = {0, 0};
    std::vector<SkPoint> translations;
};

}

void GlyphLayoutSnapshot::updateSimpleText(const SkFont& font, const char* text, size_t size, SkColor color) {
    inkBounds.setEmpty();
    advance = 0;
    runs.resize(1);
    auto& run = runs.front();
    run.font = font;
    run.color = color;
    const int count = size == 0 ? 0 : font.countText(text, size, SkTextEncoding::kUTF8);
    run.glyphs.resize(count);
    run.bounds.resize(count);
    run.positions.resize(count);
    if (count == 0) { return; }
    font.textToGlyphs(text, size, SkTextEncoding::kUTF8, run.glyphs);
    simpleAdvances.resize(count);
    font.getWidthsBounds(run.glyphs, simpleAdvances, run.bounds, nullptr);
    for (size_t index = 0; index < run.glyphs.size(); ++index) {
        run.positions[index] = SkPoint::Make(advance, 0);
        inkBounds.join(run.bounds[index].makeOffset(run.positions[index]));
        advance += simpleAdvances[index];
    }
}

bool GlyphLayoutSnapshot::copySimpleText(const GlyphLayoutSnapshot& source, const SkFont& font, SkColor color) {
    if (source.runs.size() != 1 || source.runs.front().font != font) { return false; }
    runs.resize(1);
    runs.front() = source.runs.front();
    runs.front().color = color;
    advance = source.advance;
    inkBounds = source.inkBounds;
    return true;
}

void GlyphLayoutSnapshot::appendRun(Run run) {
    if (run.bounds.empty()) {
        run.bounds.resize(run.glyphs.size());
        run.font.getBounds(run.glyphs, run.bounds, nullptr);
    }
    for (size_t index = 0; index < run.glyphs.size(); ++index) {
        inkBounds.join(run.bounds[index].makeOffset(run.positions[index]));
    }
    runs.push_back(std::move(run));
}

void GlyphLayoutSnapshot::paint(SkCanvas* canvas, SkScalar originX, SkScalar baselineY) const {
    if (!canvas) { return; }
    for (const auto& run : runs) {
        SkPaint paint;
        paint.setColor(run.color);
        canvas->drawGlyphs(run.glyphs, run.positions, SkPoint::Make(originX, baselineY), run.font, paint);
    }
}

SkScalar GlyphLayoutSnapshot::measure(SkRect* bounds) const {
    if (bounds) { *bounds = inkBounds; }
    return advance;
}

bool GlyphLayoutSnapshot::supportsAtlas() const {
    for (const auto& run : runs) {
        if (!SupportsFont(run.font)) { return false; }
        for (const auto& point : run.positions) {
            if (!std::isfinite(point.x()) || !std::isfinite(point.y())) { return false; }
        }
        for (const auto& bounds : run.bounds) {
            if (!bounds.isFinite()) { return false; }
        }
    }
    return true;
}

size_t GlyphLayoutSnapshot::glyphCount() const {
    size_t count = 0;
    for (const auto& run : runs) { count += run.glyphs.size(); }
    return count;
}

bool GlyphLayoutSnapshot::copyGlyphs(void* destination, size_t capacityBytes) const {
    const size_t count = glyphCount();
    if (count > capacityBytes / sizeof(GlyphLayoutDescriptor) || (count > 0 && !destination)) { return false; }
    auto* output = static_cast<uint8_t*>(destination);
    for (const auto& run : runs) {
        for (size_t index = 0; index < run.glyphs.size(); ++index) {
            const auto& bounds = run.bounds[index];
            const auto& position = run.positions[index];
            const GlyphLayoutDescriptor descriptor = {
                run.font.getTypeface()->uniqueID(), run.glyphs[index], FontFlags(run.font), run.color,
                run.font.getSize(), run.font.getScaleX(), run.font.getSkewX(), position.x(), position.y(),
                bounds.left(), bounds.top(), bounds.right(), bounds.bottom()};
            std::memcpy(output, &descriptor, sizeof(descriptor));
            output += sizeof(descriptor);
        }
    }
    return true;
}

const GlyphLayoutSnapshot::Run* GlyphLayoutSnapshot::findGlyph(size_t& index) const {
    for (const auto& run : runs) {
        if (index < run.glyphs.size()) { return &run; }
        index -= run.glyphs.size();
    }
    return nullptr;
}

bool GlyphLayoutSnapshot::rasterBounds(size_t index, float scale, SkIRect& bounds) const {
    const auto* run = findGlyph(index);
    if (!run || !std::isfinite(scale) || scale <= 0 || !SupportsFont(run->font)) { return false; }
    SkFont font = run->font;
    font.setSize(font.getSize() * scale);
    font.setEdging(SkFont::Edging::kAntiAlias);
    if (!std::isfinite(font.getSize())) { return false; }
    SkRect rect = font.getBounds(run->glyphs[index], nullptr);
    if (!rect.isFinite() || rect.width() > 16382 || rect.height() > 16382 ||
        std::abs(rect.left()) > 1000000 || std::abs(rect.top()) > 1000000) { return false; }
    if (rect.isEmpty()) { bounds.setEmpty(); return true; }
    rect.outset(1, 1);
    rect.roundOut(&bounds);
    return true;
}

bool GlyphLayoutSnapshot::rasterize(size_t index, float scale, uint8_t* pixels, size_t capacity) const {
    SkIRect bounds;
    if (!rasterBounds(index, scale, bounds)) { return false; }
    const size_t required = size_t(bounds.width()) * size_t(bounds.height());
    if (required == 0) { return true; }
    if (!pixels || capacity < required) { return false; }
    const auto* run = findGlyph(index);
    SkFont font = run->font;
    font.setSize(font.getSize() * scale);
    font.setEdging(SkFont::Edging::kAntiAlias);
    SkBitmap bitmap;
    if (!bitmap.installPixels(SkImageInfo::MakeA8(bounds.width(), bounds.height()), pixels, bounds.width())) { return false; }
    bitmap.eraseColor(SK_ColorTRANSPARENT);
    SkCanvas canvas(bitmap);
    SkPaint paint;
    paint.setColor(SK_ColorWHITE);
    const SkPoint position = SkPoint::Make(-bounds.left(), -bounds.top());
    canvas.drawGlyphs({&run->glyphs[index], 1}, {&position, 1}, SkPoint::Make(0, 0), font, paint);
    return true;
}

std::unique_ptr<GlyphLayoutSnapshot> GlyphLayoutSnapshot::FromParagraph(textlayout::Paragraph& paragraph) {
    if (paragraph.isVertical() || !paragraph.unwrap()->getRectsForPlaceholders().empty()) { return nullptr; }
    auto snapshot = std::make_unique<GlyphLayoutSnapshot>();
    GlyphCapturePainter painter(*snapshot);
    paragraph.unwrap()->paint(&painter, 0, 0);
    return painter.supported && snapshot->supportsAtlas() ? std::move(snapshot) : nullptr;
}

}
