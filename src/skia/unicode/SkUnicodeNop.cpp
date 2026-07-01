#include "SkUnicodeNop.h"

#include "src/core/SkBitmaskEnum.h"
#include "src/core/SkUTF.h"

#include <algorithm>
#include <cstdint>
#include <memory>
#include <vector>

namespace milestro::skia {
namespace {

class NopBidiIterator final : public SkBidiIterator {
public:
    NopBidiIterator(int count, Direction direction)
            : length(std::max(count, 0)),
              level(direction == kRTL ? 1 : 0) {
    }

    Position getLength() override {
        return length;
    }

    Level getLevelAt(Position) override {
        return level;
    }

private:
    Position length;
    Level level;
};

class NopBreakIterator final : public SkBreakIterator {
public:
    explicit NopBreakIterator(SkUnicode::BreakType breakType)
            : breakType(breakType) {
        reset();
    }

    Position first() override {
        index = 0;
        done = breaks.empty();
        return current();
    }

    Position current() override {
        if (done || breaks.empty()) {
            return kDone;
        }
        return breaks[index].position;
    }

    Position next() override {
        if (done || breaks.empty()) {
            return kDone;
        }
        if (index + 1 >= breaks.size()) {
            done = true;
            return kDone;
        }
        ++index;
        return current();
    }

    Status status() override {
        if (done || breaks.empty()) {
            return SkUnicode::kNoCodeUnitFlag;
        }
        return breaks[index].status;
    }

    bool isDone() override {
        return done;
    }

    bool setText(const char utftext8[], int utf8Units) override {
        reset();
        if (utftext8 == nullptr) {
            return utf8Units == 0;
        }

        for (int i = 0; i < utf8Units; ++i) {
            const char ch = utftext8[i];
            if (breakType == SkUnicode::BreakType::kGraphemes) {
                addBreak(i + 1, SkUnicode::kGraphemeStart);
            } else if (ch == '\n' || ch == '\r') {
                addBreak(i + 1, SkUnicode::kHardLineBreakBefore);
            } else if (isAsciiSpace(ch)) {
                addBreak(i + 1, SkUnicode::kSoftLineBreakBefore);
            }
        }
        addBreak(std::max(utf8Units, 0), SkUnicode::kSoftLineBreakBefore);
        first();
        return utf8Units >= 0;
    }

    bool setText(const char16_t utftext16[], int utf16Units) override {
        reset();
        if (utftext16 == nullptr) {
            return utf16Units == 0;
        }

        for (int i = 0; i < utf16Units; ++i) {
            const char16_t ch = utftext16[i];
            if (breakType == SkUnicode::BreakType::kGraphemes) {
                addBreak(i + 1, SkUnicode::kGraphemeStart);
            } else if (ch == u'\n' || ch == u'\r' || ch == u'\u2028') {
                addBreak(i + 1, SkUnicode::kHardLineBreakBefore);
            } else if (isSpace(ch)) {
                addBreak(i + 1, SkUnicode::kSoftLineBreakBefore);
            }
        }
        addBreak(std::max(utf16Units, 0), SkUnicode::kSoftLineBreakBefore);
        first();
        return utf16Units >= 0;
    }

private:
    struct Break {
        Position position;
        Status status;
    };

    static constexpr Position kDone = -1;

    static bool isAsciiSpace(char ch) {
        return ch == '\t' || ch == '\n' || ch == '\v' || ch == '\f' || ch == '\r' || ch == ' ';
    }

    static bool isSpace(char16_t ch) {
        return ch == u'\t' || ch == u'\n' || ch == u'\v' || ch == u'\f' || ch == u'\r' ||
               ch == u' ' || ch == u'\u2028' || ch == u'\u2029';
    }

    void reset() {
        breaks.clear();
        addBreak(0, SkUnicode::kSoftLineBreakBefore);
        index = 0;
        done = false;
    }

