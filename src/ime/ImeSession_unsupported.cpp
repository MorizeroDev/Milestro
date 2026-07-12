#include <Milestro/common/milestro_platform.h>
#include <Milestro/ime/ImeSession.h>

#if !MILESTRO_PLATFORM_MAC && !MILESTRO_PLATFORM_IOS && !MILESTRO_PLATFORM_WINDOWS

namespace milestro::ime {

CancelCompositionResult CancelComposition() noexcept {
    return CancelCompositionResult::Unsupported;
}

} // namespace milestro::ime

#endif
