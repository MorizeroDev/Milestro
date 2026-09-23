#include "include/core/SkBitmap.h"
#include "include/core/SkCanvas.h"
#include "include/core/SkTypeface.h"
#include "include/ports/SkFontMgr_empty.h"
#include "modules/skparagraph/include/TypefaceFontProvider.h"
#include "modules/skparagraph/src/ParagraphBuilderImpl.h"
#include "modules/skparagraph/src/experimental/RubyPrototype.h"
#include "modules/skunicode/include/SkUnicode_icu.h"
#include <cmath>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <unicode/udata.h>
#include <unicode/uversion.h>

using namespace skia::textlayout;

int checks = 0;
void require(bool condition, const std::string& message) {
    if (!condition)
        throw std::runtime_error(message);
    ++checks;
}

void near(float actual, float expected, const std::string& message) {
    require(std::abs(actual - expected) < 0.02f, message);
}

struct Fixture {
    sk_sp<SkUnicode> unicode = SkUnicodes::ICU::Make();
    sk_sp<FontCollection> fonts = sk_make_sp<FontCollection>();
    ParagraphStyle style;

    Fixture() {
        auto loader = SkFontMgr_New_Custom_Empty();
        auto provider = sk_make_sp<TypefaceFontProvider>();
        auto add = [&](const std::string& path, const char* alias) {
            auto face = loader->makeFromFile(path.c_str());
            require(face != nullptr, "font load " + path);
            provider->registerTypeface(face, SkString(alias));
            SkString family;
            face->getFamilyName(&family);
            std::cout << "FONT " << alias << " family=" << family.c_str() << " glyphs=" << face->countGlyphs() << '\n';
        };
        add(PROTOTYPE_ROOT "/ext/skia/resources/fonts/Roboto-Regular.ttf", "Latin");
        add(PROTOTYPE_ROOT "/tests/data/font/SourceHanSans-VF.otf.woff2.bytes", "CJK");
        add(PROTOTYPE_ROOT "/tests/data/font/NotoColorEmoji.ttf.bytes", "Emoji");
        add(PROTOTYPE_ROOT "/tests/data/font/LiberationSans-Bold.ttf.bytes", "RTL");
        fonts->setDefaultFontManager(provider);
        TextStyle text;
        text.setFontFamilies({SkString("Latin"), SkString("CJK"), SkString("Emoji"), SkString("RTL")});
        text.setFontSize(32);
        text.setColor(SK_ColorBLACK);
        text.setLocale(SkString("ja"));
        style.setTextStyle(text);
        style.setTextAlign(TextAlign::kLeft);
        style.setApplyRoundingHack(false);
    }

    std::unique_ptr<ParagraphBuilderImpl> builder(const std::string& text, size_t split = 0) {
        auto result = std::make_unique<ParagraphBuilderImpl>(style, fonts, unicode);
        if (split) {
            result->addText(text.data(), split);
            auto changed = style.getTextStyle();
            changed.setFontSize(40);
            result->pushStyle(changed);
            result->addText(text.data() + split, text.size() - split);
        } else {
            result->addText(text.data(), text.size());
        }
        return result;
    }

    std::unique_ptr<Paragraph> plain(const std::string& text, size_t split = 0) {
        auto result = builder(text, split)->Build();
        result->layout(1000);
        return result;
    }

    std::unique_ptr<RubyPrototype>
    ruby(const std::string& text, std::vector<PrototypeRubyInput> inputs, size_t split = 0) {
        return std::make_unique<RubyPrototype>(builder(text, split)->Build(), std::move(inputs), style, fonts, unicode);
    }
};

void dump(const std::string& name, ParagraphImpl& paragraph) {
    std::cout << "SHAPE " << name << " runs=" << paragraph.runs().size()
              << " clusters=" << paragraph.clusters().size() - 1 << '\n';
    for (auto& run: paragraph.runs()) {
        SkString family;
        run.font().getTypeface()->getFamilyName(&family);
        std::cout << " RUN " << run.index() << " bytes=" << run.textRange().start << ':' << run.textRange().end
                  << " family=" << family.c_str() << '\n';
    }
    for (auto& cluster: paragraph.clusters()) {
        std::cout << " CLUSTER " << cluster.textRange().start << ':' << cluster.textRange().end
                  << " glyphs=" << cluster.size() << " advance=" << cluster.width() << '\n';
    }
}