    void addBreak(Position position, Status status) {
        if (!breaks.empty() && breaks.back().position == position) {
            breaks.back().status |= status;
            return;
        }
        breaks.push_back({position, status});
    }

    SkUnicode::BreakType breakType;
    std::vector<Break> breaks;
    size_t index = 0;
    bool done = false;
};

class NopUnicode final : public SkUnicode {
public:
    SkString toUpper(const SkString& str) override {
        return str;
    }

    SkString toUpper(const SkString& str, const char*) override {
        return str;
    }

    bool isControl(SkUnichar unichar) override {
        return (unichar < ' ') || (unichar >= 0x7f && unichar <= 0x9f) ||
               (unichar >= 0x200D && unichar <= 0x200F) ||
               (unichar >= 0x202A && unichar <= 0x202E);
    }

    bool isWhitespace(SkUnichar unichar) override {
        switch (unichar) {
            case 0x0009:
            case 0x000A:
            case 0x000B:
            case 0x000C:
            case 0x000D:
            case 0x0020:
            case 0x1680:
            case 0x2000:
            case 0x2001:
            case 0x2002:
            case 0x2003:
            case 0x2004:
            case 0x2005:
            case 0x2006:
            case 0x2008:
            case 0x2009:
            case 0x200A:
            case 0x2028:
            case 0x2029:
            case 0x205F:
            case 0x3000:
                return true;
            default:
                return false;
        }
    }

    bool isSpace(SkUnichar unichar) override {
        return isWhitespace(unichar) || unichar == 0x0085 || unichar == 0x00A0 ||
               unichar == 0x2007 || unichar == 0x202F;
    }

    bool isTabulation(SkUnichar unichar) override {
        return unichar == '\t';
    }

    bool isHardBreak(SkUnichar unichar) override {
        return unichar == '\n' || unichar == '\r' || unichar == 0x2028;
    }

    bool isEmoji(SkUnichar) override {
        return false;
    }

    bool isEmojiComponent(SkUnichar) override {
        return false;
    }

    bool isEmojiModifierBase(SkUnichar) override {
        return false;
    }

    bool isEmojiModifier(SkUnichar) override {
        return false;
    }

    bool isRegionalIndicator(SkUnichar) override {
        return false;
    }

    bool isIdeographic(SkUnichar unichar) override {
        return (unichar >= 4352 && unichar < 4607) ||
               (unichar >= 11904 && unichar < 42191) ||
               (unichar >= 43072 && unichar < 43135) ||
               (unichar >= 44032 && unichar < 55215) ||
               (unichar >= 63744 && unichar < 64255) ||
               (unichar >= 65072 && unichar < 65103) ||
               (unichar >= 65381 && unichar < 65500) ||
               (unichar >= 131072 && unichar < 196607);
    }

    std::unique_ptr<SkBidiIterator> makeBidiIterator(
            const uint16_t[], int count, SkBidiIterator::Direction direction) override {
        return std::make_unique<NopBidiIterator>(count, direction);
    }

    std::unique_ptr<SkBidiIterator> makeBidiIterator(
            const char[], int count, SkBidiIterator::Direction direction) override {
        return std::make_unique<NopBidiIterator>(count, direction);
    }

    std::unique_ptr<SkBreakIterator> makeBreakIterator(const char[], BreakType breakType) override {
        return std::make_unique<NopBreakIterator>(breakType);
    }

    std::unique_ptr<SkBreakIterator> makeBreakIterator(BreakType breakType) override {
        return std::make_unique<NopBreakIterator>(breakType);
    }

    bool getBidiRegions(const char[], int utf8Units, TextDirection direction,
                        std::vector<BidiRegion>* results) override {
        if (results == nullptr) {
            return false;
        }
        results->clear();
        if (utf8Units > 0) {
            results->emplace_back(0, utf8Units, direction == TextDirection::kRTL ? 1 : 0);
        }
        return true;
    }

