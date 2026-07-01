#ifndef MILESTRO_UNICODE_H
#define MILESTRO_UNICODE_H

#include "include/core/SkRefCnt.h"
#include "include/core/SkFontMgr.h"
#include "Milestro/util/milestro_class.h"
#include "Milestro/common/milestro_result.h"
#include "Milestro/common/milestro_export_macros.h"
#include <string>
#include <utility>
#include "modules/skunicode/include/SkUnicode.h"

namespace milestro::skia {
class MILESTRO_API UnicodeProvider {
public:
    explicit UnicodeProvider(sk_sp<SkUnicode> skUnicode) {
        this->skUnicode = std::move(skUnicode);
    }

    MILESTRO_DECLARE_NON_COPYABLE(UnicodeProvider)

    sk_sp<SkUnicode> unwrap() {
        return skUnicode;
    }

private:
    sk_sp<SkUnicode> skUnicode;
};

MILESTRO_API UnicodeProvider *GetUnicodeProvider();
}

#endif //MILESTRO_UNICODE_H
