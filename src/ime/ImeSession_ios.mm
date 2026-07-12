#include <Milestro/common/milestro_platform.h>
#include <Milestro/ime/ImeSession.h>

#if MILESTRO_PLATFORM_IOS

namespace milestro::ime {

CancelCompositionResult CancelComposition() noexcept {
    // Unity's active UITextInput client must be verified before UIKit cancellation is implemented.
    return CancelCompositionResult::Unsupported;
}

} // namespace milestro::ime

#endif