    bool getWords(const char utf8[], int utf8Units, const char*, std::vector<Position>* results) override {
        if (results == nullptr) {
            return false;
        }
        results->clear();
        addUniquePosition(results, 0);
        if (utf8 != nullptr && utf8Units > 0) {
            int utf16Units = 0;
            const char* current = utf8;
            const char* end = utf8 + utf8Units;
            while (current < end) {
                SkUnichar unichar = SkUTF::NextUTF8(&current, end);
                if (unichar < 0) {
                    unichar = 0xFFFD;
                }
                uint16_t buffer[2];
                utf16Units += static_cast<int>(SkUTF::ToUTF16(unichar, buffer));
                if (isSpace(unichar) || isHardBreak(unichar) || current == end) {
                    addUniquePosition(results, utf16Units);
                }
            }
        }
        addUniquePosition(results, utf16UnitsForUtf8(utf8, utf8Units));
        return true;
    }

    bool getUtf8Words(const char utf8[], int utf8Units, const char*, std::vector<Position>* results) override {
        if (results == nullptr) {
            return false;
        }
        results->clear();
        addUniquePosition(results, 0);
        if (utf8 != nullptr && utf8Units > 0) {
            const char* current = utf8;
            const char* end = utf8 + utf8Units;
            while (current < end) {
                SkUnichar unichar = SkUTF::NextUTF8(&current, end);
                if (unichar < 0) {
                    unichar = 0xFFFD;
                }
                if (isSpace(unichar) || isHardBreak(unichar) || current == end) {
                    addUniquePosition(results, static_cast<Position>(current - utf8));
                }
            }
        }
        addUniquePosition(results, std::max(utf8Units, 0));
        return true;
    }

    bool getSentences(const char utf8[], int utf8Units, const char*, std::vector<Position>* results) override {
        if (results == nullptr) {
            return false;
        }
        results->clear();
        addUniquePosition(results, 0);
        if (utf8 != nullptr && utf8Units > 0) {
            for (int i = 0; i < utf8Units; ++i) {
                if (utf8[i] == '.' || utf8[i] == '!' || utf8[i] == '?' || utf8[i] == '\n') {
                    addUniquePosition(results, i + 1);
                }
            }
        }
        addUniquePosition(results, std::max(utf8Units, 0));
        return true;
    }

    bool computeCodeUnitFlags(char utf8[], int utf8Units, bool replaceTabs,
                              skia_private::TArray<CodeUnitFlags, true>* results) override {
        if (results == nullptr || utf8Units < 0 || (utf8 == nullptr && utf8Units > 0)) {
            return false;
        }

        results->clear();
        results->push_back_n(utf8Units + 1, CodeUnitFlags::kNoCodeUnitFlag);
        (*results)[0] |= CodeUnitFlags::kSoftLineBreakBefore | CodeUnitFlags::kGraphemeStart;
        (*results)[utf8Units] |= CodeUnitFlags::kSoftLineBreakBefore | CodeUnitFlags::kGraphemeStart;
        if (utf8Units == 0) {
            return true;
        }

        const char* current = utf8;
        const char* end = utf8 + utf8Units;
        while (current < end) {
            const auto before = static_cast<int>(current - utf8);
            SkUnichar unichar = SkUTF::NextUTF8(&current, end);
            if (unichar < 0) {
                unichar = 0xFFFD;
            }
            const auto after = static_cast<int>(current - utf8);

            (*results)[before] |= CodeUnitFlags::kGraphemeStart;
            if (replaceTabs && isTabulation(unichar)) {
                (*results)[before] |= CodeUnitFlags::kTabulation;
                unichar = ' ';
                utf8[before] = ' ';
            }
            applyCodepointFlags(unichar, before, after, results);
            if (isHardBreak(unichar)) {
                (*results)[after] |= CodeUnitFlags::kHardLineBreakBefore;
            } else if (isSpace(unichar)) {
                (*results)[after] |= CodeUnitFlags::kSoftLineBreakBefore;
            }
        }
        return true;
    }