uint64_t plainRaster(Paragraph& paragraph, const std::string& name = "") {
    SkBitmap bitmap;
    bitmap.allocN32Pixels(1024, 512);
    bitmap.eraseColor(SK_ColorTRANSPARENT);
    SkCanvas canvas(bitmap);
    paragraph.paint(&canvas, 8, 8);
    uint64_t hash = 1469598103934665603ULL;
    auto bytes = static_cast<const unsigned char*>(bitmap.getPixels());
    size_t nontransparent = 0;
    for (int row = 0; row < bitmap.height(); ++row) {
        for (int column = 0; column < bitmap.width(); ++column) {
            nontransparent += SkColorGetA(bitmap.getColor(column, row)) != 0;
        }
    }
    require(nontransparent > 0, "original control actually painted glyphs");
    for (size_t offset = 0; offset < bitmap.computeByteSize(); ++offset)
        hash = (hash ^ bytes[offset]) * 1099511628211ULL;
    if (!name.empty()) {
        std::ofstream output(name + ".ppm", std::ios::binary);
        output << "P6\n" << bitmap.width() << ' ' << bitmap.height() << "\n255\n";
        for (int row = 0; row < bitmap.height(); ++row) {
            for (int column = 0; column < bitmap.width(); ++column) {
                auto color = bitmap.getColor(column, row);
                unsigned alpha = SkColorGetA(color);
                const unsigned char pixel[] = {
                        static_cast<unsigned char>(255 - alpha + SkColorGetR(color) * alpha / 255),
                        static_cast<unsigned char>(255 - alpha + SkColorGetG(color) * alpha / 255),
                        static_cast<unsigned char>(255 - alpha + SkColorGetB(color) * alpha / 255)};
                output.write(reinterpret_cast<const char*>(pixel), 3);
            }
        }
    }
    return hash;
}

void raster(const std::string& name, const RubyPrototype& paragraph, float clipInline = -1) {
    SkBitmap bitmap;
    const int margin = 8;
    bitmap.allocN32Pixels(1024, static_cast<int>(std::ceil(paragraph.height())) + margin * 2);
    bitmap.eraseColor(SK_ColorTRANSPARENT);
    SkCanvas canvas(bitmap);
    canvas.translate(margin, margin);
    if (clipInline >= 0)
        canvas.clipRect(SkRect::MakeWH(clipInline, paragraph.height()));
    paragraph.paint(&canvas);
    size_t inkPixels = 0;
    size_t outside = 0;
    std::ofstream output(name + ".ppm", std::ios::binary);
    output << "P6\n" << bitmap.width() << ' ' << bitmap.height() << "\n255\n";
    for (int row = 0; row < bitmap.height(); ++row) {
        for (int column = 0; column < bitmap.width(); ++column) {
            auto color = bitmap.getColor(column, row);
            unsigned alpha = SkColorGetA(color);
            if (alpha) {
                ++inkPixels;
                bool contained = false;
                for (const auto& placement: paragraph.placements()) {
                    auto expanded = placement.ink.makeOutset(2, 2);
                    contained |= expanded.contains(column - margin + 0.5f, row - margin + 0.5f);
                }
                outside += !contained;
            }
            const unsigned char pixel[] = {static_cast<unsigned char>(255 - alpha + SkColorGetR(color) * alpha / 255),
                                           static_cast<unsigned char>(255 - alpha + SkColorGetG(color) * alpha / 255),
                                           static_cast<unsigned char>(255 - alpha + SkColorGetB(color) * alpha / 255)};
            output.write(reinterpret_cast<const char*>(pixel), 3);
        }
    }
    require(inkPixels > 0 && outside == 0, "raster pixels covered by final ink geometry");
    std::cout << "RASTER " << name << " pixels=" << inkPixels << " outside=" << outside << '\n';
}

