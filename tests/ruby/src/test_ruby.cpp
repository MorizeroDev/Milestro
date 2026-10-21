#include "include/core/SkBitmap.h"
#include "include/core/SkCanvas.h"
#include "include/core/SkPath.h"
#include "include/ports/SkFontMgr_empty.h"
#include "modules/skparagraph/include/TypefaceFontProvider.h"
#include "modules/skparagraph/src/ParagraphBuilderImpl.h"
#include "modules/skparagraph/src/ParagraphImpl.h"
#include "modules/skunicode/include/SkUnicode_icu.h"
#include <gtest/gtest.h>
#include <fstream>
#include <tuple>
#include <unicode/udata.h>

using namespace skia::textlayout;

namespace {

class RubyLayoutTest : public testing::Test {
protected:
    inline static std::vector<uint64_t> icuData;
    ParagraphStyle style;
    sk_sp<FontCollection> fonts;
    sk_sp<SkUnicode> unicode;

    static void SetUpTestSuite() {
        std::ifstream input(RUBY_TEST_ROOT "/ext/icu-cmake/common/icudtl.dat", std::ios::binary | std::ios::ate);
        ASSERT_TRUE(input.good());
        auto length = static_cast<size_t>(input.tellg());
        icuData.resize((length + 7) / 8);
        input.seekg(0);
        input.read(reinterpret_cast<char*>(icuData.data()), length);
        UErrorCode status = U_ZERO_ERROR;
        udata_setCommonData(icuData.data(), &status);
        ASSERT_TRUE(U_SUCCESS(status));
    }

    void SetUp() override {
        unicode = SkUnicodes::ICU::Make();
        ASSERT_NE(unicode, nullptr);
        fonts = sk_make_sp<FontCollection>();
        auto loader = SkFontMgr_New_Custom_Empty();
        auto provider = sk_make_sp<TypefaceFontProvider>();
        auto load = [&](const char* path, const char* family) {
            auto face = loader->makeFromFile(path);
            EXPECT_NE(face, nullptr) << path;
            if (face) provider->registerTypeface(face, SkString(family));
            return face;
        };
        load(RUBY_TEST_ROOT "/ext/skia/resources/fonts/Roboto-Regular.ttf", "Latin");
        load(RUBY_TEST_ROOT "/tests/data/font/SourceHanSans-VF.otf.woff2.bytes", "CJK");
        load(RUBY_TEST_ROOT "/tests/data/font/NotoColorEmoji.ttf.bytes", "Emoji");
        load(RUBY_TEST_ROOT "/tests/data/font/LiberationSans-Bold.ttf.bytes", "RTL");
        fonts->setDefaultFontManager(provider);
        TextStyle textStyle;
        textStyle.setFontFamilies({SkString("Latin"), SkString("CJK"), SkString("Emoji"), SkString("RTL")});
        textStyle.setFontSize(32);
        textStyle.setLocale(SkString("ja"));
        textStyle.setColor(SK_ColorBLACK);
        style.setTextStyle(textStyle);
        style.setApplyRoundingHack(false);
        style.setTextAlign(TextAlign::kLeft);
    }

    std::unique_ptr<Paragraph> build(const std::string& text, std::vector<RubyAnnotation> annotations = {},
                                   float width = 1000, size_t split = 0) {
        ParagraphBuilderImpl builder(style, fonts, unicode);
        if (split) {
            builder.addText(text.data(), split);
            auto changed = style.getTextStyle();
            changed.setFontSize(40);
            builder.pushStyle(changed);
            builder.addText(text.data() + split, text.size() - split);
        } else {
            builder.addText(text.data(), text.size());
        }
        for (const auto& annotation : annotations) builder.addRuby(annotation);
        auto paragraph = builder.Build();
        paragraph->layout(width);
        return paragraph;
    }

    SkRect ink(Paragraph& paragraph, bool annotation, TextRange textRange = EMPTY_TEXT) {
        SkRect result = SkRect::MakeEmpty();
        paragraph.visit([&](int, const Paragraph::VisitorInfo* info) {
            if (!info || bool(info->flags & Paragraph::kRubyAnnotation_VisitorFlag) != annotation) return;
            std::vector<SkRect> bounds(info->count);
            info->font.getBounds({info->glyphs, static_cast<size_t>(info->count)}, bounds, nullptr);
            for (int index = 0; index < info->count; ++index) {
                if (!textRange.empty() && (info->utf8Starts[index] < textRange.start ||
                                          info->utf8Starts[index] >= textRange.end)) continue;
                bounds[index].offset(info->origin + info->positions[index]);
                result.join(bounds[index]);
            }
        });
        return result;
    }

    uint64_t raster(Paragraph& paragraph, const std::string& name = {}) {
        SkBitmap bitmap;
        bitmap.allocN32Pixels(1024, 512);
        bitmap.eraseColor(SK_ColorWHITE);
        SkCanvas canvas(bitmap);
        paragraph.paint(&canvas, 8, 8);
        uint64_t hash = 1469598103934665603ULL;
        auto bytes = static_cast<const unsigned char*>(bitmap.getPixels());
        for (size_t index = 0; index < bitmap.computeByteSize(); ++index)
            hash = (hash ^ bytes[index]) * 1099511628211ULL;
        if (!name.empty()) {
            std::ofstream output(name + ".ppm", std::ios::binary);
            output << "P6\n1024 512\n255\n";
            for (int row = 0; row < bitmap.height(); ++row) {
                for (int column = 0; column < bitmap.width(); ++column) {
                    auto color = bitmap.getColor(column, row);
                    const unsigned char pixel[] = {static_cast<unsigned char>(SkColorGetR(color)),
                                                   static_cast<unsigned char>(SkColorGetG(color)),
                                                   static_cast<unsigned char>(SkColorGetB(color))};
                    output.write(reinterpret_cast<const char*>(pixel), 3);
                }
            }
        }
        return hash;
    }