    bool computeCodeUnitFlags(char16_t utf16[], int utf16Units, bool replaceTabs,
                              skia_private::TArray<CodeUnitFlags, true>* results) override {
        if (results == nullptr || utf16Units < 0 || (utf16 == nullptr && utf16Units > 0)) {
            return false;
        }

        results->clear();
        results->push_back_n(utf16Units + 1, CodeUnitFlags::kNoCodeUnitFlag);
        (*results)[0] |= CodeUnitFlags::kSoftLineBreakBefore | CodeUnitFlags::kGraphemeStart;
        (*results)[utf16Units] |= CodeUnitFlags::kSoftLineBreakBefore | CodeUnitFlags::kGraphemeStart;
        if (utf16Units == 0) {
            return true;
        }

        const uint16_t* base = reinterpret_cast<const uint16_t*>(utf16);
        const uint16_t* current = base;
        const uint16_t* end = base + utf16Units;
        while (current < end) {
            const auto before = static_cast<int>(current - base);
            SkUnichar unichar = SkUTF::NextUTF16(&current, end);
            if (unichar < 0) {
                unichar = 0xFFFD;
            }
            const auto after = static_cast<int>(current - base);

            (*results)[before] |= CodeUnitFlags::kGraphemeStart;
            if (replaceTabs && isTabulation(unichar)) {
                (*results)[before] |= CodeUnitFlags::kTabulation;
                unichar = ' ';
                utf16[before] = u' ';
            }
            applyCodepointFlags(unichar, before, after, results);
            if (isHardBreak(unichar)) {
                (*results)[after] |= CodeUnitFlags::kHardLineBreakBefore;
            } else if (isSpace(unichar)) {
                (*results)[after] |= CodeUnitFlags::kSoftLineBreakBefore;
            }
        }
        return true;
    }

    void reorderVisual(const BidiLevel[], int levelsCount, int32_t logicalFromVisual[]) override {
        if (logicalFromVisual == nullptr) {
            return;
        }
        for (int i = 0; i < levelsCount; ++i) {
            logicalFromVisual[i] = i;
        }
    }

private:
    static void addUniquePosition(std::vector<Position>* results, Position position) {
        if (results->empty() || results->back() != position) {
            results->push_back(position);
        }
    }

    static int utf16UnitsForUtf8(const char* utf8, int utf8Units) {
        if (utf8 == nullptr || utf8Units <= 0) {
            return 0;
        }

        int utf16Units = 0;
        const char* current = utf8;
        const char* end = utf8 + utf8Units;
        while (current < end) {
            SkUnichar unichar = SkUTF::NextUTF8(&current, end);
            if (unichar < 0) {
                unichar = 0xFFFD;
            }
            uint16_t buffer[2];
            utf16Units += static_cast<int>(SkUTF::ToUTF16(unichar, buffer));
        }
        return utf16Units;
    }

    void applyCodepointFlags(SkUnichar unichar, int before, int after,
                             skia_private::TArray<CodeUnitFlags, true>* results) {
        for (int i = before; i < after; ++i) {
            if (isSpace(unichar)) {
                (*results)[i] |= CodeUnitFlags::kPartOfIntraWordBreak;
            }
            if (isWhitespace(unichar)) {
                (*results)[i] |= CodeUnitFlags::kPartOfWhiteSpaceBreak;
            }
            if (isControl(unichar)) {
                (*results)[i] |= CodeUnitFlags::kControl;
            }
            if (isIdeographic(unichar)) {
                (*results)[i] |= CodeUnitFlags::kIdeographic;
            }
        }
    }
};

} // namespace

sk_sp<SkUnicode> MakeNopSkUnicode() {
    return sk_make_sp<NopUnicode>();
}

} // namespace milestro::skia
