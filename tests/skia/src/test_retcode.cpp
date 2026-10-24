#include "Milestro/game/milestro_game_retcode.h"
#include "game/milestro_game_retcode.h"

#include <cstdint>

#include <gtest/gtest.h>

namespace {

struct ThrowingDestructor {
    ~ThrowingDestructor() noexcept(false) {
        throw 7;
    }
};

} // namespace

TEST(MilestroApiStatus, KeepsFailureSignAndEncodesCleanupDisposition) {
    const auto status = MILESTRO_API_STATUS_MAKE(
        MILESTRO_API_STATUS_SEVERITY_WARNING,
        MILESTRO_API_STATUS_FLAG_CONSUMED,
        MILESTRO_API_STATUS_DOMAIN_NATIVE,
        MILESTRO_API_STATUS_OPERATION_DESTROY,
        MILESTRO_API_STATUS_DETAIL_UNEXPECTED_EXCEPTION);
    const auto raw = static_cast<uint64_t>(status);

    EXPECT_GE(status, 0);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_SEVERITY_SHIFT) & MILESTRO_API_STATUS_SEVERITY_MASK,
              MILESTRO_API_STATUS_SEVERITY_WARNING);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_FLAGS_SHIFT) & MILESTRO_API_STATUS_FLAGS_MASK,
              MILESTRO_API_STATUS_FLAG_CONSUMED);
}

TEST(MilestroApiStatus, DestroyConsumesHandleWhenDestructorThrows) {
    auto* resource = new ThrowingDestructor();

    const auto status = MilestroDestroyHandle(resource);
    const auto raw = static_cast<uint64_t>(status);

    EXPECT_EQ(resource, nullptr);
    EXPECT_GE(status, 0);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_SEVERITY_SHIFT) & MILESTRO_API_STATUS_SEVERITY_MASK,
              MILESTRO_API_STATUS_SEVERITY_WARNING);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_FLAGS_SHIFT) & MILESTRO_API_STATUS_FLAGS_MASK,
              MILESTRO_API_STATUS_FLAG_CONSUMED);
}