    SkRect box(Paragraph& paragraph, unsigned first, unsigned last) {
        SkRect result = SkRect::MakeEmpty();
        for (const auto& entry : paragraph.getRectsForRange(first, last, RectHeightStyle::kTight, RectWidthStyle::kTight))
            result.join(entry.rect);
        return result;
    }
};

TEST_F(RubyLayoutTest, NativeLinesPaintingVisitorsAndQueriesAgree) {
    auto plain = build("漢字後");
    auto ruby = build("漢字後", {{{0, 6}, "かんじ"}});
    EXPECT_EQ(ruby->lineNumber(), 1u);
    EXPECT_GT(ruby->getHeight(), plain->getHeight());
    EXPECT_LT(ink(*ruby, true).bottom(), ink(*ruby, false).top());
    EXPECT_GE(ink(*ruby, true).top(), 0);
    EXPECT_LE(ink(*ruby, false).bottom(), ruby->getHeight());
    EXPECT_FALSE(box(*ruby, 0, 2).isEmpty());
    auto center = box(*ruby, 0, 2).centerX();
    auto position = ruby->getGlyphPositionAtCoordinate(center, box(*ruby, 0, 2).centerY());
    EXPECT_GE(position.position, 0);
    EXPECT_LE(position.position, 2);
    int visitorCount = 0;
    int extendedCount = 0;
    int lineEnds = 0;
    ruby->visit([&](int, const Paragraph::VisitorInfo* info) { if (info) visitorCount += info->count; });
    ruby->extendedVisit([&](int, const Paragraph::ExtendedVisitorInfo* info) {
        if (!info) { ++lineEnds; return; }
        extendedCount += info->count;
        for (int index = 0; index < info->count; ++index) EXPECT_LT(info->utf8Starts[index], 9u);
    });
    EXPECT_EQ(visitorCount, extendedCount);
    EXPECT_EQ(lineEnds, 1);
    SkPath path;
    EXPECT_EQ(ruby->getPath(0, &path), 0);
    EXPECT_LE(path.getBounds().top(), ink(*ruby, true).bottom());
    raster(*ruby, "native-ruby");
}

TEST_F(RubyLayoutTest, RubyStyleControlsAnnotationTextAndGap) {
    RubyStyle rubyStyle;
    rubyStyle.textStyle = style.getTextStyle();
    rubyStyle.textStyle->setFontSize(24);
    rubyStyle.fontSizeMultiplier = 1;
    rubyStyle.gap = 9;
    auto customized = build("漢", {{{0, 3}, "かん", rubyStyle}});
    auto defaultStyle = build("漢", {{{0, 3}, "かん"}});

    EXPECT_GT(ink(*customized, true).height(), ink(*defaultStyle, true).height());
    EXPECT_GT(ink(*customized, false).top() - ink(*customized, true).bottom(),
              ink(*defaultStyle, false).top() - ink(*defaultStyle, true).bottom() + 5);
}

TEST_F(RubyLayoutTest, RubyStyleCanPlaceAnnotationBelowBase) {
    RubyStyle rubyStyle;
    rubyStyle.position = RubyPosition::Below;
    auto paragraph = build("漢", {{{0, 3}, "かん", rubyStyle}});

    EXPECT_GT(ink(*paragraph, true).top(), ink(*paragraph, false).bottom());
    EXPECT_LE(ink(*paragraph, true).bottom(), paragraph->getHeight());
}

TEST_F(RubyLayoutTest, RubyAlignmentPositionsShortAnnotationWithinBase) {
    RubyStyle startStyle;
    startStyle.alignment = RubyAlignment::Start;
    RubyStyle endStyle;
    endStyle.alignment = RubyAlignment::End;
    auto start = build("漢字", {{{0, 6}, "a", startStyle}});
    auto centered = build("漢字", {{{0, 6}, "a"}});
    auto end = build("漢字", {{{0, 6}, "a", endStyle}});

    EXPECT_LT(ink(*start, true).left(), ink(*centered, true).left());
    EXPECT_LT(ink(*centered, true).left(), ink(*end, true).left());
}

TEST_F(RubyLayoutTest, RubyAlignmentDistributesBaseGlyphsForLongAnnotation) {
    RubyStyle distributedStyle;
    distributedStyle.alignment = RubyAlignment::SpaceBetween;
    auto centered = build("漢字", {{{0, 6}, "とてもながいよみかた"}});
    auto distributed = build("漢字", {{{0, 6}, "とてもながいよみかた", distributedStyle}});
    auto centeredDistance = ink(*centered, false, {3, 6}).left() - ink(*centered, false, {0, 3}).left();
    auto distributedDistance = ink(*distributed, false, {3, 6}).left() - ink(*distributed, false, {0, 3}).left();

    EXPECT_GT(distributedDistance, centeredDistance);
    EXPECT_NEAR(distributed->getMaxIntrinsicWidth(), centered->getMaxIntrinsicWidth(), 0.02);
}

TEST_F(RubyLayoutTest, RubyAlignmentDistributesShortAnnotationAcrossBase) {
    RubyStyle distributedStyle;
    distributedStyle.alignment = RubyAlignment::SpaceBetween;
    auto centered = build("漢字仮名", {{{0, 12}, "かな"}});
    auto distributed = build("漢字仮名", {{{0, 12}, "かな", distributedStyle}});

    EXPECT_GT(ink(*distributed, true).width(), ink(*centered, true).width() + 20);
    EXPECT_NEAR(ink(*distributed, true).centerX(), ink(*distributed, false).centerX(), 2);
}

TEST_F(RubyLayoutTest, RubyOverhangKeepsAnnotationSizeWithoutExpandingBaseCell) {
    RubyStyle overhangStyle;
    overhangStyle.overhang = RubyOverhang::Allow;
    auto contained = build("漢後", {{{0, 3}, "とてもながいよみかた"}});
    auto overhanging = build("漢後", {{{0, 3}, "とてもながいよみかた", overhangStyle}});

    EXPECT_LT(overhanging->getMaxIntrinsicWidth(), contained->getMaxIntrinsicWidth());
    EXPECT_NEAR(ink(*overhanging, true).width(), ink(*contained, true).width(), 0.02);
}

TEST_F(RubyLayoutTest, RubyPairsStayAtomicAndAdjacentPairsWrapSeparately) {
    auto paragraph = build("漢字仮名後", {{{0, 6}, "かんじ"}, {{6, 12}, "ながいよみかた"}}, 80);
    EXPECT_GE(paragraph->lineNumber(), 3u);
    EXPECT_EQ(paragraph->getLineNumberAt(0), paragraph->getLineNumberAt(3));
    EXPECT_EQ(paragraph->getLineNumberAt(6), paragraph->getLineNumberAt(9));
    EXPECT_NE(paragraph->getLineNumberAt(0), paragraph->getLineNumberAt(6));
    raster(*paragraph, "native-ruby-wrapped");
}

TEST_F(RubyLayoutTest, EmergencyWrappingPreservesPunctuationAndNonbreakingSpaces) {
    auto opening = build("「漢字」後", {{{3, 9}, "かんじ"}}, 1);
    EXPECT_EQ(opening->getLineNumberAt(0), opening->getLineNumberAt(3));
    EXPECT_EQ(opening->getLineNumberAt(3), opening->getLineNumberAt(9));
    auto nbsp = build("A\xC2\xA0" "B漢", {{{4, 7}, "かん"}}, 1);
    EXPECT_EQ(nbsp->getLineNumberAt(0), nbsp->getLineNumberAt(3));
    auto joiner = build("A\xE2\x81\xA0" "B漢", {{{5, 8}, "かん"}}, 1);
    EXPECT_EQ(joiner->getLineNumberAt(0), joiner->getLineNumberAt(4));
    auto word = build("ABCDE漢", {{{5, 8}, "かん"}}, 25);
    EXPECT_GT(word->lineNumber(), 2u);
}

TEST_F(RubyLayoutTest, HardBreaksKeepEmptyLinesAndRejectCrossingAnnotations) {
    auto paragraph = build("漢\n\n字\n", {{{0, 3}, "かん"}, {{5, 8}, "じ"}}, 0);
    EXPECT_EQ(paragraph->lineNumber(), 4u);
    EXPECT_GT(paragraph->getHeight(), 0);
    EXPECT_THROW(build("漢\n字", {{{0, 7}, "reading"}}), std::invalid_argument);
    EXPECT_THROW(build("漢", {{{0, 3}, "x\xE2\x80\xA8y"}}), std::invalid_argument);
}

TEST_F(RubyLayoutTest, OversizedRubyPreservesBaseAndDoesNotAccumulateOnResize) {
    std::string reading;
    for (int repeat = 0; repeat < 8; ++repeat) reading += "ながいよみかた";
    auto paragraph = build("漢", {{{0, 3}, reading}}, 120);
    auto bounds = ink(*paragraph, false);
    EXPECT_GE(bounds.left(), 0);
    EXPECT_LE(bounds.right(), 120);
    EXPECT_GT(paragraph->getMaxIntrinsicWidth(), 800);
    auto hash = raster(*paragraph, "native-ruby-overflow");
    paragraph->layout(0);
    paragraph->layout(1000);
    paragraph->layout(120);
    EXPECT_EQ(hash, raster(*paragraph));
    auto inlineParagraph = build("漢", {{{0, 3}, reading, RubyOverflow::Inline}}, 120);
    EXPECT_GT(ink(*inlineParagraph, false).left(), 120);
    EXPECT_EQ(paragraph->getMaxIntrinsicWidth(), inlineParagraph->getMaxIntrinsicWidth());
}

TEST_F(RubyLayoutTest, ShapingCacheCannotLeakRubyPaddingIntoOtherParents) {
    auto plain = build("漢字後");
    auto plainHash = raster(*plain);
    auto first = build("漢字後", {{{0, 6}, "とてもながいよみかた"}}, 100);
    auto firstHash = raster(*first);
    auto second = build("漢字後", {{{0, 6}, "かんじ"}}, 300);
    EXPECT_NE(first->getMaxIntrinsicWidth(), second->getMaxIntrinsicWidth());
    EXPECT_EQ(plainHash, raster(*build("漢字後")));
    EXPECT_EQ(firstHash, raster(*first));
    first->markDirty();
    first->layout(100);
    EXPECT_EQ(firstHash, raster(*first));
}

TEST_F(RubyLayoutTest, RawGraphemesSurrogatesAndShapedLigaturesRemainIntact) {
    EXPECT_THROW(build("a\xCC\x81X", {{{1, 3}, "read"}}, 30, 1), std::invalid_argument);
    auto crossStyle = build("a\xCC\x81X", {{{0, 3}, "accent"}}, 0, 1);
    EXPECT_EQ(crossStyle->getLineNumberAt(0), crossStyle->getLineNumberAt(1));
    auto emoji = build("A😀漢B", {{{1, 5}, "smile"}}, 0);
    EXPECT_TRUE(box(*emoji, 1, 2).isEmpty());
    EXPECT_FALSE(box(*emoji, 1, 3).isEmpty());
    EXPECT_THROW(build("漢字", {{{1, 6}, "read"}}), std::invalid_argument);
    auto ligature = build("office");
    auto& native = static_cast<ParagraphImpl&>(*ligature);
    bool found = false;
    for (const auto& cluster : native.clusters()) {
        if (cluster.textRange().width() > 1 && cluster.size() < cluster.textRange().width()) {
            found = true;
            EXPECT_THROW(build("office", {{{cluster.textRange().start + 1, cluster.textRange().end}, "read"}}),
                         std::invalid_argument);
        }
    }
    EXPECT_TRUE(found);
}

TEST_F(RubyLayoutTest, MixedBidiAndWrappedBidiKeepRubyWithTheirBase) {
    auto paragraph = build("A אב B", {{{2, 4}, "alef"}});
    ASSERT_EQ(paragraph->unresolvedGlyphs(), 0);
    EXPECT_LT(box(*paragraph, 3, 4).left(), box(*paragraph, 2, 3).left());
    EXPECT_NEAR(ink(*paragraph, true).centerX(), box(*paragraph, 2, 3).centerX(), 6);
    raster(*paragraph, "native-ruby-bidi");
    paragraph->layout(75);
    EXPECT_GT(paragraph->lineNumber(), 1u);
    EXPECT_EQ(paragraph->getLineNumberAt(2), paragraph->getLineNumberAt(4));
    EXPECT_LT(box(*paragraph, 3, 4).left(), box(*paragraph, 2, 3).left());
    EXPECT_THROW(build("A אב B", {{{0, 4}, "mixed"}}), std::invalid_argument);
}

TEST_F(RubyLayoutTest, RtlParagraphAndRtlAnnotationHaveNativeVisualOrder) {
    style.setTextDirection(TextDirection::kRtl);
    style.setTextAlign(TextAlign::kStart);
    auto paragraph = build("אב", {{{0, 2}, "אב"}}, 200);
    ASSERT_EQ(paragraph->unresolvedGlyphs(), 0);
    EXPECT_LT(box(*paragraph, 1, 2).left(), box(*paragraph, 0, 1).left());
    EXPECT_GT(box(*paragraph, 0, 1).left(), 100);
    auto trailing = build("אב ", {{{0, 2}, "אב"}}, 200);
    EXPECT_NEAR(box(*paragraph, 1, 2).left(), box(*trailing, 1, 2).left(), 0.02);
    auto expanded = build("אב", {{{0, 4}, "a longer annotation"}}, 250);
    EXPECT_NEAR(ink(*expanded, true).centerX(), ink(*expanded, false).centerX(), 6);
    EXPECT_LT(ink(*expanded, false, {2, 4}).left(), ink(*expanded, false, {0, 2}).left());
    EXPECT_LT(ink(*expanded, true).bottom(), ink(*expanded, false).top());
    raster(*paragraph, "native-ruby-rtl");
}

TEST_F(RubyLayoutTest, RubyAlignmentStartAndEndFollowBidiDirection) {
    style.setTextDirection(TextDirection::kRtl);
    style.setTextAlign(TextAlign::kStart);
    RubyStyle startStyle;
    startStyle.alignment = RubyAlignment::Start;
    RubyStyle endStyle;
    endStyle.alignment = RubyAlignment::End;
    auto start = build("אב", {{{0, 4}, "a", startStyle}});
    auto end = build("אב", {{{0, 4}, "a", endStyle}});

    EXPECT_GT(ink(*start, true).left(), ink(*end, true).left());
}

TEST_F(RubyLayoutTest, BidiIsolatesOverridesNumbersAndRtlWrappingKeepNativeOrder) {
    auto isolated = build("A \u2067אב 12\u2069 B", {{{5, 9}, "אב"}});
    EXPECT_EQ(isolated->unresolvedGlyphs(), 0);
    EXPECT_LT(box(*isolated, 4, 5).left(), box(*isolated, 3, 4).left());
    EXPECT_LT(box(*isolated, 6, 7).left(), box(*isolated, 7, 8).left());
    EXPECT_LT(box(*isolated, 7, 8).right(), box(*isolated, 4, 5).left());
    EXPECT_NEAR(ink(*isolated, true).centerX(), box(*isolated, 3, 5).centerX(), 6);
    isolated->layout(75);
    EXPECT_GT(isolated->lineNumber(), 1u);
    EXPECT_EQ(isolated->getLineNumberAt(5), isolated->getLineNumberAt(7));
    EXPECT_THROW(build("A \u2067אב\u2069 B", {{{2, 12}, "isolate"}}), std::invalid_argument);
    auto overridden = build("A \u202eBC\u202c D", {{{5, 7}, "read"}});
    EXPECT_LT(box(*overridden, 4, 5).left(), box(*overridden, 3, 4).left());

    style.setTextDirection(TextDirection::kRtl);
    style.setTextAlign(TextAlign::kStart);
    auto wrapped = build("אב אב אב", {{{0, 4}, "first"}, {{5, 9}, "second"}, {{10, 14}, "third"}}, 65);
    EXPECT_GE(wrapped->lineNumber(), 3u);
    for (unsigned start : {0u, 3u, 6u}) {
        EXPECT_LT(box(*wrapped, start + 1, start + 2).left(), box(*wrapped, start, start + 1).left());
        EXPECT_EQ(wrapped->getLineNumberAtUTF16Offset(start), wrapped->getLineNumberAtUTF16Offset(start + 1));
    }
    EXPECT_THROW(build("אב 12", {{{0, 7}, "mixed"}}), std::invalid_argument);
    raster(*wrapped, "native-ruby-rtl-wrapped");
}

TEST_F(RubyLayoutTest, EllipsisRemovesWholeRubyGroupsAndCanStandAlone) {
    style.setMaxLines(1);
    style.setEllipsis(SkString("…"));
    auto paragraph = build("A漢字後後", {{{1, 7}, "かんじ"}}, 110);
    auto visible = paragraph->getActualTextRange(0, false);
    EXPECT_TRUE(visible.end <= 1 || visible.end >= 7);
    EXPECT_EQ(paragraph->lineNumber(), 1u);
    EXPECT_TRUE(paragraph->didExceedMaxLines());
    if (visible.end <= 1) {
        EXPECT_TRUE(ink(*paragraph, true).isEmpty());
        EXPECT_TRUE(box(*paragraph, 1, 3).isEmpty());
    }
    auto alone = build("漢字後", {{{0, 6}, "とてもながいよみかた"}}, 40);
    EXPECT_EQ(alone->getActualTextRange(0, false).width(), 0u);
    EXPECT_TRUE(ink(*alone, true).isEmpty());
    EXPECT_FALSE(ink(*alone, false).isEmpty());
    EXPECT_TRUE(alone->didExceedMaxLines());
    raster(*paragraph, "native-ruby-ellipsis");
    style.setTextDirection(TextDirection::kRtl);
    style.setTextAlign(TextAlign::kStart);
    auto rtl = build("אב אב אב", {{{0, 4}, "a very long reading"}}, 60);
    EXPECT_EQ(rtl->getActualTextRange(0, false).width(), 0u);
    EXPECT_TRUE(ink(*rtl, true).isEmpty());
    EXPECT_FALSE(ink(*rtl, false).isEmpty());
}

TEST_F(RubyLayoutTest, JustificationKeepsRubyBaseAndAnnotationTogether) {
    auto reference = build("前漢字後後後後", {{{3, 9}, "かんじ"}}, 160);
    auto referenceDistance = ink(*reference, false, {6, 9}).left() - ink(*reference, false, {3, 6}).left();
    style.setTextAlign(TextAlign::kJustify);
    auto justified = build("前漢字後後後後", {{{3, 9}, "かんじ"}}, 160);
    EXPECT_GT(justified->lineNumber(), 1u);
    EXPECT_NEAR(ink(*justified, false, {6, 9}).left() - ink(*justified, false, {3, 6}).left(), referenceDistance, 0.02);
    EXPECT_NEAR(ink(*justified, true).centerX(), ink(*justified, false, {3, 9}).centerX(), 6);
    auto firstHash = raster(*justified, "native-ruby-justified");
    justified->layout(120);
    justified->layout(160);
    EXPECT_EQ(firstHash, raster(*justified));
}

TEST_F(RubyLayoutTest, AnnotatedWhitespaceHeightBehaviorAndLineLimitsRemainConsistent) {
    auto paragraph = build("漢 \r\n字", {{{0, 4}, "かん"}}, 200);
    EXPECT_EQ(paragraph->lineNumber(), 2u);
    EXPECT_EQ(paragraph->getActualTextRange(0, false).end, 4u);
    EXPECT_EQ(paragraph->getActualTextRange(0, true).end, 4u);
    EXPECT_FALSE(ink(*paragraph, true).isEmpty());
    style.setTextHeightBehavior(TextHeightBehavior::kDisableAll);
    auto compact = build("漢\n字", {{{0, 3}, "かん"}}, 100);
    EXPECT_GE(ink(*compact, true).top(), 0);
    EXPECT_LE(ink(*compact, false).bottom(), compact->getHeight());
    style.setMaxLines(0);
    auto hidden = build("漢字", {{{0, 3}, "かん"}}, 100);
    EXPECT_EQ(hidden->lineNumber(), 0u);
    EXPECT_EQ(hidden->getHeight(), 0);
    EXPECT_TRUE(hidden->didExceedMaxLines());
}

TEST_F(RubyLayoutTest, ExtendedVisitorMatchesPaintPositionsForOffsetAnnotations) {
    auto paragraph = build("漢 אב", {{{0, 3}, "a\u0301"}, {{4, 8}, "א\u05b7ב"}});
    using GlyphPosition = std::tuple<SkGlyphID, float, float, unsigned>;
    std::vector<GlyphPosition> painted;
    std::vector<GlyphPosition> extended;
    paragraph->visit([&](int, const Paragraph::VisitorInfo* info) {
        if (!info) return;
        for (int index = 0; index < info->count; ++index) {
            auto position = info->origin + info->positions[index];
            painted.emplace_back(info->glyphs[index], position.x(), position.y(), info->flags);
        }
    });
    paragraph->extendedVisit([&](int, const Paragraph::ExtendedVisitorInfo* info) {
        if (!info) return;
        for (int index = 0; index < info->count; ++index) {
            auto position = info->origin + info->positions[index];
            extended.emplace_back(info->glyphs[index], position.x(), position.y(), info->flags);
        }
    });
    ASSERT_EQ(painted.size(), extended.size());
    for (size_t index = 0; index < painted.size(); ++index) {
        EXPECT_EQ(std::get<0>(painted[index]), std::get<0>(extended[index]));
        EXPECT_NEAR(std::get<1>(painted[index]), std::get<1>(extended[index]), 0.02);
        EXPECT_NEAR(std::get<2>(painted[index]), std::get<2>(extended[index]), 0.02);
        EXPECT_EQ(std::get<3>(painted[index]), std::get<3>(extended[index]));
    }
}

TEST_F(RubyLayoutTest, PlaceholdersRetainNativeMetricsAlongsideRuby) {
    ParagraphBuilderImpl builder(style, fonts, unicode);
    builder.addPlaceholder(PlaceholderStyle(20, 70, PlaceholderAlignment::kAboveBaseline,
                                           TextBaseline::kAlphabetic, 0));
    builder.addText("漢字");
    auto plain = builder.Build();
    plain->layout(200);
    builder.addRuby({{3, 9}, "かんじ"});
    auto paragraph = builder.Build();
    paragraph->layout(200);
    auto placeholders = paragraph->getRectsForPlaceholders();
    ASSERT_EQ(placeholders.size(), 1u);
    EXPECT_NEAR(placeholders[0].rect.width(), 20, 0.02);
    EXPECT_NEAR(placeholders[0].rect.height(), 70, 0.02);
    EXPECT_NEAR(placeholders[0].rect.top(), plain->getRectsForPlaceholders()[0].rect.top(), 0.02);
    EXPECT_LE(placeholders[0].rect.bottom(), paragraph->getHeight());
    EXPECT_LT(placeholders[0].rect.right(), ink(*paragraph, false).left());
    EXPECT_GE(ink(*paragraph, true).top(), 0);
    paragraph->layout(70);
    EXPECT_GT(paragraph->lineNumber(), 1u);
    EXPECT_NEAR(paragraph->getRectsForPlaceholders()[0].rect.height(), 70, 0.02);

    ParagraphBuilderImpl invalid(style, fonts, unicode);
    invalid.addPlaceholder(PlaceholderStyle(20, 20, PlaceholderAlignment::kAboveBaseline,
                                           TextBaseline::kAlphabetic, 0));
    invalid.addText("漢");
    invalid.addRuby({{0, 6}, "invalid"});
    auto rejected = invalid.Build();
    EXPECT_THROW(rejected->layout(200), std::invalid_argument);
}

TEST_F(RubyLayoutTest, ReformattingRestoresOversizePlacementAndIndependentParents) {
    auto paragraph = build("漢\n字", {{{0, 3}, "とてもながいよみかた"}}, 70);
    auto initialHash = raster(*paragraph);
    auto firstBase = ink(*paragraph, false, {0, 3});
    EXPECT_GE(firstBase.left(), 0);
    EXPECT_LE(firstBase.right(), 70);
    paragraph->updateTextAlign(TextAlign::kCenter);
    paragraph->layout(70);
    auto referenceStyle = style;
    style.setTextAlign(TextAlign::kCenter);
    auto reference = build("漢\n字", {{{0, 3}, "とてもながいよみかた"}}, 70);
    EXPECT_EQ(raster(*paragraph), raster(*reference));
    style = referenceStyle;
    paragraph->updateTextAlign(TextAlign::kLeft);
    paragraph->layout(70);
    EXPECT_EQ(raster(*paragraph), initialHash);
}

TEST_F(RubyLayoutTest, InputOrderingOverlapsAndEmptyAnnotationsAreValidated) {
    auto unsorted = build("漢字", {{{3, 6}, "じ"}, {{0, 3}, "かん"}}, 35);
    EXPECT_EQ(unsorted->lineNumber(), 2u);
    EXPECT_THROW(build("漢字", {{{0, 6}, "first"}, {{3, 6}, "overlap"}}), std::invalid_argument);
    EXPECT_THROW(build("漢字", {{{0, 7}, "outside"}}), std::invalid_argument);
    EXPECT_THROW(build("漢字", {{{0, 3}, ""}}), std::invalid_argument);
    EXPECT_THROW(build("漢字", {{{0, 3}, "a\tb"}}), std::invalid_argument);
    RubyStyle invalidScale;
    invalidScale.fontSizeMultiplier = 0;
    EXPECT_THROW(build("漢", {{{0, 3}, "かん", invalidScale}}), std::invalid_argument);
    RubyStyle invalidGap;
    invalidGap.gap = -1;
    EXPECT_THROW(build("漢", {{{0, 3}, "かん", invalidGap}}), std::invalid_argument);
}

TEST_F(RubyLayoutTest, BuilderResetDiscardsAnnotations) {
    ParagraphBuilderImpl builder(style, fonts, unicode);
    builder.addText("漢");
    builder.addRuby({{0, 3}, "かん"});
    builder.Reset();
    builder.addText("A");
    auto paragraph = builder.Build();
    paragraph->layout(100);
    EXPECT_FALSE(static_cast<ParagraphImpl&>(*paragraph).hasRuby());
}

TEST_F(RubyLayoutTest, TextIndentHandlesSingleRunMeasurements) {
    auto control = build("word");
    style.setTextIndent(20, false);
    auto paragraph = build("word");
    auto& impl = static_cast<ParagraphImpl&>(*paragraph);
    ASSERT_EQ(impl.lines().size(), 1u);

    EXPECT_FLOAT_EQ(impl.lines()[0].offset().fX, 20);
    EXPECT_FLOAT_EQ(paragraph->getLongestLine(), control->getLongestLine() + 20);
    EXPECT_FLOAT_EQ(paragraph->getMinIntrinsicWidth(), control->getMinIntrinsicWidth() + 20);
    EXPECT_FLOAT_EQ(paragraph->getMaxIntrinsicWidth(), control->getMaxIntrinsicWidth() + 20);

    style.setTextIndent(-5, false);
    auto negative = build("word");
    auto& negativeLine = static_cast<ParagraphImpl&>(*negative).lines()[0];
    EXPECT_FLOAT_EQ(negativeLine.offset().fX, -5);
    EXPECT_FLOAT_EQ(negative->getMinIntrinsicWidth(), control->getMinIntrinsicWidth() - 5);
    EXPECT_FLOAT_EQ(negative->getMaxIntrinsicWidth(), control->getMaxIntrinsicWidth() - 5);
}

TEST_F(RubyLayoutTest, TextIndentWrapsAndHitTestsOnlyTheFirstLine) {
    style.setTextIndent(24, false);
    auto paragraph = build("one two three four", {}, 110);
    auto& impl = static_cast<ParagraphImpl&>(*paragraph);
    ASSERT_GT(impl.lines().size(), 1u);

    EXPECT_FLOAT_EQ(impl.lines()[0].offset().fX, 24);
    EXPECT_FLOAT_EQ(impl.lines()[1].offset().fX, 0);
    EXPECT_LE(impl.lines()[0].width(), 86);
    auto firstBoxes = paragraph->getRectsForRange(0, 3, RectHeightStyle::kTight, RectWidthStyle::kMax);
    ASSERT_FALSE(firstBoxes.empty());
    EXPECT_NEAR(firstBoxes.front().rect.fLeft, 24, 0.01);

    LineMetrics secondLine;
    ASSERT_TRUE(paragraph->getLineMetricsAt(1, &secondLine));
    auto secondBoxes = paragraph->getRectsForRange(secondLine.fStartIndex,
                                                   secondLine.fEndExcludingWhitespaces,
                                                   RectHeightStyle::kTight,
                                                   RectWidthStyle::kMax);
    ASSERT_FALSE(secondBoxes.empty());
    EXPECT_NEAR(secondBoxes.front().rect.fLeft, 0, 0.01);
}

TEST_F(RubyLayoutTest, TextIndentResolvesPercentagesAgainstLayoutWidth) {
    style.setTextIndent(10, true);
    auto paragraph = build("one two", {}, 200);
    auto& impl = static_cast<ParagraphImpl&>(*paragraph);
    ASSERT_EQ(impl.lines().size(), 1u);
    EXPECT_FLOAT_EQ(impl.lines()[0].offset().fX, 20);

    paragraph->layout(300);
    EXPECT_FLOAT_EQ(impl.lines()[0].offset().fX, 30);
}

TEST_F(RubyLayoutTest, TextIndentUsesInlineStartForAlignmentAndDirection) {
    style.setTextAlign(TextAlign::kRight);
    style.setTextIndent(20, false);
    auto ltr = build("one two", {}, 120);
    auto& ltrLine = static_cast<ParagraphImpl&>(*ltr).lines()[0];
    EXPECT_NEAR(ltrLine.offset().fX + ltrLine.width(), 120, 0.01);

    style.setTextDirection(TextDirection::kRtl);
    auto rtl = build("one two", {}, 120);
    auto& rtlLine = static_cast<ParagraphImpl&>(*rtl).lines()[0];
    EXPECT_NEAR(rtlLine.offset().fX + rtlLine.width(), 100, 0.01);
}

TEST_F(RubyLayoutTest, TextIndentConstrainsJustificationAndEllipsis) {
    style.setTextAlign(TextAlign::kJustify);
    style.setTextIndent(20, false);
    auto justified = build("one two three four", {}, 180);
    auto& justifiedLine = static_cast<ParagraphImpl&>(*justified).lines()[0];
    EXPECT_FLOAT_EQ(justifiedLine.offset().fX, 20);
    EXPECT_NEAR(justifiedLine.width(), 160, 0.01);

    style.setTextAlign(TextAlign::kLeft);
    style.setMaxLines(1);
    style.setEllipsis(SkString("..."));
    auto ellipsized = build("one two three four", {}, 180);
    auto& ellipsizedLine = static_cast<ParagraphImpl&>(*ellipsized).lines()[0];
    EXPECT_FLOAT_EQ(ellipsizedLine.offset().fX, 20);
    EXPECT_LE(ellipsizedLine.width(), 160);
}

TEST_F(RubyLayoutTest, TextIndentAffectsOnlyFirstIntrinsicSegmentsAndHardLines) {
    auto firstMaxControl = build("longword x\na");
    auto laterMaxControl = build("x\nlongword");
    auto firstMinControl = build("longword x");
    auto laterMinControl = build("x longword");
    style.setTextIndent(20, false);
    auto firstMax = build("longword x\na");
    auto laterMax = build("x\nlongword");
    auto firstMin = build("longword x");
    auto laterMin = build("x longword");

    EXPECT_FLOAT_EQ(firstMax->getMaxIntrinsicWidth(), firstMaxControl->getMaxIntrinsicWidth() + 20);
    EXPECT_FLOAT_EQ(laterMax->getMaxIntrinsicWidth(), laterMaxControl->getMaxIntrinsicWidth());
    EXPECT_FLOAT_EQ(firstMin->getMinIntrinsicWidth(), firstMinControl->getMinIntrinsicWidth() + 20);
    EXPECT_FLOAT_EQ(laterMin->getMinIntrinsicWidth(), laterMinControl->getMinIntrinsicWidth());
    EXPECT_FLOAT_EQ(firstMax->getLongestLine(), firstMaxControl->getLongestLine() + 20);
    EXPECT_FLOAT_EQ(laterMax->getLongestLine(), laterMaxControl->getLongestLine());
}

TEST_F(RubyLayoutTest, TextIndentAppliesToRubyFirstLine) {
    std::vector<RubyAnnotation> annotations = {{{0, 4}, "note"}};
    auto control = build("ruby base", annotations, 90);
    style.setTextIndent(20, false);
    auto paragraph = build("ruby base", annotations, 90);
    auto& impl = static_cast<ParagraphImpl&>(*paragraph);
    ASSERT_GT(impl.lines().size(), 1u);

    EXPECT_FLOAT_EQ(impl.lines()[0].offset().fX, 20);
    EXPECT_FLOAT_EQ(impl.lines()[1].offset().fX, 0);
    EXPECT_LE(impl.lines()[0].width(), 70);
    EXPECT_FLOAT_EQ(paragraph->getMaxIntrinsicWidth(), control->getMaxIntrinsicWidth() + 20);
}

TEST_F(RubyLayoutTest, SoftHyphenBreakReservesHyphenWidthWithTextIndent) {
    const auto width = build("xy abcd")->getMaxIntrinsicWidth() +
                       build("-")->getMaxIntrinsicWidth() / 2;
    style.setRenderSoftHyphens(true);
    style.setTextIndent(24, false);
    auto paragraph = build("xy abcd\xC2\xAD" "efgh", {}, width + 24);
    auto lines = static_cast<ParagraphImpl&>(*paragraph).lines();
    ASSERT_GE(lines.size(), 2u);
    EXPECT_EQ(lines[0].trimmedText().end, 2u);
    EXPECT_EQ(lines[0].hyphen(), nullptr);
    EXPECT_FLOAT_EQ(lines[0].offset().fX, 24);
    EXPECT_FLOAT_EQ(lines[1].offset().fX, 0);
    EXPECT_LE(paragraph->getLongestLine(), width + 24);
}

TEST_F(RubyLayoutTest, SoftHyphenSelectionAndHitTestingUseOriginalSourceRange) {
    const auto width = build("inter-")->getMaxIntrinsicWidth() + 0.5f;
    style.setRenderSoftHyphens(true);
    for (auto direction : {TextDirection::kLtr, TextDirection::kRtl}) {
        style.setTextDirection(direction);
        auto paragraph = build("inter\xC2\xAD" "national", {}, width);
        auto lines = static_cast<ParagraphImpl&>(*paragraph).lines();
        ASSERT_GE(lines.size(), 2u);
        ASSERT_NE(lines[0].hyphen(), nullptr);
        const auto rects = paragraph->getRectsForRange(5, 6, RectHeightStyle::kTight,
                                                      RectWidthStyle::kTight);
        ASSERT_EQ(rects.size(), 1u);
        EXPECT_NEAR(rects[0].rect.width(), lines[0].hyphen()->advance().fX, 0.01f);
        const auto position = paragraph->getGlyphPositionAtCoordinate(
                rects[0].rect.centerX(), rects[0].rect.centerY());
        EXPECT_GE(position.position, 5);
        EXPECT_LE(position.position, 6);
    }
}

TEST_F(RubyLayoutTest, HeightOverrideAppliesBaselineShiftOnce) {
    for (bool halfLeading : {false, true}) {
        auto textStyle = style.getTextStyle();
        textStyle.setHeightOverride(true);
        textStyle.setHeight(2);
        textStyle.setHalfLeading(halfLeading);
        textStyle.setBaselineShift(0);
        style.setTextStyle(textStyle);
        auto control = build("A");
        textStyle.setBaselineShift(-12);
        style.setTextStyle(textStyle);
        auto shifted = build("A");
        const auto& originalRun = static_cast<ParagraphImpl&>(*control).runs()[0];
        const auto& shiftedRun = static_cast<ParagraphImpl&>(*shifted).runs()[0];
        EXPECT_NEAR(shiftedRun.correctAscent(), originalRun.correctAscent() - 12, 0.01f);
        EXPECT_NEAR(shiftedRun.correctDescent(), originalRun.correctDescent() - 12, 0.01f);
    }
}

TEST_F(RubyLayoutTest, CachedHalfLeadingMatchesFreshLayoutForBaseAndRuby) {
    for (const auto& annotations : {std::vector<RubyAnnotation>{},
                                    std::vector<RubyAnnotation>{{{0, 4}, "annotation"}}}) {
        auto textStyle = style.getTextStyle();
        textStyle.setHeightOverride(true);
        textStyle.setHeight(2);
        textStyle.setHalfLeading(false);
        style.setTextStyle(textStyle);
        auto scaled = build("base", annotations);
        textStyle.setHalfLeading(true);
        style.setTextStyle(textStyle);
        auto cached = build("base", annotations);
        fonts->clearCaches();
        auto fresh = build("base", annotations);
        EXPECT_FLOAT_EQ(cached->getAlphabeticBaseline(), fresh->getAlphabeticBaseline());
        EXPECT_EQ(raster(*cached), raster(*fresh));
        EXPECT_NE(scaled->getAlphabeticBaseline(), fresh->getAlphabeticBaseline());
    }
}

TEST_F(RubyLayoutTest, CacheRestoresMissingGlyphAndResolvedFontDiagnostics) {
    auto textStyle = style.getTextStyle();
    textStyle.setFontFamilies({SkString("Latin")});
    style.setTextStyle(textStyle);
    const std::string text = "base \xE2\xBF\xB0";
    auto shaped = build(text);
    auto cached = build(text);
    ASSERT_GT(shaped->unresolvedGlyphs(), 0);
    EXPECT_EQ(cached->unresolvedGlyphs(), shaped->unresolvedGlyphs());
    EXPECT_EQ(cached->unresolvedCodepoints(), shaped->unresolvedCodepoints());
    EXPECT_EQ(static_cast<ParagraphImpl&>(*cached).resolvedFonts().size(),
              static_cast<ParagraphImpl&>(*shaped).resolvedFonts().size());
}

}
