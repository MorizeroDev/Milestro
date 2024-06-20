#ifndef MILESTRO_FONTMANAGER_H
#define MILESTRO_FONTMANAGER_H

#include <include/core/SkRefCnt.h>
#include <include/core/SkFontMgr.h>
#include <include/core/SkTypeface.h>
#include <Milestro/util/milestro_class.h>
#include "Milestro/common/milestro_result.h"
#include <string>
#include <utility>

namespace milestro::skia {

class FontManager {
public:
  explicit FontManager(sk_sp<SkFontMgr> fontMgr) {
    this->fontMgr = std::move(fontMgr);
  }

  MILESTRO_DECLARE_NON_COPYABLE(FontManager)

  void RegisterFont(char *path) {
    fontMgr->makeFromFile(path);
  }

  sk_sp<SkFontMgr> unwrap() {
    return fontMgr;
  }

private:
  sk_sp<SkFontMgr> fontMgr;
};

FontManager *GetFontManager();
}

#endif //MILESTRO_FONTMANAGER_H
