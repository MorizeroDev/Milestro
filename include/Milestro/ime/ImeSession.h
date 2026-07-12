#ifndef MILESTRO_IME_SESSION_H
#define MILESTRO_IME_SESSION_H

#include <cstdint>

namespace milestro::ime {

enum class CancelCompositionResult : int32_t {
    NotAttempted = 0,
    Succeeded = 1,
    Unsupported = 2,
    WrongThread = 3,
    NoActiveContext = 4,
    Failed = 5,
    NativeUnavailable = 6,
};

CancelCompositionResult CancelComposition() noexcept;

} // namespace milestro::ime

#endif // MILESTRO_IME_SESSION_H
