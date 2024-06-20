#include "FontManager.h"
#include "Milestro/log/log.h"

#if WIN32

#include "include/ports/SkTypeface_win.h"

#endif

namespace milestro::skia {

inline sk_sp<SkFontMgr> MakeSkFontMgr() {
  sk_sp<SkFontMgr> result;
#if WIN32
  result = SkFontMgr_New_DirectWrite();
#else
#error No SkFontMgr Provider
#endif
  return result;
}

std::unique_ptr<FontManager> FontManagerInstance = nullptr;

Result<void, std::string> InitialFontManager() {
  auto skFontMgr = MakeSkFontMgr();
  if (skFontMgr == nullptr) {
    return Err(std::string("fail to createSkFontMgr"));
  }
  FontManagerInstance = std::make_unique<FontManager>(std::move(skFontMgr));
  return Ok();
}

FontManager *GetFontManager() {
  if (FontManagerInstance == nullptr) {
    auto result = InitialFontManager();
    if (result.isErr()) {
      MILESTROLOG_ERROR("{}", result.unwrapErr());
    }
  }

  return FontManagerInstance.get();
}

}
