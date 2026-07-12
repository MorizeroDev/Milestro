#ifndef MILESTRO_IME_SESSION_WINDOWS_INTERNAL_H
#define MILESTRO_IME_SESSION_WINDOWS_INTERNAL_H

#include <Milestro/ime/ImeSession.h>

#include <Windows.h>
#include <imm.h>

namespace milestro::ime::windows {

struct ImeApi {
    HWND (*getFocus)();
    DWORD (*getWindowThreadProcessId)(HWND window, LPDWORD processId);
    DWORD (*getCurrentProcessId)();
    DWORD (*getCurrentThreadId)();
    HIMC (*immGetContext)(HWND window);
    BOOL (*immNotifyIme)(HIMC context, DWORD action, DWORD index, DWORD value);
    BOOL (*immReleaseContext)(HWND window, HIMC context);
};

CancelCompositionResult CancelCompositionWithApi(const ImeApi& api) noexcept;

} // namespace milestro::ime::windows

#endif // MILESTRO_IME_SESSION_WINDOWS_INTERNAL_H
