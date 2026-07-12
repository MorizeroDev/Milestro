#include "ime/ImeSession_windows_internal.h"

#include <gtest/gtest.h>

namespace {

struct FakeState {
    HWND window = reinterpret_cast<HWND>(1);
    HIMC context = reinterpret_cast<HIMC>(2);
    DWORD processId = 10;
    DWORD threadId = 20;
    BOOL notifyResult = TRUE;
    BOOL releaseResult = TRUE;
    int contextCalls = 0;
    int notifyCalls = 0;
    int releaseCalls = 0;
    DWORD notifyAction = 0;
    DWORD notifyIndex = 0;
    DWORD notifyValue = 1;
};

FakeState* state;

HWND GetFocus() {
    return state->window;
}

DWORD GetWindowThreadProcessId(HWND, LPDWORD processId) {
    *processId = state->processId;
    return state->threadId;
}

DWORD GetCurrentProcessId() {
    return 10;
}

DWORD GetCurrentThreadId() {
    return 20;
}

HIMC ImmGetContext(HWND) {
    ++state->contextCalls;
    return state->context;
}

BOOL ImmNotifyIme(HIMC, DWORD action, DWORD index, DWORD value) {
    ++state->notifyCalls;
    state->notifyAction = action;
    state->notifyIndex = index;
    state->notifyValue = value;
    return state->notifyResult;
}

BOOL ImmReleaseContext(HWND, HIMC) {
    ++state->releaseCalls;
    return state->releaseResult;
}

milestro::ime::windows::ImeApi MakeApi() {
    return {
            GetFocus,
            GetWindowThreadProcessId,
            GetCurrentProcessId,
            GetCurrentThreadId,
            ImmGetContext,
            ImmNotifyIme,
            ImmReleaseContext,
    };
}

TEST(ImeSessionWindowsTest, CancelsAndReleasesFocusedUnityThreadContext) {
    FakeState fake;
    state = &fake;

    const auto result = milestro::ime::windows::CancelCompositionWithApi(MakeApi());

    EXPECT_EQ(result, milestro::ime::CancelCompositionResult::Succeeded);
    EXPECT_EQ(fake.notifyCalls, 1);
    EXPECT_EQ(fake.notifyAction, NI_COMPOSITIONSTR);
    EXPECT_EQ(fake.notifyIndex, CPS_CANCEL);
    EXPECT_EQ(fake.notifyValue, 0u);
    EXPECT_EQ(fake.releaseCalls, 1);
}

TEST(ImeSessionWindowsTest, NotifyFailureStillReleasesContext) {
    FakeState fake;
    fake.notifyResult = FALSE;
    state = &fake;

    const auto result = milestro::ime::windows::CancelCompositionWithApi(MakeApi());

    EXPECT_EQ(result, milestro::ime::CancelCompositionResult::Failed);
    EXPECT_EQ(fake.notifyCalls, 1);
    EXPECT_EQ(fake.releaseCalls, 1);
}

TEST(ImeSessionWindowsTest, ReleaseFailureDoesNotReportCancellationSuccess) {
    FakeState fake;
    fake.releaseResult = FALSE;
    state = &fake;

    const auto result = milestro::ime::windows::CancelCompositionWithApi(MakeApi());

    EXPECT_EQ(result, milestro::ime::CancelCompositionResult::Failed);
    EXPECT_EQ(fake.notifyCalls, 1);
    EXPECT_EQ(fake.releaseCalls, 1);
}

TEST(ImeSessionWindowsTest, MissingContextFailsWithoutNotifyOrRelease) {
    FakeState fake;
    fake.context = nullptr;
    state = &fake;

    const auto result = milestro::ime::windows::CancelCompositionWithApi(MakeApi());

    EXPECT_EQ(result, milestro::ime::CancelCompositionResult::NoActiveContext);
    EXPECT_EQ(fake.contextCalls, 1);
    EXPECT_EQ(fake.notifyCalls, 0);
    EXPECT_EQ(fake.releaseCalls, 0);
}

TEST(ImeSessionWindowsTest, MissingFocusedWindowFailsBeforeContextLookup) {
    FakeState fake;
    fake.window = nullptr;
    state = &fake;

    const auto result = milestro::ime::windows::CancelCompositionWithApi(MakeApi());

    EXPECT_EQ(result, milestro::ime::CancelCompositionResult::NoActiveContext);
    EXPECT_EQ(fake.contextCalls, 0);
    EXPECT_EQ(fake.notifyCalls, 0);
    EXPECT_EQ(fake.releaseCalls, 0);
}

TEST(ImeSessionWindowsTest, RejectsWindowFromAnotherProcess) {
    FakeState fake;
    fake.processId = 11;
    state = &fake;

    const auto result = milestro::ime::windows::CancelCompositionWithApi(MakeApi());

    EXPECT_EQ(result, milestro::ime::CancelCompositionResult::Failed);
    EXPECT_EQ(fake.contextCalls, 0);
    EXPECT_EQ(fake.notifyCalls, 0);
    EXPECT_EQ(fake.releaseCalls, 0);
}

TEST(ImeSessionWindowsTest, RejectsWindowFromAnotherThread) {
    FakeState fake;
    fake.threadId = 21;
    state = &fake;

    const auto result = milestro::ime::windows::CancelCompositionWithApi(MakeApi());

    EXPECT_EQ(result, milestro::ime::CancelCompositionResult::Failed);
    EXPECT_EQ(fake.contextCalls, 0);
    EXPECT_EQ(fake.notifyCalls, 0);
    EXPECT_EQ(fake.releaseCalls, 0);
}

} // namespace
