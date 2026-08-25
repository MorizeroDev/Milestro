#ifndef MILESTRO_UNITY_RENDER_DIAGNOSTICS_H
#define MILESTRO_UNITY_RENDER_DIAGNOSTICS_H

#include <cstddef>
#include <cstdint>
#include <limits>
#include <mutex>

namespace milestro::unity_render {

inline constexpr uint32_t kMilestroUnityRenderDiagnosticsAbiVersion = 1;

struct MilestroUnityRenderDiagnosticsSnapshot {
    uint32_t abiVersion = kMilestroUnityRenderDiagnosticsAbiVersion;
    uint32_t structSize = 0;
    uint64_t acceptedSubmissionCount = 0;
    uint64_t rejectedSubmissionCount = 0;
    int32_t hasLastAcceptedSubmission = 0;
    int32_t lastAcceptedGraphicsBackend = 0;
    int32_t lastAcceptedRasterWidth = 0;
    int32_t lastAcceptedRasterHeight = 0;
    float lastAcceptedEffectiveScale = 0.0f;
    uint32_t reserved = 0;
    uint64_t lastAcceptedDeviceEpoch = 0;
    uint64_t currentDeviceEpoch = 0;
};

static_assert(sizeof(MilestroUnityRenderDiagnosticsSnapshot) <= std::numeric_limits<uint32_t>::max());
inline constexpr uint32_t kMilestroUnityRenderDiagnosticsSnapshotSize =
        static_cast<uint32_t>(sizeof(MilestroUnityRenderDiagnosticsSnapshot));

static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, abiVersion) == 0);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, structSize) == sizeof(uint32_t));
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, acceptedSubmissionCount) == 8);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, rejectedSubmissionCount) == 16);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, hasLastAcceptedSubmission) == 24);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedGraphicsBackend) == 28);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedRasterWidth) == 32);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedRasterHeight) == 36);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedEffectiveScale) == 40);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, reserved) == 44);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedDeviceEpoch) == 48);
static_assert(offsetof(MilestroUnityRenderDiagnosticsSnapshot, currentDeviceEpoch) == 56);
static_assert(kMilestroUnityRenderDiagnosticsSnapshotSize == 64);

class MilestroUnityRenderDiagnostics {
public:
    void RecordRejectedSubmission() {
        std::lock_guard lock(mutex_);
        SaturatingIncrement(rejectedSubmissionCount_);
    }

    void RecordAcceptedSubmission(int32_t graphicsBackend,
                                  int32_t rasterWidth,
                                  int32_t rasterHeight,
                                  float effectiveScale,
                                  uint64_t deviceEpoch) {
        std::lock_guard lock(mutex_);
        SaturatingIncrement(acceptedSubmissionCount_);
        hasLastAcceptedSubmission_ = true;
        lastAcceptedGraphicsBackend_ = graphicsBackend;
        lastAcceptedRasterWidth_ = rasterWidth;
        lastAcceptedRasterHeight_ = rasterHeight;
        lastAcceptedEffectiveScale_ = effectiveScale;
        lastAcceptedDeviceEpoch_ = deviceEpoch;
    }

    [[nodiscard]] MilestroUnityRenderDiagnosticsSnapshot Snapshot(uint64_t currentDeviceEpoch) const {
        std::lock_guard lock(mutex_);

        MilestroUnityRenderDiagnosticsSnapshot snapshot;
        snapshot.structSize = kMilestroUnityRenderDiagnosticsSnapshotSize;
        snapshot.acceptedSubmissionCount = acceptedSubmissionCount_;
        snapshot.rejectedSubmissionCount = rejectedSubmissionCount_;
        snapshot.hasLastAcceptedSubmission = hasLastAcceptedSubmission_ ? 1 : 0;
        snapshot.lastAcceptedGraphicsBackend = lastAcceptedGraphicsBackend_;
        snapshot.lastAcceptedRasterWidth = lastAcceptedRasterWidth_;
        snapshot.lastAcceptedRasterHeight = lastAcceptedRasterHeight_;
        snapshot.lastAcceptedEffectiveScale = lastAcceptedEffectiveScale_;
        snapshot.lastAcceptedDeviceEpoch = lastAcceptedDeviceEpoch_;
        snapshot.currentDeviceEpoch = currentDeviceEpoch;
        return snapshot;
    }

private:
    static void SaturatingIncrement(uint64_t& value) {
        if (value != std::numeric_limits<uint64_t>::max()) {
            ++value;
        }
    }

    mutable std::mutex mutex_;
    uint64_t acceptedSubmissionCount_ = 0;
    uint64_t rejectedSubmissionCount_ = 0;
    bool hasLastAcceptedSubmission_ = false;
    int32_t lastAcceptedGraphicsBackend_ = 0;
    int32_t lastAcceptedRasterWidth_ = 0;
    int32_t lastAcceptedRasterHeight_ = 0;
    float lastAcceptedEffectiveScale_ = 0.0f;
    uint64_t lastAcceptedDeviceEpoch_ = 0;
};

} // namespace milestro::unity_render

#endif // MILESTRO_UNITY_RENDER_DIAGNOSTICS_H
