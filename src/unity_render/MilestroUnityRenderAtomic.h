#pragma once

#include <atomic>
#include <cstdint>

namespace milestro::unity_render {

// These words are shared with managed code: keep their int32_t ABI, and never
// reinterpret them as std::atomic objects. Unity's bundled NDK r27 libc++ has
// no atomic_ref, so use compiler atomics there with the same memory ordering.
// Callers must provide naturally aligned storage and use atomic access while
// other threads may access the word. Initialization before publication is plain.
#if defined(__cpp_lib_atomic_ref) && __cpp_lib_atomic_ref >= 201806L
static_assert(std::atomic_ref<int32_t>::required_alignment <= alignof(int32_t));
static_assert(std::atomic_ref<int32_t>::is_always_lock_free);
#elif defined(__clang__) || defined(__GNUC__)
static_assert(__atomic_always_lock_free(sizeof(int32_t), nullptr));
#else
#error "Milestro requires atomic_ref or compiler atomics for shared int32_t words"
#endif

inline void AtomicStoreRelease(int32_t& word, int32_t value) noexcept {
#if defined(__cpp_lib_atomic_ref) && __cpp_lib_atomic_ref >= 201806L
    std::atomic_ref<int32_t>(word).store(value, std::memory_order_release);
#else
    __atomic_store_n(&word, value, __ATOMIC_RELEASE);
#endif
}

inline bool AtomicCompareExchangeAcquireRelease(int32_t& word, int32_t& expected, int32_t desired) noexcept {
#if defined(__cpp_lib_atomic_ref) && __cpp_lib_atomic_ref >= 201806L
    return std::atomic_ref<int32_t>(word).compare_exchange_strong(expected,
                                                                  desired,
                                                                  std::memory_order_acq_rel,
                                                                  std::memory_order_acquire);
#else
    return __atomic_compare_exchange_n(&word, &expected, desired, false, __ATOMIC_ACQ_REL, __ATOMIC_ACQUIRE);
#endif
}

} // namespace milestro::unity_render