void checkLayout(RubyPrototype& paragraph, float width) {
    paragraph.layout(width);
    require(!paragraph.lines().empty() && paragraph.lines().size() <= paragraph.units().size() + 1,
            "finite line progress");
    size_t expectedStart = 0;
    for (const auto& line: paragraph.lines()) {
        require(line.start == expectedStart &&
                    (line.end > line.start || line.start == paragraph.units().size()),
                "contiguous unit lines or trailing empty line");
        if (line.hardBreak)
            require(paragraph.units()[line.end - 1].hardBreakAfter, "hard line ends at source break");
        float sum = 0;
        for (size_t index = line.start; index < line.end; ++index) sum += paragraph.units()[index].inlineExtent;
        for (size_t index = line.end; index > line.start && paragraph.units()[index - 1].whitespace; --index)
            sum -= paragraph.units()[index - 1].inlineExtent;
        near(line.inlineExtent, sum, "line advance equals unit extents");
        expectedStart = line.end;
        std::cout << " LINE width=" << width << " units=" << line.start << ':' << line.end
                  << " bytes=" << (line.start == line.end ? paragraph.units().back().text.end
                                                            : paragraph.units()[line.start].text.start)
                  << ':' << (line.start == line.end ? paragraph.units().back().text.end
                                                    : paragraph.units()[line.end - 1].text.end)
                  << " advance=" << line.inlineExtent << " baseline=" << line.baseline
                  << " bottom=" << line.blockEnd << '\n';
    }
    require(expectedStart == paragraph.units().size(), "all units consumed");
    for (const auto& placement: paragraph.placements()) {
        require(placement.ink.top() >= -0.01f && placement.ink.bottom() <= paragraph.height() + 0.01f,
                "ink within measured block extent");
        if (!placement.annotation) {
            auto boxes = paragraph.baseRects(placement.slice.text);
            require(!boxes.empty(), "base-only prototype byte-range geometry exists");
        }
    }
}

void reject(Fixture& fixture, const std::string& text, TextRange range, size_t split, const std::string& reason) {
    bool rejected = false;
    try {
        fixture.ruby(text, {{range, "read"}}, split);
    } catch (const std::runtime_error& error) {
        rejected = std::string(error.what()).find(reason) != std::string::npos;
        std::cout << "REJECT " << range.start << ':' << range.end << ' ' << error.what() << '\n';
    }
    require(rejected, "expected diagnostic " + reason);
}

