#include <Milestro/common/milestro_platform.h>
#include <Milestro/ime/ImeSession.h>

#if MILESTRO_PLATFORM_WINDOWS

#include "ImeSession_windows_internal.h"

namespace milestro::ime::windows {
namespace {

HWND GetFocusedWindow() {
    return ::GetFocus();
}

DWORD GetWindowThread(HWND window, LPDWORD processId) {
    return ::GetWindowThreadProcessId(window, processId);
}

DWORD GetProcess() {
    return ::GetCurrentProcessId();
}

DWORD GetThread() {
    return ::GetCurrentThreadId();
}

HIMC GetImeContext(HWND window) {
    return ::ImmGetContext(window);
}

BOOL NotifyIme(HIMC context, DWORD action, DWORD index, DWORD value) {
    return ::ImmNotifyIME(context, action, index, value);
}

BOOL ReleaseImeContext(HWND window, HIMC context) {
    return ::ImmReleaseContext(window, context);
}

const ImeApi SystemApi{
        GetFocusedWindow,
        GetWindowThread,
        GetProcess,
        GetThread,
        GetImeContext,
        NotifyIme,
        ReleaseImeContext,
};

class ScopedImeContext {
public:
    ScopedImeContext(const ImeApi& api, HWND window, HIMC context) : api_(api), window_(window), context_(context) {
    }

    ~ScopedImeContext() {
        if (context_ != nullptr) {
            api_.immReleaseContext(window_, context_);
        }
    }

    ScopedImeContext(const ScopedImeContext&) = delete;
    ScopedImeContext& operator=(const ScopedImeContext&) = delete;

    HIMC Get() const {
        return context_;
    }

    BOOL Release() {
        const BOOL result = api_.immReleaseContext(window_, context_);
        context_ = nullptr;
        return result;
    }

private:
    const ImeApi& api_;
    HWND window_;
    HIMC context_;
};

} // namespace

CancelCompositionResult CancelCompositionWithApi(const ImeApi& api) noexcept {
    const HWND window = api.getFocus();
    if (window == nullptr) {
        return CancelCompositionResult::NoActiveContext;
    }

    DWORD processId = 0;
    const DWORD windowThreadId = api.getWindowThreadProcessId(window, &processId);
    if (windowThreadId == 0 || processId != api.getCurrentProcessId() || windowThreadId != api.getCurrentThreadId()) {
        return CancelCompositionResult::Failed;
    }

    const HIMC rawContext = api.immGetContext(window);
    if (rawContext == nullptr) {
        return CancelCompositionResult::NoActiveContext;
    }
    ScopedImeContext context(api, window, rawContext);

    const BOOL cancelled = api.immNotifyIme(context.Get(), NI_COMPOSITIONSTR, CPS_CANCEL, 0);
    const BOOL released = context.Release();
    return cancelled && released ? CancelCompositionResult::Succeeded : CancelCompositionResult::Failed;
}

} // namespace milestro::ime::windows

namespace milestro::ime {

CancelCompositionResult CancelComposition() noexcept {
    return windows::CancelCompositionWithApi(windows::SystemApi);
}

} // namespace milestro::ime

#endif
