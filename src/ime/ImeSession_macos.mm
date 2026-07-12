#include <Milestro/common/milestro_platform.h>
#include <Milestro/ime/ImeSession.h>

#if MILESTRO_PLATFORM_MAC

#import <AppKit/AppKit.h>

namespace milestro::ime {

CancelCompositionResult CancelComposition() noexcept {
    @autoreleasepool {
        if (![NSThread isMainThread]) {
            return CancelCompositionResult::WrongThread;
        }

        NSTextInputContext* inputContext = [NSTextInputContext currentInputContext];
        if (inputContext == nil) {
            return CancelCompositionResult::NoActiveContext;
        }

        @try {
            [inputContext discardMarkedText];
            return CancelCompositionResult::Succeeded;
        } @catch (NSException* exception) {
            (void) exception;
            return CancelCompositionResult::Failed;
        }
    }
}

} // namespace milestro::ime

#endif