int main() {
    try {
        std::ifstream data(PROTOTYPE_ROOT "/ext/icu-cmake/common/icudtl.dat", std::ios::binary | std::ios::ate);
        require(data.good(), "ICU data readable");
        auto length = data.tellg();
        std::vector<uint64_t> icuData((static_cast<size_t>(length) + 7) / 8);
        data.seekg(0);
        data.read(reinterpret_cast<char*>(icuData.data()), length);
        UErrorCode status = U_ZERO_ERROR;
        udata_setCommonData(icuData.data(), &status);
        require(U_SUCCESS(status), "ICU data installed");
        std::cout << "UNICODE ICU " << U_ICU_VERSION << " raster CPU FreeType HarfBuzz\n";
        Fixture fixture;
        const std::string text = "「漢字仮名」後 A\xC2\xA0"
                                 "B 後";
        const std::vector<PrototypeRubyInput> inputs = {{{3, 9}, "かんじ"}, {{9, 15}, "とてもながいよみかた"}};
        auto controlBefore = fixture.plain(text);
        auto controlHash = plainRaster(*controlBefore);
        auto ruby = fixture.ruby(text, inputs);
        dump("A", ruby->shapedBase());
        auto unannotated = fixture.ruby(text, {});
        near(unannotated->maxIntrinsic(),
             controlBefore->getLongestLine(),
             "ordinary glyph advance preserved without ruby");
        for (const auto& unit: ruby->units()) {
            std::cout << " UNIT bytes=" << unit.text.start << ':' << unit.text.end << " ruby=" << unit.hasRuby
                      << " advance=" << unit.inlineExtent << " original_break=" << unit.breakAfter << '\n';
            if (unit.text.end == 23 || unit.text.end == 25) {
                require(!unit.breakAfter, "NBSP retains original non-break boundaries");
            }
        }
        for (float width: {1000.f, 120.f, 30.f, 0.f}) {
            auto control = fixture.plain(text);
            control->layout(width);
            std::cout << "CONTROL width=" << width << " lines=" << control->lineNumber()
                      << " height=" << control->getHeight() << " longest=" << control->getLongestLine() << '\n';
            auto name = std::to_string(static_cast<int>(width));
            plainRaster(*control, "A-control-" + name);
            checkLayout(*ruby, width);
            checkLayout(*unannotated, width);
            raster("A-ruby-" + std::to_string(static_cast<int>(width)), *ruby);
        }
        ruby->layout(1000);
        auto saved = ruby->placements().back().origin;
        auto lastOrdinary = ruby->placements().back();
        near(lastOrdinary.origin.x(),
             ruby->maxIntrinsic() - ruby->units().back().inlineExtent,
             "ordinary suffix starts after all expanded ruby units");
        ruby->layout(30);
        ruby->layout(1000);
        require(ruby->placements().back().origin == saved, "resize does not accumulate padding");
        require(ruby->maxIntrinsic() > unannotated->maxIntrinsic(), "real annotation expands width");
        require(ruby->minIntrinsic() > 0, "real intrinsic width positive");
        auto controlAfter = fixture.plain(text);
        require(controlHash == plainRaster(*controlAfter),
                "original no-ruby raster unchanged after prototype and cache use");
        std::cout << "CONTROL raster_hash=" << controlHash << " unchanged\n";

        const std::string adjacent = "漢字仮名後";
        auto adjacentRuby = fixture.ruby(adjacent, {{{0, 6}, "かんじ"}, {{6, 12}, "ながいよみかた"}});
        require(adjacentRuby->shapedBase().runs().size() == 1, "two ruby pairs share one shaped run");
        auto width = std::max(adjacentRuby->units()[0].inlineExtent, adjacentRuby->units()[1].inlineExtent);
        checkLayout(*adjacentRuby, width);
        require(adjacentRuby->lines().size() >= 2, "adjacent pairs can wrap separately");
        raster("A-adjacent", *adjacentRuby);

        std::string extremeReading;
        for (int repeat = 0; repeat < 8; ++repeat) extremeReading += "ながいよみかた";
        auto extreme = fixture.ruby("漢後", {{{0, 3}, extremeReading}});
        checkLayout(*extreme, 120);
        require(extreme->units()[0].inlineExtent > 800, "stress annotation really wider than container");
        require(extreme->lines().size() == 2, "oversized pair progresses before ordinary suffix");
        auto baseBoxes = extreme->baseRects({0, 3});
        require(!baseBoxes.empty() && baseBoxes.front().left() > 120, "centering can put entire base outside clip");
        std::cout << "EXTREME container=120 pair=" << extreme->units()[0].inlineExtent
                  << " base_left=" << baseBoxes.front().left() << '\n';
        raster("A-extreme-full", *extreme);
        raster("A-extreme-clip120", *extreme, 120);
        auto baseVisible = fixture.ruby("漢後", {{{0, 3}, extremeReading, PrototypeOversizePlacement::BaseVisible}});
        checkLayout(*baseVisible, 120);
        auto visibleBoxes = baseVisible->baseRects({0, 3});
        require(visibleBoxes.front().left() >= 0 && visibleBoxes.front().right() <= 120,
                "base-visible places base inside positive fitting viewport");
        near(baseVisible->maxIntrinsic(), extreme->maxIntrinsic(), "placement does not change width allocation");
        near(baseVisible->height(), extreme->height(), "placement does not change line height");
        require(baseVisible->lines().size() == extreme->lines().size(), "one line breaker for both modes");
        std::cout << "BASE_VISIBLE container=120 base_left=" << visibleBoxes.front().left()
                  << " base_right=" << visibleBoxes.front().right() << '\n';
        raster("A-base-visible-clip120", *baseVisible, 120);
        extreme->layout(1000);
        baseVisible->layout(1000);
        require(extreme->placements().size() == baseVisible->placements().size(), "same visual glyph count");
        for (size_t index = 0; index < extreme->placements().size(); ++index) {
            require(extreme->placements()[index].origin == baseVisible->placements()[index].origin,
                    "fitting pair has identical placement in both modes");
        }
        checkLayout(*baseVisible, 0);

        const std::string combining = "a\xCC\x81"
                                      "X";
        auto rawControl = fixture.plain(combining, 1);
        auto& rawImpl = static_cast<ParagraphImpl&>(*rawControl);
        plainRaster(*rawControl, "B-combining-control");
        dump("B-combining", rawImpl);
        require(rawImpl.runs().size() >= 2, "combining input actually split into style runs");
        require(rawImpl.codeUnitHasProperty(1, SkUnicode::CodeUnitFlags::kGraphemeStart),
                "shape inserted run-start grapheme flag");
        fixture.fonts->getParagraphCache()->reset();
        int cacheHits = 0;
        fixture.fonts->getParagraphCache()->setChecker([&](ParagraphImpl*, const char* event, bool) {
            if (std::string(event) == "foundParagraph")
                ++cacheHits;
        });
        reject(fixture, combining, {1, 3}, 1, "raw-grapheme");
        auto cacheCount = fixture.fonts->getParagraphCache()->count();
        require(cacheCount > 0, "rejected ruby leaves a cached pure-base shape");
        reject(fixture, combining, {1, 3}, 1, "raw-grapheme");
        require(fixture.fonts->getParagraphCache()->count() == cacheCount,
                "repeat validation uses same cached base key");
        require(cacheHits == 1, "hot rejection actually hit the paragraph shape cache");
        fixture.fonts->getParagraphCache()->setChecker([](ParagraphImpl*, const char*, bool) {});
        std::cout << "CACHE raw-grapheme rejected on cold and actual hot hit=" << cacheHits << '\n';
        auto crossRun = fixture.ruby(combining, {{{0, 3}, "accent"}}, 1);
        require(!crossRun->rawBoundary(1), "raw boundaries distinct from shape flags");
        checkLayout(*crossRun, 30);
        raster("B-cross-style", *crossRun);
        auto plainCrossRun = fixture.ruby(combining, {}, 1);
        require(plainCrossRun->units()[0].text.end == 3,
                "ordinary grapheme remains atomic across style runs");
        checkLayout(*plainCrossRun, 0);

        const std::string fallback = "A😀漢B";
        auto fallbackRuby = fixture.ruby(fallback, {{{0, fallback.size()}, "fallback"}});
        dump("B-fallback", fallbackRuby->shapedBase());
        require(fallbackRuby->shapedBase().runs().size() >= 3, "real family fallback splits runs");
        auto utf16 = SkUnicode::convertUtf8ToUtf16(fallback.data(), fallback.size());
        require(utf16.size() == 5, "non-BMP UTF16 count excludes annotation");
        reject(fixture, fallback, {2, 5}, 0, "raw-grapheme");
        checkLayout(*fallbackRuby, 0);
        raster("B-fallback", *fallbackRuby);
        auto emojiBytes = fallbackRuby->baseRects({1, 5});
        auto emojiUtf16 = fallbackRuby->baseRectsUtf16({1, 3});
        require(!emojiBytes.empty() && emojiBytes == emojiUtf16,
                "UTF16 supplementary-character query matches UTF8 ink geometry");
        require(fallbackRuby->baseRectsUtf16({1, 2}).empty(),
                "partial surrogate query does not select the emoji");
        require(fallbackRuby->baseRectsUtf16({2, 3}).empty(),
                "trailing surrogate query does not select the emoji");

        auto multiline = fixture.ruby("漢\n字\n", {{{0, 3}, "かん"}, {{4, 7}, "じ"}});
        checkLayout(*multiline, 1000);
        require(multiline->lines().size() == 3 && multiline->lines()[0].hardBreak &&
                    multiline->lines()[1].hardBreak && multiline->lines()[2].start == multiline->units().size(),
                "hard breaks preserve a final empty line");
        auto firstLine = multiline->baseRectsUtf16({0, 1});
        auto secondLine = multiline->baseRectsUtf16({2, 3});
        require(!firstLine.empty() && !secondLine.empty() &&
                    secondLine.front().top() >= firstLine.front().bottom(),
                "UTF16 range geometry follows multiline placement");
        auto center = SkPoint::Make(firstLine.front().centerX(), firstLine.front().centerY());
        require(multiline->hitTestRangeUtf16({0, 1}, center) &&
                    !multiline->hitTestRangeUtf16({2, 3}, center),
                "UTF16 hit testing follows the queried line");
        require(multiline->maxIntrinsic() <
                    multiline->units()[0].inlineExtent + multiline->units()[2].inlineExtent,
                "maximum intrinsic width resets at hard breaks");
        raster("C-multiline", *multiline);
        checkLayout(*multiline, 0);
        require(multiline->lines().back().start == multiline->units().size(),
                "resize preserves the empty final line");
        reject(fixture, "漢\n字", {0, 7}, 0, "hard line break");
        auto consecutiveBreaks = fixture.ruby("A\n\nB", {});
        checkLayout(*consecutiveBreaks, 1000);
        require(consecutiveBreaks->lines().size() == 3 && consecutiveBreaks->lines()[0].hardBreak &&
                    consecutiveBreaks->lines()[1].hardBreak && !consecutiveBreaks->lines()[2].hardBreak,
                "consecutive hard breaks keep the middle line");
        require(consecutiveBreaks->lines()[1].blockEnd > consecutiveBreaks->lines()[0].blockEnd,
                "hard-break-only line has positive height");
        bool annotationBreakRejected = false;
        try {
            fixture.ruby("A", {{{0, 1}, "x\u2028y"}});
        } catch (const std::runtime_error& error) {
            annotationBreakRejected = std::string(error.what()).find("single-line") != std::string::npos;
        }
        require(annotationBreakRejected, "Unicode hard break is rejected inside annotation");

        auto emergency = fixture.ruby("ABCDE", {});
        checkLayout(*emergency, emergency->units()[0].inlineExtent * 1.5f);
        require(emergency->lines().size() > 1, "unbreakable word wraps at unit boundary");
        auto spaced = fixture.ruby("A B", {});
        checkLayout(*spaced, spaced->units()[0].inlineExtent + 0.1f);
        require(spaced->lines().size() == 2 && spaced->lines()[0].end == 2 &&
                    spaced->lines()[1].start == 2,
                "line break keeps trailing space with preceding line");
        near(spaced->lines()[0].inlineExtent, spaced->units()[0].inlineExtent,
             "trailing space does not inflate visible line width");

        auto bidi = fixture.ruby("A אב B", {{{2, 4}, "alef"}});
        checkLayout(*bidi, 1000);
        auto alef = bidi->baseRects({2, 4});
        auto bet = bidi->baseRects({4, 6});
        require(!alef.empty() && !bet.empty() && bet.front().left() < alef.front().left(),
                "mixed-direction line places Hebrew clusters in visual order");
        require(bidi->baseRectsUtf16({2, 3}) == alef,
                "UTF16 query follows reordered Hebrew base ink");
        raster("C-bidi", *bidi);
        auto mixedWidth = bidi->units()[0].inlineExtent + bidi->units()[1].inlineExtent +
                          bidi->units()[2].inlineExtent + bidi->units()[3].inlineExtent + 0.1f;
        checkLayout(*bidi, mixedWidth);
        alef = bidi->baseRects({2, 4});
        bet = bidi->baseRects({4, 6});
        require(bidi->lines().size() == 2 && !alef.empty() && !bet.empty() &&
                    bet.front().left() < alef.front().left(),
                "wrapped mixed-direction line preserves Hebrew visual order");
        raster("C-bidi-wrapped", *bidi);
        reject(fixture, "A אב B", {0, 4}, 0, "bidi levels");
        fixture.style.setTextDirection(TextDirection::kRtl);
        auto rtl = fixture.ruby("אב", {{{0, 2}, "alef"}});
        checkLayout(*rtl, 1000);
        auto rtlAlef = rtl->baseRects({0, 2});
        auto rtlBet = rtl->baseRects({2, 4});
        require(!rtlAlef.empty() && !rtlBet.empty() && rtlBet.front().left() < rtlAlef.front().left(),
                "RTL paragraph reorders annotated and plain clusters");
        auto rtlWithSpace = fixture.ruby("אב ", {{{0, 2}, "alef"}});
        checkLayout(*rtlWithSpace, 1000);
        auto rtlBetWithSpace = rtlWithSpace->baseRects({2, 4});
        require(!rtlBetWithSpace.empty(), "RTL base ink exists with trailing whitespace");
        near(rtlBetWithSpace.front().left(), rtlBet.front().left(),
             "RTL trailing space does not shift visible base ink");
        fixture.style.setTextDirection(TextDirection::kLtr);

        const std::string ligature = "office";
        auto ligatureControl = fixture.plain(ligature);
        auto& ligatureImpl = static_cast<ParagraphImpl&>(*ligatureControl);
        dump("B-ligature", ligatureImpl);
        bool actualLigature = false;
        for (auto& cluster: ligatureImpl.clusters()) {
            if (cluster.textRange().width() > 1 && cluster.size() < cluster.textRange().width()) {
                actualLigature = true;
                reject(fixture,
                       ligature,
                       {cluster.textRange().start + 1, cluster.textRange().end},
                       0,
                       "shaped-cluster");
                auto wholeLigature = fixture.ruby(ligature, {{cluster.textRange(), "ligature"}});
                checkLayout(*wholeLigature, 0);
                raster("B-whole-ligature", *wholeLigature);
            }
        }
        require(actualLigature, "font actually generated a multi-character ligature cluster");
        std::cout << "PASS checks=" << checks << '\n';
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL after " << checks << " checks: " << error.what() << '\n';
        return 1;
    }
}
